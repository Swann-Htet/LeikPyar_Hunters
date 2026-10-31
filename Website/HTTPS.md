# HTTPS for leikpyarhunters.org

Why: accounts and passwords must not travel in plain text, and the instant-start game cache (service worker) works only on https.

Done on 2026-10-29: the docker container lor-nginx (port 80) was stopped (docker stop lor-nginx; the other lor-* containers still run), so Caddy
uses ports 80 and 443 and gets the certificate by itself. To bring the old site back: systemctl stop caddy, then docker start lor-nginx.

On the server (as root), after the DNS A records point to 152.42.217.145:

    apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
    apt-get update && apt-get install -y caddy
    ufw allow 443/tcp

/etc/caddy/Caddyfile:

    leikpyarhunters.org, www.leikpyarhunters.org {
        encode zstd gzip
        reverse_proxy 127.0.0.1:8000
    }

    systemctl restart caddy

Then in the game (Assets/Scripts/Online/ApiClient.cs) set DefaultBase to "https://leikpyarhunters.org" (only the Editor uses it).
