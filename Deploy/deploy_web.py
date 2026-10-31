#!/usr/bin/env python3
"""
Deploys the WEBSITE and the SERVER (accounts, leaderboard, protected 3D models) of Leikpyar Hunters.

  Source : D:\\LORDS_WEB   (site/, server/server.py, models/)
  Target : /opt/lords      (site/, server/, models/ are replaced; game/ and data/ are NOT touched)
  Service: lords-web       (python3 /opt/lords/server/server.py, port 8000)

Run it after you change the website, the server or the models. To put a new Unity build online use deploy_pro.py.
The first run also moves the old 'pro-http' service away (it used port 8000).

Usage:   python D:\\deploy_tools\\deploy_web.py
Needs:   pip install paramiko
"""
import hashlib
import os
import re
import shutil
import tarfile
import tempfile

import paramiko

import lh_ssh

HOST = "152.42.217.145"
USER = "root"
LOCAL = r"D:\LORDS_WEB"
REMOTE = "/opt/lords"
SERVICE = "lords-web"

UNIT = """[Unit]
Description=Leikpyar Hunters (website, accounts, leaderboard, game files)
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/lords
Environment=LORDS_ROOT=/opt/lords
Environment=LORDS_PORT=8000
ExecStart=/usr/bin/python3 /opt/lords/server/server.py
Restart=always
RestartSec=3
User=root

[Install]
WantedBy=multi-user.target
"""


def run(c, cmd, show=True):
    stdin, stdout, stderr = c.exec_command(cmd)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    code = stdout.channel.recv_exit_status()
    if show:
        print("$ " + cmd + "\n--exit " + str(code) + "--\n" + out + err)
    return code, out, err


def file_hash(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        h.update(f.read())
    return h.hexdigest()[:10]


def stamp_versions(stage_site):
    """?v=<hash of the file> on every local asset of index.html, so the browser keeps them until the file changes."""
    index = os.path.join(stage_site, "index.html")
    with open(index, "r", encoding="utf-8") as f:
        html = f.read()

    def repl(match):
        url = match.group(1)
        path = os.path.join(stage_site, url.lstrip("/").replace("/", os.sep))
        return url + "?v=" + (file_hash(path) if os.path.isfile(path) else "1") + match.group(2)

    html = re.sub(r'(/(?:css|js|img|fonts|vendor)/[A-Za-z0-9_\-./]+?)\?v=[A-Za-z0-9.]+(["\'])', repl, html)
    with open(index, "w", encoding="utf-8", newline="\n") as f:
        f.write(html)

    # the same inside the css (the fonts)
    css = os.path.join(stage_site, "css", "site.css")
    with open(css, "r", encoding="utf-8") as f:
        text = f.read()
    text = re.sub(r'(/fonts/[A-Za-z0-9_\-.]+)\?v=[A-Za-z0-9.]+', repl2(stage_site), text)
    with open(css, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def repl2(stage_site):
    def inner(match):
        url = match.group(1)
        path = os.path.join(stage_site, url.lstrip("/").replace("/", os.sep))
        return url + "?v=" + (file_hash(path) if os.path.isfile(path) else "1")
    return inner


def main():
    with tempfile.TemporaryDirectory() as tmp:
        stage = os.path.join(tmp, "lords")
        os.makedirs(stage)
        shutil.copytree(os.path.join(LOCAL, "site"), os.path.join(stage, "site"))
        os.makedirs(os.path.join(stage, "server"))
        for name in os.listdir(os.path.join(LOCAL, "server")):          # server.py and admin.py
            if name.endswith(".py"):
                shutil.copy(os.path.join(LOCAL, "server", name), os.path.join(stage, "server", name))
        shutil.copytree(os.path.join(LOCAL, "models"), os.path.join(stage, "models"))
        stamp_versions(os.path.join(stage, "site"))

        print("Connecting to %s ..." % HOST)
        c = lh_ssh.connect()          # an SSH key, or the LH_SSH_PASSWORD variable (see lh_ssh.py)
        run(c, "mkdir -p %s/site %s/server %s/models %s/data %s/game" % ((REMOTE,) * 5), show=False)

        # only the files that changed are sent: compare a hash of every file here with the one on the server
        local = {}
        for top in ("site", "server", "models"):
            for folder, _dirs, files in os.walk(os.path.join(stage, top)):
                for name in files:
                    full = os.path.join(folder, name)
                    local[os.path.relpath(full, stage).replace(os.sep, "/")] = full
        local_sum = {}
        for rel, full in local.items():
            with open(full, "rb") as f:
                local_sum[rel] = hashlib.sha1(f.read()).hexdigest()

        _code, out, _err = run(c, "cd %s && find site server models -type f -print0 | xargs -0 -r sha1sum" % REMOTE, show=False)
        remote_sum = {}
        for line in out.splitlines():
            if len(line) > 42:
                remote_sum[line[42:]] = line[:40]

        changed = sorted(rel for rel in local if remote_sum.get(rel) != local_sum[rel])
        gone = sorted(rel for rel in remote_sum if rel not in local and "__pycache__" not in rel)
        print("%d files here, %d changed, %d removed" % (len(local), len(changed), len(gone)))

        sftp = c.open_sftp()
        with sftp.open("/etc/systemd/system/%s.service" % SERVICE, "w") as f:
            f.write(UNIT)
        if changed:
            archive = os.path.join(tmp, "lords_web.tar.gz")
            with tarfile.open(archive, "w:gz") as tar:
                for rel in changed:
                    tar.add(local[rel], arcname=rel)
            print("Sending %.2f MB ..." % (os.path.getsize(archive) / 1048576))
            sftp.put(archive, "/root/lords_web.tar.gz")
            run(c, "tar xzf /root/lords_web.tar.gz -C %s && rm -f /root/lords_web.tar.gz" % REMOTE, show=False)
        sftp.close()
        for rel in gone:
            run(c, "rm -f '%s/%s'" % (REMOTE, rel.replace("'", "")), show=False)
        run(c, "find %s/site %s/server %s/models -type d -empty -delete" % ((REMOTE,) * 3), show=False)
        run(c, "chmod 700 %s/models %s/data" % (REMOTE, REMOTE))

        # the old static server used port 8000: it goes away (its folder /opt/PRO stays as a backup)
        run(c, "systemctl disable --now pro-http 2>&1 | tail -1", show=False)
        run(c, "systemctl daemon-reload && systemctl enable %s 2>&1 | tail -1 && systemctl restart %s && sleep 2 && systemctl is-active %s" % (SERVICE, SERVICE, SERVICE))
        run(c, "curl -s http://127.0.0.1:8000/api/health; echo; curl -sI http://127.0.0.1:8000/ | head -3")
        c.close()
        print("\nDone. The website is live at http://%s:8000/" % HOST)


if __name__ == "__main__":
    main()
