#!/usr/bin/env python3
"""
Leikpyar Hunters - the admin panel (the server side). Python standard library only.

  /admin/                    the panel (site/admin/)
  /api/admin/...             the admin API (needs the admin cookie lh_admin)
  /uploads/<name>            the pictures the admins uploaded (data/uploads/)

What the admins can do:
  - read the numbers about the agents (accounts) in charts, find an agent, block, sign out, reset the password, delete
  - edit EVERY text of the website, English and Burmese (the texts are found in site/index.html and site/js/i18n-my.js)
  - replace every picture (the creators' photos, the logo, the icons)
  - edit the Privacy page and the contact address of the license
  - create other admins, see the activity log

How the edited texts reach the website: the changes are saved in the database (table content). The server puts them into index.html as
window.LH_CONTENT (see serve_index) and site/js/content.js applies them before the page is shown. The files of the site are never changed,
so a new deploy of the website keeps every edit.

The first admin is made on the server:   LH_ADMIN_PASSWORD='...' python3 server.py --create-admin you@example.com
"""
import base64
import csv
import gzip
import hashlib
import hmac
import io
import json
import os
import re
import secrets
import sys
import time
from html.parser import HTMLParser

S = {}                      # the helpers of server.py (setup)
_cache = {"payload": None}  # the content that goes into the page (made again after every change)

LOCAL_UTC_OFFSET = 6 * 3600 + 1800        # Myanmar time: the days and the hours of the charts
ADMIN_SESSION_DAYS = 7
EMAIL_RE = re.compile(r"^[^@\s]{1,64}@[^@\s]{1,190}\.[A-Za-z]{2,}$")

SECTION_NAMES = {
    "hero": "Home (top of the page)",
    "night": "The night",
    "play": "Play",
    "gallery": "Gallery",
    "scores": "High scores",
    "agent": "Agent account",
    "license": "Credits and license",
    "privacy": "Privacy",
}

# the attributes the site swaps to Burmese (see js/i18n.js)
TEXT_ATTRS = ("placeholder", "aria-label", "title", "alt")


def setup(ns):
    S.clear()
    S.update(ns)


def db():
    return S["db"]()


def norm(s):
    return re.sub(r"\s+", " ", s).strip()


def now():
    return int(time.time())


def ok(h, **extra):
    out = {"ok": True}
    out.update(extra)
    h.send_json(200, out)


# ----------------------------------------------------------------------------------------------- database
def init_admin_db():
    c = db()
    c.executescript(
        """
        CREATE TABLE IF NOT EXISTS admins (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            email TEXT NOT NULL UNIQUE COLLATE NOCASE,
            salt BLOB NOT NULL,
            pw BLOB NOT NULL,
            created INTEGER NOT NULL,
            last_login INTEGER NOT NULL DEFAULT 0,
            created_by TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS admin_sessions (
            token_hash TEXT PRIMARY KEY,
            admin_id INTEGER NOT NULL REFERENCES admins(id) ON DELETE CASCADE,
            created INTEGER NOT NULL,
            expires INTEGER NOT NULL,
            ip TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS content (
            kind TEXT NOT NULL,
            key TEXT NOT NULL,
            value TEXT NOT NULL,
            updated INTEGER NOT NULL,
            by TEXT NOT NULL DEFAULT '',
            PRIMARY KEY (kind, key)
        );
        CREATE TABLE IF NOT EXISTS admin_log (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            at INTEGER NOT NULL,
            admin TEXT NOT NULL,
            action TEXT NOT NULL,
            detail TEXT NOT NULL DEFAULT ''
        );
        CREATE TABLE IF NOT EXISTS logins (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            agent_id INTEGER NOT NULL,
            at INTEGER NOT NULL,
            device TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS idx_logins_at ON logins(at);
        CREATE INDEX IF NOT EXISTS idx_admin_log_at ON admin_log(at DESC);
        CREATE INDEX IF NOT EXISTS idx_runs_created ON runs(created);
        """
    )
    cols = [r["name"] for r in c.execute("PRAGMA table_info(agents)").fetchall()]
    if "banned" not in cols:
        c.execute("ALTER TABLE agents ADD COLUMN banned INTEGER NOT NULL DEFAULT 0")
    if "gender" not in cols:        # 'male', 'female', or '' (an old agent who has not chosen yet)
        c.execute("ALTER TABLE agents ADD COLUMN gender TEXT NOT NULL DEFAULT ''")
    c.commit()


def device_of(user_agent):
    return "mobile" if re.search(r"Mobi|Android|iPhone|iPad|iPod", user_agent or "") else "desktop"


def record_login(agent_id, user_agent):
    """Called by server.py when an agent signs in or is created (the charts: activity and devices)."""
    try:
        c = db()
        c.execute("INSERT INTO logins (agent_id, at, device) VALUES (?, ?, ?)", (agent_id, now(), device_of(user_agent)))
        c.commit()
    except Exception as e:  # a chart must never stop a sign in
        sys.stderr.write("record_login: %r\n" % (e,))


def log(admin_email, action, detail=""):
    c = db()
    c.execute("INSERT INTO admin_log (at, admin, action, detail) VALUES (?, ?, ?, ?)", (now(), admin_email, action, detail[:400]))
    c.execute("DELETE FROM admin_log WHERE id < (SELECT COALESCE(MAX(id), 0) - 5000 FROM admin_log)")
    c.commit()


# ----------------------------------------------------------------------------------------------- admins
def create_admin(email, password, by=""):
    c = db()
    salt = secrets.token_bytes(16)
    pw = S["hash_password"](password, salt)
    c.execute("INSERT INTO admins (email, salt, pw, created, created_by) VALUES (?, ?, ?, ?, ?)", (email.lower(), salt, pw, now(), by))
    c.commit()


def cli_create_admin(email):
    """python3 server.py --create-admin you@example.com   (the password is in the LH_ADMIN_PASSWORD variable). Existing admin: new password."""
    password = os.environ.get("LH_ADMIN_PASSWORD", "")
    email = email.strip().lower()
    if not EMAIL_RE.match(email):
        print("That is not an e-mail address.")
        return 1
    if len(password) < 8:
        print("Set LH_ADMIN_PASSWORD (8 characters or more).")
        return 1
    c = db()
    row = c.execute("SELECT id FROM admins WHERE email = ?", (email,)).fetchone()
    if row:
        salt = secrets.token_bytes(16)
        c.execute("UPDATE admins SET salt = ?, pw = ? WHERE id = ?", (salt, S["hash_password"](password, salt), row["id"]))
        c.execute("DELETE FROM admin_sessions WHERE admin_id = ?", (row["id"],))
        c.commit()
        print("The password of %s is changed." % email)
    else:
        create_admin(email, password, "server")
        print("Admin %s is created." % email)
    log("server", "admin.cli", email)
    return 0


def current_admin(h):
    token = h.cookie("lh_admin")
    if not token:
        return None
    return db().execute(
        "SELECT a.* FROM admin_sessions s JOIN admins a ON a.id = s.admin_id WHERE s.token_hash = ? AND s.expires > ?",
        (S["hash_token"](token), now()),
    ).fetchone()


def cookie_header(h, token, max_age):
    flags = "Path=/; HttpOnly; SameSite=Strict; Max-Age=%d" % max_age
    if h.is_https():
        flags += "; Secure"
    return "lh_admin=%s; %s" % (token, flags)


def public_admin(row):
    return {"id": row["id"], "email": row["email"], "created": row["created"], "lastLogin": row["last_login"], "createdBy": row["created_by"]}


def api_login(h):
    ip = h.client_ip()
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    email = str(data.get("email", "")).strip().lower()
    password = str(data.get("password", ""))
    if not S["rate_ok"]("admlogin:ip:" + ip, 12, 600) or not S["rate_ok"]("admlogin:" + email, 6, 600):
        return h.fail(429, "Too many tries. Wait a few minutes.")
    c = db()
    row = c.execute("SELECT * FROM admins WHERE email = ?", (email,)).fetchone()
    good = False
    if row:
        good = hmac.compare_digest(S["hash_password"](password, bytes(row["salt"])), bytes(row["pw"]))
    else:
        S["hash_password"](password, b"0" * 16)         # the same time for a wrong e-mail
    if not good:
        log(email[:80] or "?", "login.failed", ip)
        return h.fail(401, "Wrong e-mail or password.")
    token = secrets.token_urlsafe(32)
    t = now()
    c.execute("DELETE FROM admin_sessions WHERE expires < ?", (t,))
    c.execute("INSERT INTO admin_sessions (token_hash, admin_id, created, expires, ip) VALUES (?, ?, ?, ?, ?)",
              (S["hash_token"](token), row["id"], t, t + ADMIN_SESSION_DAYS * 86400, ip))
    c.execute("UPDATE admins SET last_login = ? WHERE id = ?", (t, row["id"]))
    c.commit()
    log(row["email"], "login", ip)
    h.send_json(200, {"ok": True, "admin": public_admin(row)}, {"Set-Cookie": cookie_header(h, token, ADMIN_SESSION_DAYS * 86400)})


def api_logout(h, adm):
    token = h.cookie("lh_admin")
    if token:
        c = db()
        c.execute("DELETE FROM admin_sessions WHERE token_hash = ?", (S["hash_token"](token),))
        c.commit()
    h.send_json(200, {"ok": True}, {"Set-Cookie": cookie_header(h, "", 0)})


def api_admins(h, adm):
    rows = db().execute("SELECT * FROM admins ORDER BY id").fetchall()
    ok(h, admins=[public_admin(r) for r in rows], me=adm["id"])


def api_admin_create(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    email = str(data.get("email", "")).strip().lower()
    password = str(data.get("password", ""))
    if not EMAIL_RE.match(email):
        return h.fail(400, "Write a real e-mail address.")
    if len(password) < 8 or len(password) > 64:
        return h.fail(400, "Password: 8 to 64 characters.")
    if db().execute("SELECT 1 FROM admins WHERE email = ?", (email,)).fetchone():
        return h.fail(409, "This admin exists already.")
    create_admin(email, password, adm["email"])
    log(adm["email"], "admin.create", email)
    ok(h)


def api_admin_delete(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    try:
        target = int(data.get("id"))
    except (TypeError, ValueError):
        return h.fail(400, "Bad request.")
    c = db()
    row = c.execute("SELECT * FROM admins WHERE id = ?", (target,)).fetchone()
    if not row:
        return h.fail(404, "No such admin.")
    if row["id"] == adm["id"]:
        return h.fail(400, "You can not remove yourself.")
    if c.execute("SELECT COUNT(*) AS n FROM admins").fetchone()["n"] <= 1:
        return h.fail(400, "The last admin can not be removed.")
    c.execute("DELETE FROM admins WHERE id = ?", (target,))
    c.commit()
    log(adm["email"], "admin.delete", row["email"])
    ok(h)


def api_admin_password(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    current = str(data.get("current", ""))
    new = str(data.get("password", ""))
    if len(new) < 8 or len(new) > 64:
        return h.fail(400, "Password: 8 to 64 characters.")
    if not hmac.compare_digest(S["hash_password"](current, bytes(adm["salt"])), bytes(adm["pw"])):
        return h.fail(403, "The current password is wrong.")
    salt = secrets.token_bytes(16)
    c = db()
    c.execute("UPDATE admins SET salt = ?, pw = ? WHERE id = ?", (salt, S["hash_password"](new, salt), adm["id"]))
    keep = S["hash_token"](h.cookie("lh_admin") or "")
    c.execute("DELETE FROM admin_sessions WHERE admin_id = ? AND token_hash != ?", (adm["id"], keep))
    c.commit()
    log(adm["email"], "password.change")
    ok(h)


def api_log(h, adm):
    rows = db().execute("SELECT * FROM admin_log ORDER BY id DESC LIMIT 200").fetchall()
    ok(h, log=[{"at": r["at"], "admin": r["admin"], "action": r["action"], "detail": r["detail"]} for r in rows])


# ----------------------------------------------------------------------------------------------- the numbers (charts)
def day_of(t):
    return (t + LOCAL_UTC_OFFSET) // 86400


def day_label(d):
    return time.strftime("%Y-%m-%d", time.gmtime(d * 86400))


def api_overview(h, adm, query):
    try:
        days = max(7, min(180, int(query.get("days", ["30"])[0])))
    except ValueError:
        days = 30
    c = db()
    t = now()
    today = day_of(t)
    first = today - days + 1
    start = first * 86400 - LOCAL_UTC_OFFSET

    def per_day(sql, params=()):
        out = {}
        for r in c.execute(sql, params).fetchall():
            out[r[0]] = r[1]
        return out

    signups = per_day("SELECT (created + ?) / 86400 AS d, COUNT(*) FROM agents WHERE created >= ? GROUP BY d", (LOCAL_UTC_OFFSET, start))
    runs = per_day("SELECT (created + ?) / 86400 AS d, COUNT(*) FROM runs WHERE created >= ? GROUP BY d", (LOCAL_UTC_OFFSET, start))
    souls = per_day("SELECT (created + ?) / 86400 AS d, COALESCE(SUM(captures), 0) FROM runs WHERE created >= ? GROUP BY d", (LOCAL_UTC_OFFSET, start))
    active = per_day(
        "SELECT d, COUNT(DISTINCT a) FROM ("
        " SELECT (at + ?) / 86400 AS d, agent_id AS a FROM logins WHERE at >= ?"
        " UNION ALL SELECT (created + ?) / 86400, agent_id FROM runs WHERE created >= ?) GROUP BY d",
        (LOCAL_UTC_OFFSET, start, LOCAL_UTC_OFFSET, start))
    before = c.execute("SELECT COUNT(*) AS n FROM agents WHERE created < ?", (start,)).fetchone()["n"]

    series = []
    total = before
    for d in range(first, today + 1):
        total += signups.get(d, 0)
        series.append({"day": day_label(d), "signups": signups.get(d, 0), "total": total, "active": active.get(d, 0),
                       "runs": runs.get(d, 0), "souls": souls.get(d, 0)})

    one = lambda sql, p=(): c.execute(sql, p).fetchone()[0]
    today_start = today * 86400 - LOCAL_UTC_OFFSET
    kpi = {
        "agents": one("SELECT COUNT(*) FROM agents"),
        "newToday": one("SELECT COUNT(*) FROM agents WHERE created >= ?", (today_start,)),
        "new7": one("SELECT COUNT(*) FROM agents WHERE created >= ?", (t - 7 * 86400,)),
        "active24": one("SELECT COUNT(*) FROM agents WHERE last_login >= ?", (t - 86400,)),
        "active7": one("SELECT COUNT(*) FROM agents WHERE last_login >= ?", (t - 7 * 86400,)),
        "playing": one("SELECT COUNT(*) FROM agents WHERE runs > 0"),
        "blocked": one("SELECT COUNT(*) FROM agents WHERE banned = 1"),
        "runs": one("SELECT COUNT(*) FROM runs"),
        "runs7": one("SELECT COUNT(*) FROM runs WHERE created >= ?", (t - 7 * 86400,)),
        "souls": one("SELECT COALESCE(SUM(captures), 0) FROM runs"),
        "cages": one("SELECT COALESCE(SUM(cages), 0) FROM runs"),
        "topScore": one("SELECT COALESCE(MAX(best_score), 0) FROM agents"),
        "avgScore": round(one("SELECT COALESCE(AVG(best_score), 0) FROM agents WHERE best_score > 0")),
        "saves": one("SELECT COUNT(*) FROM saves"),
        "admins": one("SELECT COUNT(*) FROM admins"),
    }

    # score ranges (the best score of each agent)
    edges = [(0, 0, "No score"), (1, 999, "1 - 999"), (1000, 2499, "1,000 - 2,499"), (2500, 4999, "2,500 - 4,999"),
             (5000, 9999, "5,000 - 9,999"), (10000, 10 ** 9, "10,000 +")]
    ranges = [{"label": lab, "count": one("SELECT COUNT(*) FROM agents WHERE best_score BETWEEN ? AND ?", (lo, hi))} for lo, hi, lab in edges]

    # hours of the day (Myanmar time) and devices, from the sign ins of the last 30 days
    hours = [0] * 24
    for r in c.execute("SELECT ((at + ?) % 86400) / 3600 AS hr, COUNT(*) FROM logins WHERE at >= ? GROUP BY hr", (LOCAL_UTC_OFFSET, t - 30 * 86400)).fetchall():
        hours[int(r[0]) % 24] = r[1]
    devices = {"mobile": 0, "desktop": 0}
    for r in c.execute("SELECT device, COUNT(DISTINCT agent_id) FROM logins WHERE at >= ? GROUP BY device", (t - 30 * 86400,)).fetchall():
        if r[0] in devices:
            devices[r[0]] = r[1]

    top = [{"agentId": S["agent_code"](r["id"]), "codename": r["codename"], "score": r["best_score"], "nights": r["best_nights"], "souls": r["best_captures"]}
           for r in c.execute("SELECT * FROM agents WHERE best_score > 0 ORDER BY best_score DESC, best_at ASC LIMIT 10").fetchall()]
    recent = [{"agentId": S["agent_code"](r["id"]), "codename": r["codename"], "created": r["created"], "lastLogin": r["last_login"]}
              for r in c.execute("SELECT * FROM agents ORDER BY id DESC LIMIT 8").fetchall()]

    genders = {"male": one("SELECT COUNT(*) FROM agents WHERE gender = 'male'"),
               "female": one("SELECT COUNT(*) FROM agents WHERE gender = 'female'"),
               "unset": one("SELECT COUNT(*) FROM agents WHERE gender NOT IN ('male', 'female')")}

    ok(h, days=days, kpi=kpi, series=series, ranges=ranges, hours=hours, devices=devices, genders=genders, top=top, recent=recent, tz="UTC+6:30")


# ----------------------------------------------------------------------------------------------- the agents (accounts)
USER_SORTS = {"created": "id", "last": "last_login", "score": "best_score", "runs": "runs", "name": "codename COLLATE NOCASE"}


def user_row(r):
    return {"id": r["id"], "agentId": S["agent_code"](r["id"]), "codename": r["codename"], "created": r["created"], "lastLogin": r["last_login"],
            "score": r["best_score"], "souls": r["best_captures"], "cages": r["best_cages"], "nights": r["best_nights"], "runs": r["runs"],
            "blocked": bool(r["banned"]), "gender": r["gender"]}


def users_query(query):
    q = query.get("q", [""])[0].strip()
    flt = query.get("filter", ["all"])[0]
    where, params = [], []
    if q:
        like = "%" + q.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_") + "%"
        m = re.match(r"^AGT-?0*(\d+)$", q, re.I)
        if m:
            where.append("(id = ? OR codename LIKE ? ESCAPE '\\')")
            params += [int(m.group(1)), like]
        else:
            where.append("codename LIKE ? ESCAPE '\\'")
            params.append(like)
    if flt == "blocked":
        where.append("banned = 1")
    elif flt == "scored":
        where.append("best_score > 0")
    elif flt == "noscore":
        where.append("best_score = 0")
    elif flt == "new7":
        where.append("created >= ?")
        params.append(now() - 7 * 86400)
    sort = USER_SORTS.get(query.get("sort", ["created"])[0], "id")
    direction = "ASC" if query.get("dir", ["desc"])[0] == "asc" else "DESC"
    return (" WHERE " + " AND ".join(where)) if where else "", params, " ORDER BY %s %s, id DESC" % (sort, direction)


def api_users(h, adm, query):
    where, params, order = users_query(query)
    try:
        page = max(1, int(query.get("page", ["1"])[0]))
    except ValueError:
        page = 1
    size = 25
    c = db()
    total = c.execute("SELECT COUNT(*) FROM agents" + where, params).fetchone()[0]
    rows = c.execute("SELECT * FROM agents" + where + order + " LIMIT ? OFFSET ?", params + [size, (page - 1) * size]).fetchall()
    ok(h, total=total, page=page, pages=max(1, (total + size - 1) // size), users=[user_row(r) for r in rows])


def api_users_csv(h, adm, query):
    where, params, order = users_query(query)
    out = io.StringIO()
    w = csv.writer(out)
    w.writerow(["agent_id", "codename", "gender", "created_utc", "last_login_utc", "best_score", "souls", "cages", "nights", "runs", "blocked"])
    fmt = lambda t: time.strftime("%Y-%m-%d %H:%M:%S", time.gmtime(t)) if t else ""
    for r in db().execute("SELECT * FROM agents" + where + order, params).fetchall():
        w.writerow([S["agent_code"](r["id"]), r["codename"], r["gender"], fmt(r["created"]), fmt(r["last_login"]), r["best_score"],
                    r["best_captures"], r["best_cages"], r["best_nights"], r["runs"], r["banned"]])
    log(adm["email"], "users.export")
    h.send_bytes(200, ("\ufeff" + out.getvalue()).encode("utf-8"), "text/csv; charset=utf-8",
                 {"Content-Disposition": 'attachment; filename="leikpyar-agents.csv"', "Cache-Control": "no-store"})


def agent_from(h, data):
    try:
        agent_id = int(data.get("id"))
    except (TypeError, ValueError):
        return None
    return db().execute("SELECT * FROM agents WHERE id = ?", (agent_id,)).fetchone()


def api_user_block(h, adm):
    data = h.read_json()
    row = agent_from(h, data or {})
    if not row:
        return h.fail(404, "No such agent.")
    blocked = 1 if data.get("blocked") else 0
    c = db()
    c.execute("UPDATE agents SET banned = ? WHERE id = ?", (blocked, row["id"]))
    if blocked:
        c.execute("DELETE FROM sessions WHERE agent_id = ?", (row["id"],))
    c.commit()
    log(adm["email"], "agent.block" if blocked else "agent.unblock", "%s %s" % (S["agent_code"](row["id"]), row["codename"]))
    ok(h)


def api_user_signout(h, adm):
    data = h.read_json()
    row = agent_from(h, data or {})
    if not row:
        return h.fail(404, "No such agent.")
    c = db()
    c.execute("DELETE FROM sessions WHERE agent_id = ?", (row["id"],))
    c.commit()
    log(adm["email"], "agent.signout", "%s %s" % (S["agent_code"](row["id"]), row["codename"]))
    ok(h)


def api_user_password(h, adm):
    data = h.read_json()
    row = agent_from(h, data or {})
    if not row:
        return h.fail(404, "No such agent.")
    password = str(data.get("password", ""))
    if len(password) < 6 or len(password) > 64:
        return h.fail(400, "Password: 6 to 64 characters.")
    salt = secrets.token_bytes(16)
    c = db()
    c.execute("UPDATE agents SET salt = ?, pw = ? WHERE id = ?", (salt, S["hash_password"](password, salt), row["id"]))
    c.execute("DELETE FROM sessions WHERE agent_id = ?", (row["id"],))
    c.commit()
    log(adm["email"], "agent.password", "%s %s" % (S["agent_code"](row["id"]), row["codename"]))
    ok(h)


def api_user_delete(h, adm):
    data = h.read_json()
    row = agent_from(h, data or {})
    if not row:
        return h.fail(404, "No such agent.")
    c = db()
    c.execute("DELETE FROM logins WHERE agent_id = ?", (row["id"],))
    c.execute("DELETE FROM agents WHERE id = ?", (row["id"],))
    c.commit()
    log(adm["email"], "agent.delete", "%s %s" % (S["agent_code"](row["id"]), row["codename"]))
    ok(h)


# ----------------------------------------------------------------------------------------------- the texts and pictures of the site
class _Source(HTMLParser):
    """Reads site/index.html the way js/i18n.js reads the page: every text node and the texts of some attributes."""
    VOID = {"img", "br", "meta", "link", "input", "hr", "source", "area", "base", "col", "embed", "track", "wbr"}
    SKIP = {"script", "style", "svg", "noscript", "textarea", "code", "model-viewer", "canvas"}

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.stack = []
        self.section = "Page title and description"
        self.items = []        # [key, kind, section]
        self.images = []       # [src, alt, section]
        self.in_title = False

    def skipping(self):
        return any(skip for _, skip in self.stack)

    def attr_items(self, tag, attrs):
        d = {k: (v or "") for k, v in attrs}
        if "data-no-i18n" in d:
            return d
        for name in TEXT_ATTRS:
            if d.get(name) and norm(d[name]):
                self.items.append([norm(d[name]), "attr", self.section])
        if tag == "meta" and d.get("name") == "description" and d.get("content"):
            self.items.append([norm(d["content"]), "meta", "Page title and description"])
        return d

    def handle_starttag(self, tag, attrs):
        d = dict((k, v or "") for k, v in attrs)
        if tag == "section" and d.get("id"):
            self.section = SECTION_NAMES.get(d["id"], d["id"].title())
        elif tag == "header" and "top" in d.get("class", "").split():
            self.section = "Menu and header"
        elif tag == "footer" and "foot" in d.get("class", "").split():
            self.section = "Footer"
        skip = (tag in self.SKIP) or ("data-no-i18n" in d)
        if not self.skipping() and not skip:
            self.attr_items(tag, attrs)
            if tag == "img" and d.get("src"):
                self.images.append([d["src"].split("?")[0], d.get("alt", ""), self.section])
            if tag == "link" and d.get("href") and re.search(r"icon", d.get("rel", "")):
                self.images.append([d["href"].split("?")[0], "Icon of the site", "Page title and description"])
        if tag == "title":
            self.in_title = True
        if tag not in self.VOID:
            self.stack.append((tag, skip))

    def handle_startendtag(self, tag, attrs):
        d = dict((k, v or "") for k, v in attrs)
        if not self.skipping() and tag not in self.SKIP:
            self.attr_items(tag, attrs)
            if tag == "img" and d.get("src"):
                self.images.append([d["src"].split("?")[0], d.get("alt", ""), self.section])

    def handle_endtag(self, tag):
        if tag == "title":
            self.in_title = False
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                del self.stack[i:]
                break

    def handle_data(self, data):
        text = norm(data)
        if not text or self.skipping():
            return
        if self.in_title:
            self.items.append([text, "title", "Page title and description"])
            return
        if self.stack and self.stack[-1][0] == "kbd" and len(text) <= 2:
            return
        if not re.search(r"[A-Za-z\u1000-\u109f]", text):
            return
        self.items.append([text, "text", self.section])


_source_cache = {"mtime": None, "data": None}
PAGE_FILES = ("index.html", "privacy.html")


def read_source():
    """(texts, images): the English texts of the pages in order, and their pictures. The pages are index.html and privacy.html.
    Cached until a page changes."""
    paths = [os.path.join(S["SITE"], name) for name in PAGE_FILES]
    stamp = tuple(os.stat(p).st_mtime_ns if os.path.exists(p) else 0 for p in paths)
    if _source_cache["mtime"] == stamp:
        return _source_cache["data"]
    texts = {}
    images = {}
    for path in paths:
        if not os.path.exists(path):
            continue
        with open(path, "r", encoding="utf-8") as f:
            html = f.read()
        p = _Source()
        p.feed(html)
        for key, kind, section in p.items:
            if key in texts:
                texts[key]["uses"] += 1
            else:
                texts[key] = {"key": key, "kind": kind, "section": section, "uses": 1, "dynamic": False}
        for src, alt, section in p.images:
            if src in images:
                images[src]["uses"] += 1
            else:
                images[src] = {"key": src, "alt": alt, "section": section, "uses": 1}
    _source_cache["mtime"] = stamp
    _source_cache["data"] = (texts, images)
    return _source_cache["data"]


_dict_cache = {"mtime": None, "data": {}}


def read_dictionary():
    """The Burmese texts that came with the site (js/i18n-my.js): English -> Burmese."""
    path = os.path.join(S["SITE"], "js", "i18n-my.js")
    try:
        mtime = os.stat(path).st_mtime_ns
    except OSError:
        return {}
    if _dict_cache["mtime"] == mtime:
        return _dict_cache["data"]
    data = {}
    try:
        with open(path, "r", encoding="utf-8") as f:
            text = f.read()
        start = text.index("{", text.index("window.LH_MY"))        # the comment above the object has {0} {1} in it
        body = text[start: text.rindex("}") + 1]
        body = re.sub(r",\s*}", "}", body)
        data = json.loads(body)
    except (OSError, ValueError):
        data = {}
    _dict_cache["mtime"] = mtime
    _dict_cache["data"] = data
    return data


def stored(kind):
    out = {}
    for r in db().execute("SELECT key, value FROM content WHERE kind = ?", (kind,)).fetchall():
        try:
            out[r["key"]] = json.loads(r["value"])
        except ValueError:
            pass
    return out


def invalidate():
    _cache["payload"] = None


def api_texts(h, adm):
    texts, _ = read_source()
    dictionary = read_dictionary()
    saved = stored("text")
    items = []
    for key, t in texts.items():
        items.append(dict(t))
    for key in dictionary:                    # the messages the page makes itself (scores, errors ...)
        if key not in texts:
            items.append({"key": key, "kind": "text", "section": "Messages the page shows", "uses": 0, "dynamic": True})
    for it in items:
        s = saved.get(it["key"], {})
        it["defMy"] = dictionary.get(it["key"], "")
        it["en"] = s.get("en") or ""
        it["my"] = s["my"] if "my" in s and s["my"] is not None else it["defMy"]
        it["edited"] = bool(s)
        it["template"] = bool(re.search(r"\{\d+\}", it["key"]))
    ok(h, items=items)


def clean_text(value, limit):
    value = str(value if value is not None else "").replace("\r", "")
    value = value.strip()
    return value[:limit]


def api_texts_save(h, adm):
    data = h.read_json(limit=400 * 1024)
    if data is None or not isinstance(data.get("items"), list):
        return h.fail(400, "Bad request.")
    texts, _ = read_source()
    dictionary = read_dictionary()
    c = db()
    changed = 0
    for it in data["items"][:500]:
        if not isinstance(it, dict):
            continue
        key = str(it.get("key", ""))
        if key not in texts and key not in dictionary:
            continue
        en = norm(clean_text(it.get("en"), 2000))
        my = clean_text(it.get("my"), 4000)
        value = {}
        if en and en != key and not re.search(r"\{\d+\}", key):
            value["en"] = en
        if my != dictionary.get(key, ""):
            value["my"] = my                      # "" = no Burmese for this text (it stays English)
        if value:
            c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES ('text', ?, ?, ?, ?) "
                      "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
                      (key, json.dumps(value, ensure_ascii=False), now(), adm["email"]))
        else:
            c.execute("DELETE FROM content WHERE kind = 'text' AND key = ?", (key,))
        changed += 1
    c.commit()
    invalidate()
    log(adm["email"], "texts.save", "%d texts" % changed)
    ok(h, saved=changed)


def api_texts_reset(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    c = db()
    if data.get("all"):
        c.execute("DELETE FROM content WHERE kind = 'text'")
        what = "all texts"
    else:
        c.execute("DELETE FROM content WHERE kind = 'text' AND key = ?", (str(data.get("key", "")),))
        what = str(data.get("key", ""))[:60]
    c.commit()
    invalidate()
    log(adm["email"], "texts.reset", what)
    ok(h)


# -- the pictures
UPLOAD_DIR = lambda: os.path.join(S["DATA"], "uploads")
IMAGE_LIMIT = 700 * 1024


def image_kind(blob):
    if blob[:8] == b"\x89PNG\r\n\x1a\n":
        return "png"
    if blob[:3] == b"\xff\xd8\xff":
        return "jpg"
    if blob[:4] == b"RIFF" and blob[8:12] == b"WEBP":
        return "webp"
    if blob[:4] == b"\x00\x00\x01\x00":
        return "ico"
    return None


def api_images(h, adm):
    _, images = read_source()
    saved = stored("image")
    items = []
    for key, im in images.items():
        d = dict(im)
        d["custom"] = ("/uploads/" + saved[key]["file"]) if key in saved and saved[key].get("file") else ""
        items.append(d)
    ok(h, images=items)


CREW_SLOTS = ("game-designer", "sound", "developer", "artist")


def api_crew_public(h):
    """The photos of the four creators that the admins uploaded (the game shows them in its credits). No sign in: it is what the website shows.
    photo = "" means no photo was uploaded: the game keeps its own picture."""
    images = content_payload()["images"]
    crew = [{"slot": s, "photo": images.get("/img/crew/%s.webp" % s, "")} for s in CREW_SLOTS]
    h.send_json(200, {"ok": True, "crew": crew}, {"Cache-Control": "no-cache"})


def store_upload(data, allow_ico=False):
    """Saves a picture the admin sent (base64) in data/uploads. Returns (file name, None) or (None, error)."""
    try:
        blob = base64.b64decode(str(data or ""), validate=True)
    except (ValueError, TypeError):
        return None, "Bad picture."
    if not blob or len(blob) > IMAGE_LIMIT:
        return None, "The picture is too big (700 KB at most)."
    kind = image_kind(blob)
    if kind is None or (kind == "ico" and not allow_ico):
        return None, "Use a PNG, JPG or WebP picture."
    name = hashlib.sha1(blob).hexdigest()[:12] + "." + kind
    os.makedirs(UPLOAD_DIR(), exist_ok=True)
    with open(os.path.join(UPLOAD_DIR(), name), "wb") as f:
        f.write(blob)
    return name, None


AUDIO_LIMIT = 10 * 1024 * 1024      # one sound file (an MP3 of a few minutes)
AUDIO_URL = re.compile(r"^/uploads/[a-f0-9]{12}\.(mp3|ogg|wav|m4a)$")


def audio_kind(blob):
    if blob[:3] == b"ID3" or (len(blob) > 2 and blob[0] == 0xFF and (blob[1] & 0xE0) == 0xE0):
        return "mp3"
    if blob[:4] == b"OggS":
        return "ogg"
    if blob[:4] == b"RIFF" and blob[8:12] == b"WAVE":
        return "wav"
    if blob[4:8] == b"ftyp":
        return "m4a"
    return None


def store_audio(data):
    """Saves a sound file the admin sent (base64) in data/uploads. Returns (file name, None) or (None, error)."""
    try:
        blob = base64.b64decode(str(data or ""), validate=True)
    except (ValueError, TypeError):
        return None, "Bad sound file."
    if not blob or len(blob) > AUDIO_LIMIT:
        return None, "The sound file is too big (10 MB at most)."
    kind = audio_kind(blob)
    if kind is None:
        return None, "Use an MP3, OGG, WAV or M4A sound file."
    name = hashlib.sha1(blob).hexdigest()[:12] + "." + kind
    os.makedirs(UPLOAD_DIR(), exist_ok=True)
    with open(os.path.join(UPLOAD_DIR(), name), "wb") as f:
        f.write(blob)
    return name, None


def api_image_save(h, adm):
    data = h.read_json(limit=int(IMAGE_LIMIT * 1.4) + 2048)
    if data is None:
        return h.fail(400, "The picture is too big (700 KB at most).")
    _, images = read_source()
    key = str(data.get("key", ""))
    if key not in images:
        return h.fail(404, "No such picture on the site.")
    name, error = store_upload(data.get("data"), allow_ico=key.endswith(".ico"))
    if error:
        return h.fail(400, error)
    c = db()
    c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES ('image', ?, ?, ?, ?) "
              "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
              (key, json.dumps({"file": name}), now(), adm["email"]))
    c.commit()
    invalidate()
    log(adm["email"], "image.save", key)
    ok(h, url="/uploads/" + name)


def api_image_reset(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    c = db()
    c.execute("DELETE FROM content WHERE kind = 'image' AND key = ?", (str(data.get("key", "")),))
    c.commit()
    invalidate()
    log(adm["email"], "image.reset", str(data.get("key", ""))[:80])
    ok(h)


def serve_upload(h, name):
    if not re.match(r"^[a-f0-9]{12}\.(png|jpg|webp|ico|mp3|ogg|wav|m4a)$", name):
        return h.fail(404, "Not found.")
    try:
        with open(os.path.join(UPLOAD_DIR(), name), "rb") as f:
            blob = f.read()
    except OSError:
        return h.fail(404, "Not found.")
    ext = name.rsplit(".", 1)[1]
    ctype = {"png": "image/png", "jpg": "image/jpeg", "webp": "image/webp", "ico": "image/x-icon",
             "mp3": "audio/mpeg", "ogg": "audio/ogg", "wav": "audio/wav", "m4a": "audio/mp4"}[ext]
    headers = {"Cache-Control": S["CACHE_FOREVER"]}
    if ctype.startswith("audio/"):
        headers["Accept-Ranges"] = "bytes"
        m = re.match(r"^bytes=(\d*)-(\d*)$", (h.headers.get("Range") or "").strip())
        if m and (m.group(1) or m.group(2)):
            size = len(blob)
            if m.group(1):
                start = int(m.group(1))
                end = min(int(m.group(2)), size - 1) if m.group(2) else size - 1
            else:
                start = max(0, size - int(m.group(2)))
                end = size - 1
            if start >= size or start > end:
                return h.send_bytes(416, b"", ctype, {"Content-Range": "bytes */%d" % size})
            headers["Content-Range"] = "bytes %d-%d/%d" % (start, end, size)
            return h.send_bytes(206, blob[start:end + 1], ctype, headers)
    h.send_bytes(200, blob, ctype, headers)


# -- more contributors and special thanks: small landscape boxes (a picture, a label, a name, a copyright line) under the four main creators.
# A special thanks box can also hold a list of sounds (SFX) with the artist of each: it opens inside the card on the website.
CREDIT_IMAGE = re.compile(r"^/uploads/[a-f0-9]{12}\.(png|jpg|webp)$")
CREDIT_LIMITS = {"name": 60, "role": 40, "roleMy": 80, "note": 120, "noteMy": 240, "link": 300}
LINK_RE = re.compile(r"^https?://[^\s<>\"']{3,290}$", re.I)       # a web address: only http and https (never javascript:)
SOUND_LIMITS = {"title": 90, "artist": 70}
LISTS = {"credits": False, "thanks": True}          # kind -> can hold sounds


def entries(kind):
    return stored(kind).get("list", {}).get("items", [])


def credits_list():
    return entries("credits")


def creator_links():
    return stored("setting").get("creatorLinks", {}).get("value", {})


def api_credits(h, adm):
    ok(h, credits=entries("credits"), thanks=entries("thanks"), links=creator_links())


def api_creator_links_save(h, adm):
    data = h.read_json(limit=16 * 1024)
    if data is None or not isinstance(data.get("links"), dict):
        return h.fail(400, "Bad request.")
    try:
        with open(os.path.join(S["SITE"], "index.html"), "r", encoding="utf-8") as f:
            slots = set(re.findall(r'data-slot="([a-z0-9\-]{1,40})"', f.read()))
    except OSError:
        slots = set()
    clean = {}
    for slot, url in data["links"].items():
        if slot not in slots:
            continue
        url = str(url or "").strip()
        if not url:
            continue
        if not LINK_RE.match(url):
            return h.fail(400, "A link must start with http:// or https://")
        clean[slot] = url
    c = db()
    if clean:
        c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES ('setting', 'creatorLinks', ?, ?, ?) "
                  "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
                  (json.dumps({"value": clean}), now(), adm["email"]))
    else:
        c.execute("DELETE FROM content WHERE kind = 'setting' AND key = 'creatorLinks'")
    c.commit()
    invalidate()
    log(adm["email"], "creators.links", "%d links" % len(clean))
    ok(h, saved=len(clean))


def save_entries(h, adm, kind):
    data = h.read_json(limit=160 * 1024)
    if data is None or not isinstance(data.get("items"), list):
        return h.fail(400, "Bad request.")
    sounds_ok = LISTS[kind]
    items = []
    for raw in data["items"][:60]:
        if not isinstance(raw, dict):
            continue
        item = {k: norm(clean_text(raw.get(k), n)) for k, n in CREDIT_LIMITS.items()}
        if not item["name"]:
            return h.fail(400, "Every box needs a name.")
        if item["link"] and not LINK_RE.match(item["link"]):
            return h.fail(400, "A link must start with http:// or https://")
        image = str(raw.get("image") or "")
        if image and (not CREDIT_IMAGE.match(image) or not os.path.isfile(os.path.join(UPLOAD_DIR(), image[len("/uploads/"):]))):
            return h.fail(400, "A picture is missing. Choose it again.")
        item["image"] = image
        if sounds_ok:
            sounds = []
            for snd in (raw.get("sounds") if isinstance(raw.get("sounds"), list) else [])[:300]:
                if not isinstance(snd, dict):
                    continue
                one = {k: norm(clean_text(snd.get(k), n)) for k, n in SOUND_LIMITS.items()}
                f = str(snd.get("file") or "")
                if f:
                    if not AUDIO_URL.match(f) or not os.path.isfile(os.path.join(UPLOAD_DIR(), f[len("/uploads/"):])):
                        return h.fail(400, "A sound file is missing. Upload it again.")
                    one["file"] = f
                if one["title"]:
                    sounds.append(one)
            item["sounds"] = sounds
        items.append(item)
    c = db()
    if items:
        c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES (?, 'list', ?, ?, ?) "
                  "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
                  (kind, json.dumps({"items": items}, ensure_ascii=False), now(), adm["email"]))
    else:
        c.execute("DELETE FROM content WHERE kind = ?", (kind,))
    c.commit()
    invalidate()
    log(adm["email"], kind + ".save", "%d boxes" % len(items))
    ok(h, saved=len(items))


def api_credits_save(h, adm):
    save_entries(h, adm, "credits")


def api_thanks_save(h, adm):
    save_entries(h, adm, "thanks")


def api_credit_audio(h, adm):
    data = h.read_json(limit=int(AUDIO_LIMIT * 1.4) + 2048)
    if data is None:
        return h.fail(400, "The sound file is too big (10 MB at most).")
    name, error = store_audio(data.get("data"))
    if error:
        return h.fail(400, error)
    ok(h, url="/uploads/" + name)


def api_credit_image(h, adm):
    data = h.read_json(limit=int(IMAGE_LIMIT * 1.4) + 2048)
    if data is None:
        return h.fail(400, "The picture is too big (700 KB at most).")
    name, error = store_upload(data.get("data"))
    if error:
        return h.fail(400, error)
    ok(h, url="/uploads/" + name)


# -- the pages (Privacy) and the settings
PAGES = ("privacy",)


def api_pages(h, adm):
    saved = stored("page")
    settings = stored("setting")
    ok(h, pages={p: {"en": saved.get(p, {}).get("en", ""), "my": saved.get(p, {}).get("my", "")} for p in PAGES},
       settings={"contactEmail": settings.get("contactEmail", {}).get("value", "")})


def api_page_save(h, adm):
    data = h.read_json(limit=120 * 1024)
    if data is None:
        return h.fail(400, "Bad request.")
    name = str(data.get("page", ""))
    if name not in PAGES:
        return h.fail(404, "No such page.")
    en = clean_text(data.get("en"), 30000)
    my = clean_text(data.get("my"), 40000)
    c = db()
    if en:
        c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES ('page', ?, ?, ?, ?) "
                  "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
                  (name, json.dumps({"en": en, "my": my}, ensure_ascii=False), now(), adm["email"]))
    else:
        c.execute("DELETE FROM content WHERE kind = 'page' AND key = ?", (name,))     # empty = the text that came with the site
    c.commit()
    invalidate()
    log(adm["email"], "page.save", name)
    ok(h)


def api_settings_save(h, adm):
    data = h.read_json()
    if data is None:
        return h.fail(400, "Bad request.")
    email = str(data.get("contactEmail", "")).strip()
    if email and not EMAIL_RE.match(email):
        return h.fail(400, "Write a real e-mail address, or leave it empty.")
    c = db()
    if email:
        c.execute("INSERT INTO content (kind, key, value, updated, by) VALUES ('setting', 'contactEmail', ?, ?, ?) "
                  "ON CONFLICT(kind, key) DO UPDATE SET value = excluded.value, updated = excluded.updated, by = excluded.by",
                  (json.dumps({"value": email}), now(), adm["email"]))
    else:
        c.execute("DELETE FROM content WHERE kind = 'setting' AND key = 'contactEmail'")
    c.commit()
    invalidate()
    log(adm["email"], "settings.save", "contact e-mail")
    ok(h)


# ----------------------------------------------------------------------------------------------- the website with the edits
def content_payload():
    """What goes into the page: {v, texts: {English: {en, my}}, images: {path: url}, pages: {privacy: {en, my}}, settings}."""
    if _cache["payload"] is not None:
        return _cache["payload"]
    texts = stored("text")
    images = {k: "/uploads/" + v["file"] for k, v in stored("image").items() if v.get("file")}
    pages = stored("page")
    settings = {k: v.get("value", "") for k, v in stored("setting").items()}
    credits = entries("credits")
    thanks = entries("thanks")
    version = hashlib.sha1(json.dumps([texts, images, pages, settings, credits, thanks], sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()[:10]
    payload = {"v": version, "texts": texts, "images": images, "pages": pages, "settings": settings, "credits": credits, "thanks": thanks}
    _cache["payload"] = payload
    return payload


def serve_index(h):
    serve_html(h, "index.html")


def serve_html(h, name):
    """A page of the site (index.html, privacy.html) with the texts, the pictures and the pages the admins edited put in."""
    path = os.path.join(S["SITE"], name)
    try:
        st = os.stat(path)
        with open(path, "r", encoding="utf-8") as f:
            html = f.read()
    except OSError:
        return h.fail(404, "Not found.")
    payload = content_payload()
    has = bool(payload["texts"] or payload["images"] or payload["pages"] or payload["settings"] or payload["credits"] or payload["thanks"])
    etag = 'W/"%x-%x-%s"' % (st.st_mtime_ns, st.st_size, payload["v"] if has else "0")
    if h.headers.get("If-None-Match") == etag:
        h.send_response(304)
        h.send_header("ETag", etag)
        h.send_header("Cache-Control", "no-cache")
        h.end_headers()
        return
    if has:
        for key, url in payload["images"].items():
            html = re.sub(r'((?:src|href)=")' + re.escape(key) + r'(?:\?[^"]*)?(")', lambda m: m.group(1) + url + m.group(2), html)
        blob = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
        tag = "<script>window.LH_CONTENT=" + blob + ";</script>\n"
        marker = '<script src="/js/i18n-my.js'
        html = html.replace(marker, tag + marker, 1) if marker in html else html.replace("</body>", tag + "</body>", 1)
    body = html.encode("utf-8")
    headers = {"ETag": etag, "Cache-Control": "no-cache", "Vary": "Accept-Encoding"}
    if "gzip" in h.headers.get("Accept-Encoding", ""):
        body = gzip.compress(body, 6)
        headers["Content-Encoding"] = "gzip"
    h.send_bytes(200, body, "text/html; charset=utf-8", headers)


# ----------------------------------------------------------------------------------------------- routing
def handle(h, method, api, query):
    """api = what comes after /api/admin/. Returns after it has answered."""
    if method == "HEAD":
        method = "GET"
    if api == "login" and method == "POST":
        return api_login(h)
    adm = current_admin(h)
    if api == "me" and method == "GET":
        return ok(h, admin=public_admin(adm) if adm else None)
    if adm is None:
        return h.fail(401, "Sign in first.")
    if method == "POST" and h.headers.get("X-LH-Admin") != "1":
        return h.fail(403, "Not allowed.")

    if method == "GET":
        routes = {
            "overview": lambda: api_overview(h, adm, query),
            "users": lambda: api_users(h, adm, query),
            "users.csv": lambda: api_users_csv(h, adm, query),
            "texts": lambda: api_texts(h, adm),
            "images": lambda: api_images(h, adm),
            "pages": lambda: api_pages(h, adm),
            "credits": lambda: api_credits(h, adm),
            "admins": lambda: api_admins(h, adm),
            "log": lambda: api_log(h, adm),
        }
    else:
        routes = {
            "logout": lambda: api_logout(h, adm),
            "users/block": lambda: api_user_block(h, adm),
            "users/signout": lambda: api_user_signout(h, adm),
            "users/password": lambda: api_user_password(h, adm),
            "users/delete": lambda: api_user_delete(h, adm),
            "texts": lambda: api_texts_save(h, adm),
            "texts/reset": lambda: api_texts_reset(h, adm),
            "images": lambda: api_image_save(h, adm),
            "images/reset": lambda: api_image_reset(h, adm),
            "pages": lambda: api_page_save(h, adm),
            "credits": lambda: api_credits_save(h, adm),
            "thanks": lambda: api_thanks_save(h, adm),
            "creators/links": lambda: api_creator_links_save(h, adm),
            "credits/image": lambda: api_credit_image(h, adm),
            "credits/audio": lambda: api_credit_audio(h, adm),
            "settings": lambda: api_settings_save(h, adm),
            "admins": lambda: api_admin_create(h, adm),
            "admins/delete": lambda: api_admin_delete(h, adm),
            "password": lambda: api_admin_password(h, adm),
        }
    fn = routes.get(api)
    if fn is None:
        return h.fail(404, "Unknown address.")
    return fn()
