# Leikpyar Hunters - website and server

    site/      the website (index.html, css, js, fonts, vendor/model-viewer, sw.js)
    server/    server.py  (Python only: website, /play/ game files, accounts, scores, leaderboard, protected 3D models)
    models/    the 3D art (.glb) + models.json. It is NEVER served as a file: only through /api/model/... (see server.py)
    data/      the database (lords.db) and the secret key. Created by the server. Do not upload it.

## Run on your PC (to look at it)

    cd D:\LORDS_WEB
    set LORDS_DEV=1 & set LORDS_PORT=8123 & python server\server.py
    open http://localhost:8123/

(`game/` is where a Unity build goes; deploy_pro.py makes it on the server. For a local test copy D:\PRO there.)

## Put it online

    python D:\deploy_tools\deploy_web.py     # the website + server + models   (first time: also moves the old pro-http away)
    python D:\deploy_tools\deploy_pro.py     # the Unity build in D:\PRO -> /play/

## Add a 3D model to the gallery

1. Put the .glb in models/ and add it to models/models.json (id, title, description, file, tags, author, year).
2. Run deploy_web.py. It shows in the gallery, protected.

## The admin panel (https://leikpyarhunters.org/admin/)

Files: server/admin.py (the API), site/admin/ (the screens), site/js/content.js (puts the edits into the public page).

What it does: charts and numbers of the agents; find, block, sign out, reset the password or delete an agent; export the agents (CSV);
edit EVERY text of the website in English and in Burmese; replace every picture (creator photos, logo, icons); edit the Privacy page and
the contact e-mail of the license; add and remove admins; an activity log.

How the edits work: they are kept in the database (table `content`), not in the files. The server puts them into index.html
(`window.LH_CONTENT`) and content.js applies them before i18n.js starts. So a new deploy of the website keeps every edit, and
"Reset all texts" in the panel brings back the texts of the files. The list of texts is read from site/index.html and site/js/i18n-my.js:
a text you add to index.html shows up in the panel by itself. Uploaded pictures are in data/uploads/.

The first admin is made on the server (the password is in a variable, so it does not stay in the shell history of the process list):

    LH_ADMIN_PASSWORD='the password' LORDS_ROOT=/opt/lords python3 /opt/lords/server/server.py --create-admin you@example.com

The same command with an existing e-mail sets a new password (a lost password). Every admin can add other admins in the panel.

## Contact address in the art license

In the admin panel: Privacy and license > Contact e-mail for the license.

## The domain (leikpyarhunters.org) with https

Point the A record of leikpyarhunters.org (and www) to 152.42.217.145. https is done by Caddy on the server (ports 80 and 443,
the old docker nginx on port 80 was stopped): see HTTPS.md.
