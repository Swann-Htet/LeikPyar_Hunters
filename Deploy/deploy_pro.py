#!/usr/bin/env python3
"""
Puts the Unity WebGL build (D:\\PRO) online as the GAME of Leikpyar Hunters: http://<server>:8000/play/

What it does:
  - gives the build a VERSION (a hash of the big files) and writes /play/version.json (the website reads it)
  - adds ?v=<version> to the addresses of the big files in index.html, so a browser or the service worker keeps them
    until a new version is deployed (download once)
  - uploads, replaces /opt/lords/game, makes .gz copies of the big files on the server (the first download is smaller)

The website, the accounts and the leaderboard are deployed by deploy_web.py (run it first on a new server).

Lives at D:\\deploy_tools\\deploy_pro.py (outside D:\\PRO) so a Unity rebuild does not delete it.
Usage:   python D:\\deploy_tools\\deploy_pro.py
Needs:   pip install paramiko
"""
import hashlib
import json
import os
import re
import shutil
import tarfile
import tempfile

import paramiko

import lh_ssh

HOST = "152.42.217.145"
USER = "root"
LOCAL_DIR = r"D:\PRO"
REMOTE = "/opt/lords"
REMOTE_GAME = REMOTE + "/game"


def run(c, cmd, show=True):
    stdin, stdout, stderr = c.exec_command(cmd)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    code = stdout.channel.recv_exit_status()
    if show:
        print("$ " + cmd + "\n--exit " + str(code) + "--\n" + out + err)
    return code, out, err


def sha1_of(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        while True:
            chunk = f.read(1 << 20)
            if not chunk:
                break
            h.update(chunk)
    return h.hexdigest()


def prepare(stage):
    build = os.path.join(stage, "Build")
    files = sorted(f for f in os.listdir(build) if os.path.isfile(os.path.join(build, f)))
    h = hashlib.sha1()
    for f in files:
        h.update(f.encode())
        h.update(sha1_of(os.path.join(build, f)).encode())
    version = h.hexdigest()[:10]

    # ?v=<version> on the Unity files in index.html
    index = os.path.join(stage, "index.html")
    with open(index, "r", encoding="utf-8") as f:
        html = f.read()
    html, n = re.subn(r'(Build/[A-Za-z0-9_\-.]+\.(?:data|wasm|framework\.js|loader\.js|js|symbols\.json))(?!\?v=)', r"\1?v=" + version, html)
    # the template builds the addresses in script: buildUrl + "/PRO.data"
    html, n2 = re.subn(r'(buildUrl \+ "/[A-Za-z0-9_\-.]+\.(?:data|wasm|framework\.js|loader\.js|js))(")', r"\1?v=" + version + r"\2", html)
    with open(index, "w", encoding="utf-8", newline="\n") as f:
        f.write(html)
    print("index.html: %d + %d addresses versioned (%s)" % (n, n2, version))

    entries = []
    total = 0
    for f in files:
        size = os.path.getsize(os.path.join(build, f))
        total += size
        entries.append({"url": "/play/Build/%s?v=%s" % (f, version), "size": size})
    with open(os.path.join(stage, "version.json"), "w", encoding="utf-8") as f:
        json.dump({"version": version, "total": total, "files": entries}, f, separators=(",", ":"))
    return version, total


def main():
    if not os.path.isdir(os.path.join(LOCAL_DIR, "Build")):
        raise SystemExit("D:\\PRO\\Build is missing: export the WebGL build from Unity first.")

    with tempfile.TemporaryDirectory() as tmp:
        stage = os.path.join(tmp, "game")
        print("Copying the build ...")
        shutil.copytree(LOCAL_DIR, stage)
        version, total = prepare(stage)

        archive = os.path.join(tmp, "game.tar.gz")
        print("Archiving %.0f MB ..." % (total / 1048576))
        with tarfile.open(archive, "w:gz") as tar:
            tar.add(stage, arcname="game")
        print("Archive: %.1f MB" % (os.path.getsize(archive) / 1048576))

        print("Connecting to %s ..." % HOST)
        c = lh_ssh.connect()          # an SSH key, or the LH_SSH_PASSWORD variable (see lh_ssh.py)

        code, out, err = run(c, "test -f %s/server/server.py && echo yes" % REMOTE, show=False)
        if "yes" not in out:
            raise SystemExit("The web server is not installed yet: run  python D:\\deploy_tools\\deploy_web.py  first.")

        print("Uploading ...")
        sftp = c.open_sftp()
        last = [0]

        def progress(done, size):
            if done - last[0] > 8 * 1048576 or done == size:
                last[0] = done
                print("  %.0f of %.0f MB" % (done / 1048576, size / 1048576))

        sftp.put(archive, "/root/game.tar.gz", callback=progress)
        sftp.close()

        run(c, "rm -rf %s.new && mkdir -p %s.new && tar xzf /root/game.tar.gz -C %s.new --strip-components=1 && rm -f /root/game.tar.gz" % (REMOTE_GAME, REMOTE_GAME, REMOTE_GAME))
        # compressed copies of the big files (the server sends them to browsers that accept gzip)
        run(c, "cd %s.new/Build && for f in *; do case \"$f\" in *.gz) ;; *) gzip -9 -k -f \"$f\" ;; esac; done; ls -la" % REMOTE_GAME)
        run(c, "rm -rf %s.old && (test -d %s && mv %s %s.old || true) && mv %s.new %s && rm -rf %s.old" % ((REMOTE_GAME,) * 7))
        run(c, "ls -la %s | head; curl -s http://127.0.0.1:8000/play/version.json | head -c 300; echo; curl -sI http://127.0.0.1:8000/play/ | head -3" % REMOTE_GAME)
        c.close()
        print("\nDone. Version %s. The game is at http://%s:8000/play/  and the website at http://%s:8000/" % (version, HOST, HOST))


if __name__ == "__main__":
    main()
