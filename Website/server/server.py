#!/usr/bin/env python3
"""
Leikpyar Hunters - the web server of the game and the website (Python standard library only).

  /                 the website (site/)
  /play/            the Unity WebGL game (game/)
  /api/...          accounts (Agent ID), scores, the leaderboard, the protected 3D models

Run:   python3 server.py                 (port 8000, data in ./data)
Env:   LORDS_ROOT   folder with site/ game/ models/ data/   (default: the folder above this file)
       LORDS_PORT   (default 8000)
       LORDS_SECRET (a long random text; written to data/secret.key the first time if not set)
"""
import base64
import gzip
import hashlib
import hmac
import io
import json
import mimetypes
import os
import re
import secrets
import sqlite3
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, unquote, urlparse

import admin      # the admin panel (admin.py, next to this file)

ROOT = os.environ.get("LORDS_ROOT") or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SITE = os.path.join(ROOT, "site")
GAME = os.path.join(ROOT, "game")
MODELS = os.path.join(ROOT, "models")
DATA = os.path.join(ROOT, "data")
PORT = int(os.environ.get("LORDS_PORT", "8000"))
os.makedirs(DATA, exist_ok=True)

mimetypes.add_type("application/wasm", ".wasm")
mimetypes.add_type("application/javascript", ".js")
mimetypes.add_type("font/woff2", ".woff2")
mimetypes.add_type("font/woff", ".woff")
mimetypes.add_type("model/gltf-binary", ".glb")

# ----------------------------------------------------------------------------------------------- secret
def load_secret():
    s = os.environ.get("LORDS_SECRET")
    if s:
        return s.encode()
    path = os.path.join(DATA, "secret.key")
    if not os.path.exists(path):
        with open(path, "wb") as f:
            f.write(secrets.token_bytes(48))
        try:
            os.chmod(path, 0o600)
        except OSError:
            pass
    with open(path, "rb") as f:
        return f.read()


SECRET = load_secret()


# ----------------------------------------------------------------------------------------------- reCAPTCHA (against bots that create agents)
# Off until there are keys. The keys come from  https://www.google.com/recaptcha/admin  (type: reCAPTCHA v2, "I'm not a robot" checkbox,
# domain leikpyarhunters.org) and are put in data/recaptcha.json as {"site": "...", "secret": "..."}  (python D:\deploy_tools\set_recaptcha.py does it)
# or in LORDS_RECAPTCHA_SITE / LORDS_RECAPTCHA_SECRET. The site key is public (the page needs it), the secret key stays on the server.
def recaptcha_keys():
    site = os.environ.get("LORDS_RECAPTCHA_SITE", "")
    secret = os.environ.get("LORDS_RECAPTCHA_SECRET", "")
    if not (site and secret):
        try:
            with open(os.path.join(DATA, "recaptcha.json"), "r", encoding="utf-8") as f:
                cfg = json.load(f)
            site, secret = str(cfg.get("site", "")).strip(), str(cfg.get("secret", "")).strip()
        except (OSError, ValueError):
            site = secret = ""
    return site, secret


def verify_captcha(token, ip):
    """True when Google says the answer is a real person's. Any problem = False (no agent is made without the check)."""
    site, secret = recaptcha_keys()
    if not (site and secret):
        return True
    if not token or len(token) > 4096:
        return False
    import urllib.parse
    import urllib.request
    body = urllib.parse.urlencode({"secret": secret, "response": token, "remoteip": ip}).encode()
    try:
        req = urllib.request.Request("https://www.google.com/recaptcha/api/siteverify", data=body, method="POST")
        with urllib.request.urlopen(req, timeout=6) as r:
            return bool(json.loads(r.read().decode("utf-8")).get("success"))
    except Exception as e:
        sys.stderr.write("captcha check failed: %r\n" % (e,))
        return False

# ----------------------------------------------------------------------------------------------- database
DB_PATH = os.path.join(DATA, "lords.db")
_local = threading.local()


def db():
    conn = getattr(_local, "conn", None)
    if conn is None:
        conn = sqlite3.connect(DB_PATH, timeout=15)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("PRAGMA foreign_keys=ON")
        _local.conn = conn
    return conn


def init_db():
    c = db()
    c.executescript(
        """
        CREATE TABLE IF NOT EXISTS agents (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            codename TEXT NOT NULL UNIQUE COLLATE NOCASE,
            salt BLOB NOT NULL,
            pw BLOB NOT NULL,
            created INTEGER NOT NULL,
            last_login INTEGER NOT NULL,
            best_score INTEGER NOT NULL DEFAULT 0,
            best_captures INTEGER NOT NULL DEFAULT 0,
            best_cages INTEGER NOT NULL DEFAULT 0,
            best_nights INTEGER NOT NULL DEFAULT 0,
            best_at INTEGER NOT NULL DEFAULT 0,
            runs INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS sessions (
            token_hash TEXT PRIMARY KEY,
            agent_id INTEGER NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
            created INTEGER NOT NULL,
            expires INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS runs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            agent_id INTEGER NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
            score INTEGER NOT NULL,
            captures INTEGER NOT NULL,
            cages INTEGER NOT NULL,
            nights INTEGER NOT NULL,
            created INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS saves (
            agent_id INTEGER PRIMARY KEY REFERENCES agents(id) ON DELETE CASCADE,
            state TEXT NOT NULL,
            photo BLOB,
            day INTEGER NOT NULL DEFAULT 1,
            hour REAL NOT NULL DEFAULT 20,
            score INTEGER NOT NULL DEFAULT 0,
            saved INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_agents_score ON agents(best_score DESC, best_at ASC);
        CREATE INDEX IF NOT EXISTS idx_sessions_agent ON sessions(agent_id);
        """
    )
    c.commit()
    admin.init_admin_db()


# ----------------------------------------------------------------------------------------------- the rules
SCORE_CAPTURE = 100     # one ghost sealed in the generator
SCORE_CAGE = 500        # one Faraday cage checked by the NPC
SCORE_NIGHT = 250       # one night survived (until dawn)
MAX_CAPTURES, MAX_CAGES, MAX_NIGHTS = 400, 400, 100

SESSION_DAYS = 30
GENDERS = ("male", "female")
NAME_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 _.\-]{2,15}$")


def agent_code(agent_id):
    return "AGT-%06d" % int(agent_id)


def hash_password(password, salt):
    return hashlib.scrypt(password.encode("utf-8"), salt=salt, n=2 ** 14, r=8, p=1, dklen=32)


def hash_token(token):
    return hashlib.sha256(token.encode()).hexdigest()


def compute_score(captures, cages, nights):
    return captures * SCORE_CAPTURE + cages * SCORE_CAGE + nights * SCORE_NIGHT


# ----------------------------------------------------------------------------------------------- rate limit
_rate = {}
_rate_lock = threading.Lock()


def rate_ok(key, limit, per):
    now = time.time()
    with _rate_lock:
        hits = [t for t in _rate.get(key, []) if now - t < per]
        if len(hits) >= limit:
            _rate[key] = hits
            return False
        hits.append(now)
        _rate[key] = hits
        if len(_rate) > 5000:
            for k in list(_rate.keys())[:2500]:
                _rate.pop(k, None)
    return True


# ----------------------------------------------------------------------------------------------- 3D models (protected)
MODEL_META = os.path.join(MODELS, "models.json")


def load_models():
    try:
        with open(MODEL_META, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return []


_model_tokens = {}
_model_lock = threading.Lock()


def make_model_token(model_id, ip, ua):
    token = secrets.token_urlsafe(24)
    key = hmac.new(SECRET, ("model:" + token).encode(), hashlib.sha256).digest()
    with _model_lock:
        now = time.time()
        for k in [k for k, v in _model_tokens.items() if v["exp"] < now]:
            _model_tokens.pop(k, None)
        _model_tokens[token] = {"id": model_id, "ip": ip, "ua": hashlib.sha1(ua.encode()).hexdigest(), "exp": now + 60, "key": key}
    return token, key


def take_model_token(token, model_id, ip, ua):
    with _model_lock:
        entry = _model_tokens.pop(token, None)
    if not entry or entry["exp"] < time.time() or entry["id"] != model_id:
        return None
    if entry["ip"] != ip or entry["ua"] != hashlib.sha1(ua.encode()).hexdigest():
        return None
    return entry["key"]


def xor_bytes(data, key):
    # the model is scrambled with a per-request key: it is no file that can be saved from the network panel as it is
    out = bytearray(data)
    k = len(key)
    for i in range(len(out)):
        out[i] ^= key[i % k]
    return bytes(out)


# ----------------------------------------------------------------------------------------------- the handler
JSON_LIMIT = 8192     # room for the reCAPTCHA answer (about 2 KB)
SAVE_LIMIT = 700 * 1024          # a saved game with its photo
SAVE_STATE_MAX = 60000           # characters of the saved state
SAVE_PHOTO_MAX = 420 * 1024      # bytes of the photo (a small JPEG)
CACHE_FOREVER = "public, max-age=31536000, immutable"


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "LeikpyarHunters"
    sys_version = ""

    # -- helpers ---------------------------------------------------------------------------------
    def log_message(self, fmt, *args):
        if os.environ.get("LORDS_QUIET"):
            return
        sys.stderr.write("%s %s\n" % (self.client_address[0], fmt % args))

    def client_ip(self):
        fwd = self.headers.get("X-Forwarded-For")
        if fwd:
            return fwd.split(",")[0].strip()
        return self.client_address[0]

    def is_https(self):
        return self.headers.get("X-Forwarded-Proto", "").lower() == "https"

    def send_bytes(self, status, body, ctype, headers=None):
        self.send_response(status)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        for k, v in (headers or {}).items():
            self.send_header(k, v)
        self.security_headers()
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    # What a page of the website may load (the game under /play and the API are not pages: no policy there).
    # reCAPTCHA needs google.com and gstatic.com; the 3D viewer makes a worker from a blob.
    CSP = ("default-src 'self'; "
           "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://www.google.com https://www.gstatic.com; "
           "style-src 'self' 'unsafe-inline'; "
           "img-src 'self' data: blob:; media-src 'self' blob:; font-src 'self' data:; "
           "connect-src 'self' blob: data: https://www.gstatic.com; "
           "worker-src 'self' blob:; frame-src https://www.google.com https://recaptcha.google.com; "
           "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'")

    def security_headers(self):
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "same-origin")
        self.send_header("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()")
        if self.is_https():
            self.send_header("Strict-Transport-Security", "max-age=31536000")     # the browser uses https for a year
        path = self.path or ""
        if not (path.startswith("/play") or path.startswith("/api/")):
            self.send_header("Content-Security-Policy", self.CSP)
            self.send_header("X-Frame-Options", "SAMEORIGIN")

    def send_json(self, status, obj, headers=None):
        body = json.dumps(obj, separators=(",", ":")).encode("utf-8")
        h = {"Cache-Control": "no-store"}
        h.update(headers or {})
        self.send_bytes(status, body, "application/json; charset=utf-8", h)

    def fail(self, status, message):
        self.send_json(status, {"ok": False, "error": message})

    def read_json(self, limit=None):
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            length = 0
        if length <= 0 or length > (limit or JSON_LIMIT):
            return None
        try:
            data = json.loads(self.rfile.read(length).decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            return None
        return data if isinstance(data, dict) else None

    def cookie(self, name):
        raw = self.headers.get("Cookie", "")
        for part in raw.split(";"):
            if "=" in part:
                k, v = part.strip().split("=", 1)
                if k == name:
                    return v
        return None

    def session_cookie_header(self, token, max_age):
        flags = "Path=/; HttpOnly; SameSite=Lax; Max-Age=%d" % max_age
        if self.is_https():
            flags += "; Secure"
        return "lh_session=%s; %s" % (token, flags)

    # -- accounts --------------------------------------------------------------------------------
    def current_agent(self):
        token = None
        auth = self.headers.get("Authorization", "")
        if auth.lower().startswith("bearer "):
            token = auth[7:].strip()
        if not token:
            token = self.cookie("lh_session")
        if not token:
            return None
        row = db().execute(
            "SELECT a.* FROM sessions s JOIN agents a ON a.id = s.agent_id WHERE s.token_hash = ? AND s.expires > ? AND a.banned = 0",
            (hash_token(token), int(time.time())),
        ).fetchone()
        return row

    def public_agent(self, row, with_rank=True):
        out = {
            "agentId": agent_code(row["id"]),
            "codename": row["codename"],
            "gender": row["gender"],
            "bestScore": row["best_score"],
            "bestCaptures": row["best_captures"],
            "bestCages": row["best_cages"],
            "bestNights": row["best_nights"],
            "runs": row["runs"],
            "joined": row["created"],
        }
        if with_rank:
            out["rank"] = self.rank_of(row) if row["best_score"] > 0 else None
        return out

    @staticmethod
    def rank_of(row):
        r = db().execute(
            "SELECT COUNT(*) AS n FROM agents WHERE best_score > ? OR (best_score = ? AND best_at < ?)",
            (row["best_score"], row["best_score"], row["best_at"]),
        ).fetchone()
        return r["n"] + 1

    def start_session(self, agent_id):
        token = secrets.token_urlsafe(32)
        now = int(time.time())
        c = db()
        c.execute("DELETE FROM sessions WHERE expires < ?", (now,))
        c.execute(
            "INSERT INTO sessions (token_hash, agent_id, created, expires) VALUES (?, ?, ?, ?)",
            (hash_token(token), agent_id, now, now + SESSION_DAYS * 86400),
        )
        c.commit()
        return token

    def api_register(self):
        if not rate_ok("reg:" + self.client_ip(), 6, 600):
            return self.fail(429, "Too many tries. Wait a few minutes.")
        data = self.read_json()
        if data is None:
            return self.fail(400, "Bad request.")
        name = str(data.get("codename", "")).strip()
        password = str(data.get("password", ""))
        if not NAME_RE.match(name):
            return self.fail(400, "Codename: 3 to 16 letters, numbers, space, dot, dash or underscore.")
        if len(password) < 6 or len(password) > 64:
            return self.fail(400, "Password: 6 to 64 characters.")
        gender = str(data.get("gender", "")).strip().lower()
        if gender not in GENDERS:
            return self.fail(400, "Choose Male or Female.")
        # the robot check (only when the keys are set)
        if all(recaptcha_keys()) and not verify_captcha(str(data.get("captcha", "")), self.client_ip()):
            return self.fail(400, "Please tick \"I'm not a robot\" and try again.")
        c = db()
        if c.execute("SELECT 1 FROM agents WHERE codename = ?", (name,)).fetchone():
            return self.fail(409, "This codename is taken.")
        salt = secrets.token_bytes(16)
        now = int(time.time())
        try:
            cur = c.execute(
                "INSERT INTO agents (codename, salt, pw, created, last_login, gender) VALUES (?, ?, ?, ?, ?, ?)",
                (name, salt, hash_password(password, salt), now, now, gender),
            )
            c.commit()
        except sqlite3.IntegrityError:
            return self.fail(409, "This codename is taken.")
        token = self.start_session(cur.lastrowid)
        admin.record_login(cur.lastrowid, self.headers.get("User-Agent", ""))
        row = c.execute("SELECT * FROM agents WHERE id = ?", (cur.lastrowid,)).fetchone()
        self.send_json(200, {"ok": True, "token": token, "agent": self.public_agent(row)},
                       {"Set-Cookie": self.session_cookie_header(token, SESSION_DAYS * 86400)})

    def api_login(self):
        ip = self.client_ip()
        if not rate_ok("login:" + ip, 12, 600):
            return self.fail(429, "Too many tries. Wait a few minutes.")
        data = self.read_json()
        if data is None:
            return self.fail(400, "Bad request.")
        name = str(data.get("codename", "")).strip()
        password = str(data.get("password", ""))
        c = db()
        row = c.execute("SELECT * FROM agents WHERE codename = ?", (name,)).fetchone()
        ok = False
        if row:
            ok = hmac.compare_digest(hash_password(password, bytes(row["salt"])), bytes(row["pw"]))
        else:
            hash_password(password, b"0" * 16)  # same time for a wrong codename
        if not ok:
            return self.fail(401, "Wrong codename or password.")
        if row["banned"]:
            return self.fail(403, "This agent is blocked.")
        c.execute("UPDATE agents SET last_login = ? WHERE id = ?", (int(time.time()), row["id"]))
        c.commit()
        token = self.start_session(row["id"])
        admin.record_login(row["id"], self.headers.get("User-Agent", ""))
        self.send_json(200, {"ok": True, "token": token, "agent": self.public_agent(row)},
                       {"Set-Cookie": self.session_cookie_header(token, SESSION_DAYS * 86400)})

    def api_logout(self):
        token = None
        auth = self.headers.get("Authorization", "")
        if auth.lower().startswith("bearer "):
            token = auth[7:].strip()
        token = token or self.cookie("lh_session")
        if token:
            c = db()
            c.execute("DELETE FROM sessions WHERE token_hash = ?", (hash_token(token),))
            c.commit()
        self.send_json(200, {"ok": True}, {"Set-Cookie": self.session_cookie_header("", 0)})

    # An agent chooses Male or Female (an old agent who has none is asked when the account is opened)
    def api_gender(self):
        row = self.current_agent()
        if not row:
            return self.fail(401, "Sign in first.")
        if not rate_ok("gender:" + self.client_ip(), 20, 600):
            return self.fail(429, "Too many tries. Wait a few minutes.")
        data = self.read_json()
        if data is None:
            return self.fail(400, "Bad request.")
        gender = str(data.get("gender", "")).strip().lower()
        if gender not in GENDERS:
            return self.fail(400, "Choose Male or Female.")
        c = db()
        c.execute("UPDATE agents SET gender = ? WHERE id = ?", (gender, row["id"]))
        c.commit()
        row = c.execute("SELECT * FROM agents WHERE id = ?", (row["id"],)).fetchone()
        self.send_json(200, {"ok": True, "agent": self.public_agent(row)})

    def api_me(self):
        row = self.current_agent()
        if not row:
            return self.send_json(200, {"ok": True, "agent": None})
        self.send_json(200, {"ok": True, "agent": self.public_agent(row)})

    def api_score(self):
        row = self.current_agent()
        if not row:
            return self.fail(401, "Sign in first.")
        if not rate_ok("score:%d" % row["id"], 30, 600):
            return self.fail(429, "Too many scores.")
        data = self.read_json()
        if data is None:
            return self.fail(400, "Bad request.")
        try:
            captures = int(data.get("captures", 0))
            cages = int(data.get("cages", 0))
            nights = int(data.get("nights", 0))
        except (TypeError, ValueError):
            return self.fail(400, "Bad numbers.")
        if not (0 <= captures <= MAX_CAPTURES and 0 <= cages <= MAX_CAGES and 0 <= nights <= MAX_NIGHTS):
            return self.fail(400, "These numbers are not possible.")
        # the score is always computed here, never taken from the game
        score = compute_score(captures, cages, nights)
        now = int(time.time())
        c = db()
        c.execute("INSERT INTO runs (agent_id, score, captures, cages, nights, created) VALUES (?, ?, ?, ?, ?, ?)",
                  (row["id"], score, captures, cages, nights, now))
        improved = score > row["best_score"]
        if improved:
            c.execute(
                "UPDATE agents SET best_score = ?, best_captures = ?, best_cages = ?, best_nights = ?, best_at = ?, runs = runs + 1 WHERE id = ?",
                (score, captures, cages, nights, now, row["id"]))
        else:
            c.execute("UPDATE agents SET runs = runs + 1 WHERE id = ?", (row["id"],))
        c.commit()
        fresh = c.execute("SELECT * FROM agents WHERE id = ?", (row["id"],)).fetchone()
        self.send_json(200, {"ok": True, "score": score, "improved": improved, "agent": self.public_agent(fresh)})

    # -- the saved game (one for each agent): what the agent did, and a photo of the moment ----------------
    def api_save_info(self, query):
        row = self.current_agent()
        if not row:
            return self.fail(401, "Sign in first.")
        r = db().execute("SELECT state, day, hour, score, saved, length(photo) AS plen FROM saves WHERE agent_id = ?", (row["id"],)).fetchone()
        if not r:
            return self.send_json(200, {"ok": True, "save": {"saved": 0}})
        save = {"day": r["day"], "hour": r["hour"], "score": r["score"], "saved": r["saved"], "photo": bool(r["plen"])}
        if query.get("full"):
            save["state"] = r["state"]
        self.send_json(200, {"ok": True, "save": save})

    def api_save_photo(self):
        row = self.current_agent()
        if not row:
            return self.fail(401, "Sign in first.")
        r = db().execute("SELECT photo FROM saves WHERE agent_id = ?", (row["id"],)).fetchone()
        if not r or not r["photo"]:
            return self.fail(404, "No photo.")
        self.send_bytes(200, bytes(r["photo"]), "image/jpeg", {"Cache-Control": "no-store"})

    def api_save_put(self):
        row = self.current_agent()
        if not row:
            return self.fail(401, "Sign in first.")
        if not rate_ok("savegame:%d" % row["id"], 20, 600):
            return self.fail(429, "Too many saves. Wait a few minutes.")
        data = self.read_json(SAVE_LIMIT)
        if data is None:
            return self.fail(400, "Bad request.")

        state = data.get("state")
        if not isinstance(state, str) or not (2 < len(state) <= SAVE_STATE_MAX):
            return self.fail(400, "Bad save.")
        try:
            parsed = json.loads(state)
        except ValueError:
            return self.fail(400, "Bad save.")
        if not isinstance(parsed, dict):
            return self.fail(400, "Bad save.")

        photo = None
        raw = data.get("photo")
        if isinstance(raw, str) and raw:
            try:
                photo = base64.b64decode(raw, validate=True)
            except (ValueError, TypeError):
                return self.fail(400, "Bad photo.")
            if len(photo) > SAVE_PHOTO_MAX or not photo.startswith(b"\xff\xd8\xff"):
                return self.fail(400, "The photo must be a small JPEG.")

        try:
            day = max(1, min(9999, int(data.get("day", 1))))
            hour = max(0.0, min(24.0, float(data.get("hour", 20))))
            score = max(0, min(10 ** 9, int(data.get("score", 0))))
        except (TypeError, ValueError):
            return self.fail(400, "Bad numbers.")

        now = int(time.time())
        c = db()
        c.execute(
            "INSERT INTO saves (agent_id, state, photo, day, hour, score, saved) VALUES (?, ?, ?, ?, ?, ?, ?) "
            "ON CONFLICT(agent_id) DO UPDATE SET state = excluded.state, photo = excluded.photo, day = excluded.day, "
            "hour = excluded.hour, score = excluded.score, saved = excluded.saved",
            (row["id"], state, photo, day, hour, score, now))
        c.commit()
        self.send_json(200, {"ok": True, "saved": now})

    def api_leaderboard(self, query):
        try:
            limit = max(1, min(100, int(query.get("limit", ["10"])[0])))
        except ValueError:
            limit = 10
        c = db()
        rows = c.execute(
            "SELECT * FROM agents WHERE best_score > 0 ORDER BY best_score DESC, best_at ASC LIMIT ?", (limit,)
        ).fetchall()
        entries = []
        for i, r in enumerate(rows):
            entries.append({
                "rank": i + 1,
                "agentId": agent_code(r["id"]),
                "codename": r["codename"],
                "score": r["best_score"],
                "captures": r["best_captures"],
                "cages": r["best_cages"],
                "nights": r["best_nights"],
            })
        me = None
        agent = self.current_agent()
        if agent and agent["best_score"] > 0:
            me = {
                "rank": self.rank_of(agent),
                "agentId": agent_code(agent["id"]),
                "codename": agent["codename"],
                "score": agent["best_score"],
                "captures": agent["best_captures"],
                "cages": agent["best_cages"],
                "nights": agent["best_nights"],
            }
        total = c.execute("SELECT COUNT(*) AS n FROM agents WHERE best_score > 0").fetchone()["n"]
        self.send_json(200, {"ok": True, "entries": entries, "me": me, "total": total})

    def api_stats(self):
        c = db()
        a = c.execute("SELECT COUNT(*) AS n, COALESCE(MAX(best_score), 0) AS top FROM agents").fetchone()
        r = c.execute("SELECT COUNT(*) AS n FROM runs").fetchone()
        caught = c.execute("SELECT COALESCE(SUM(captures), 0) AS n FROM runs").fetchone()
        self.send_json(200, {"ok": True, "agents": a["n"], "topScore": a["top"], "runs": r["n"], "souls": caught["n"]},
                       {"Cache-Control": "public, max-age=20"})

    # -- the protected 3D models -----------------------------------------------------------------
    def api_models(self):
        out = []
        for m in load_models():
            out.append({k: m.get(k) for k in ("id", "title", "description", "tags", "author", "year", "poster") if k in m})
        self.send_json(200, {"ok": True, "models": out})

    def referer_ok(self):
        host = self.headers.get("Host", "")
        ref = self.headers.get("Referer", "") or self.headers.get("Origin", "")
        return bool(ref) and urlparse(ref).netloc == host

    def api_model_token(self, model_id):
        if not self.referer_ok():
            return self.fail(403, "Not allowed.")
        if not any(m.get("id") == model_id for m in load_models()):
            return self.fail(404, "No such model.")
        if not rate_ok("modeltoken:" + self.client_ip(), 60, 300):
            return self.fail(429, "Slow down.")
        token, key = make_model_token(model_id, self.client_ip(), self.headers.get("User-Agent", ""))
        self.send_json(200, {"ok": True, "t": token, "k": base64.b64encode(key).decode()})

    def api_model_data(self, model_id, query):
        # only for a script of our own page: opening the address in the browser or saving the page is refused
        dest = self.headers.get("Sec-Fetch-Dest", "")
        if dest and dest not in ("empty", ""):
            return self.fail(403, "Not allowed.")
        if not self.referer_ok():
            return self.fail(403, "Not allowed.")
        token = query.get("t", [""])[0]
        key = take_model_token(token, model_id, self.client_ip(), self.headers.get("User-Agent", ""))
        if key is None:
            return self.fail(403, "The link ended. Reload the page.")
        meta = next((m for m in load_models() if m.get("id") == model_id), None)
        if not meta:
            return self.fail(404, "No such model.")
        path = os.path.join(MODELS, os.path.basename(meta["file"]))
        try:
            with open(path, "rb") as f:
                data = f.read()
        except OSError:
            return self.fail(404, "No such model.")
        self.send_bytes(200, xor_bytes(data, key), "application/octet-stream",
                        {"Cache-Control": "no-store, max-age=0", "Content-Disposition": "inline"})

    # -- static files ----------------------------------------------------------------------------
    def serve_file(self, base, rel, cache):
        if os.environ.get("LORDS_DEV"):
            cache = "no-store"
        rel = rel.lstrip("/")
        path = os.path.normpath(os.path.join(base, rel))
        if not path.startswith(os.path.normpath(base)):
            return self.fail(403, "Forbidden.")
        if os.path.isdir(path):
            path = os.path.join(path, "index.html")
        if not os.path.isfile(path):
            return self.fail(404, "Not found.")

        ctype = mimetypes.guess_type(path)[0] or "application/octet-stream"
        if ctype.startswith("text/") or ctype in ("application/javascript", "application/json", "image/svg+xml"):
            ctype += "; charset=utf-8"

        st = os.stat(path)
        etag = '"%x-%x"' % (st.st_mtime_ns, st.st_size)
        if self.headers.get("If-None-Match") == etag:
            self.send_response(304)
            self.send_header("ETag", etag)
            self.send_header("Cache-Control", cache)
            self.end_headers()
            return

        # a song or a sound: the browser asks for parts of it (Range). Safari and iPhones do not play it without that answer.
        if ctype.startswith("audio/"):
            size = os.path.getsize(path)
            m = re.match(r"^bytes=(\d*)-(\d*)$", (self.headers.get("Range") or "").strip())
            if m and (m.group(1) or m.group(2)):
                if m.group(1):
                    start = int(m.group(1))
                    end = min(int(m.group(2)), size - 1) if m.group(2) else size - 1
                else:
                    start = max(0, size - int(m.group(2)))
                    end = size - 1
                if start >= size or start > end:
                    self.send_response(416)
                    self.send_header("Content-Range", "bytes */%d" % size)
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                self.send_response(206)
                self.send_header("Content-Type", ctype)
                self.send_header("Content-Range", "bytes %d-%d/%d" % (start, end, size))
                self.send_header("Content-Length", str(end - start + 1))
                self.send_header("Accept-Ranges", "bytes")
                self.send_header("ETag", etag)
                self.send_header("Cache-Control", cache)
                self.security_headers()
                self.end_headers()
                if self.command != "HEAD":
                    with open(path, "rb") as f:
                        f.seek(start)
                        left = end - start + 1
                        while left > 0:
                            chunk = f.read(min(262144, left))
                            if not chunk:
                                break
                            try:
                                self.wfile.write(chunk)
                            except (BrokenPipeError, ConnectionResetError):
                                break
                            left -= len(chunk)
                return

        # a pre-compressed copy (made by the deploy tool) when the browser takes gzip
        serve_path, encoding = path, None
        if "gzip" in self.headers.get("Accept-Encoding", "") and os.path.isfile(path + ".gz"):
            serve_path, encoding = path + ".gz", "gzip"
        elif "gzip" in self.headers.get("Accept-Encoding", "") and ctype.split(";")[0] in (
                "text/html", "text/css", "application/javascript", "application/json", "image/svg+xml") and st.st_size > 600:
            with open(path, "rb") as f:
                body = gzip.compress(f.read(), 6)
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Encoding", "gzip")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("ETag", etag)
            self.send_header("Cache-Control", cache)
            self.send_header("Vary", "Accept-Encoding")
            self.security_headers()
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(body)
            return

        size = os.path.getsize(serve_path)
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(size))
        self.send_header("ETag", etag)
        self.send_header("Cache-Control", cache)
        self.send_header("Vary", "Accept-Encoding")
        if ctype.startswith("audio/"):
            self.send_header("Accept-Ranges", "bytes")
        if encoding:
            self.send_header("Content-Encoding", encoding)
        self.security_headers()
        self.end_headers()
        if self.command == "HEAD":
            return
        with open(serve_path, "rb") as f:
            while True:
                chunk = f.read(262144)
                if not chunk:
                    break
                try:
                    self.wfile.write(chunk)
                except (BrokenPipeError, ConnectionResetError):
                    break

    # -- routing ---------------------------------------------------------------------------------
    def route(self):
        parsed = urlparse(self.path)
        path = unquote(parsed.path)
        query = parse_qs(parsed.query)
        method = self.command

        if path.startswith("/api/"):
            api = path[5:].strip("/")
            if api.startswith("admin/") and method in ("GET", "HEAD", "POST"):
                return admin.handle(self, method, api[6:], query)
            if method == "GET" or method == "HEAD":
                if api == "me":
                    return self.api_me()
                if api == "leaderboard":
                    return self.api_leaderboard(query)
                if api == "stats":
                    return self.api_stats()
                if api == "crew":
                    return admin.api_crew_public(self)
                if api == "health":
                    return self.send_json(200, {"ok": True, "time": int(time.time())})
                if api == "save-game":
                    return self.api_save_info(query)
                if api == "save-game/photo":
                    return self.api_save_photo()
                if api == "config":
                    site, secret = recaptcha_keys()
                    return self.send_json(200, {"ok": True, "captcha": bool(site and secret), "siteKey": site if secret else ""})
                if api == "models":
                    return self.api_models()
                m = re.match(r"^model/([A-Za-z0-9_\-]+)/(token|data)$", api)
                if m:
                    return self.api_model_token(m.group(1)) if m.group(2) == "token" else self.api_model_data(m.group(1), query)
            elif method == "POST":
                if api == "register":
                    return self.api_register()
                if api == "login":
                    return self.api_login()
                if api == "logout":
                    return self.api_logout()
                if api == "gender":
                    return self.api_gender()
                if api == "score":
                    return self.api_score()
                if api == "save-game":
                    return self.api_save_put()
            return self.fail(404, "Unknown address.")

        if method not in ("GET", "HEAD"):
            return self.fail(405, "Not allowed.")

        if path == "/play":
            self.send_response(301)
            self.send_header("Location", "/play/")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        if path.startswith("/play/"):
            rel = path[len("/play/"):]
            # the big files are asked as ?v=<version> (see deploy_pro.py): such an address never changes, so it is kept for good
            cache = CACHE_FOREVER if query.get("v") else "no-cache"
            return self.serve_file(GAME, rel, cache)

        # the privacy text has its own page
        if path in ("/privacy", "/privacy/"):
            return admin.serve_html(self, "privacy.html")
        if path == "/privacy.html":
            self.send_response(301)
            self.send_header("Location", "/privacy")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return

        # the pictures the admins uploaded
        if path.startswith("/uploads/"):
            return admin.serve_upload(self, path[len("/uploads/"):])
        # the admin panel: never cached
        if path == "/admin":
            self.send_response(301)
            self.send_header("Location", "/admin/")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        if path.startswith("/admin/"):
            return self.serve_file(SITE, path, "no-cache")

        # the models folder is never served as files
        if path == "/":
            return admin.serve_index(self)       # index.html with the texts, pictures and pages the admins edited
        if path in ("/sw.js", "/index.html", "/robots.txt", "/manifest.webmanifest"):
            return self.serve_file(SITE, path, "no-cache")
        versioned = bool(query.get("v"))
        return self.serve_file(SITE, path, CACHE_FOREVER if versioned else "public, max-age=3600")

    def do_GET(self):
        try:
            self.route()
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception as e:  # never kill the server for one bad request
            sys.stderr.write("ERROR %r\n" % (e,))
            try:
                self.fail(500, "Server error.")
            except Exception:
                pass

    do_HEAD = do_GET
    do_POST = do_GET


def main():
    admin.setup(globals())
    if len(sys.argv) >= 3 and sys.argv[1] == "--create-admin":
        init_db()
        sys.exit(admin.cli_create_admin(sys.argv[2]))
    init_db()
    ThreadingHTTPServer.allow_reuse_address = True
    ThreadingHTTPServer.daemon_threads = True
    httpd = ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
    print("Leikpyar Hunters server on port %d, root %s" % (PORT, ROOT), flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
