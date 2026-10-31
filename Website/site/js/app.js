/* Leikpyar Hunters - the website script. No libraries. */
(function () {
  "use strict";

  // To show a contact address in the art license, write it here (for example "name@example.com"). Empty = the line stays general.
  var CONTACT_EMAIL = (window.LH_SETTINGS && window.LH_SETTINGS.contactEmail) || "";     // the admins can set it in /admin/ (Pages)

  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ------------------------------------------------------------------------------------------ api
  function api(path, options) {
    options = options || {};
    var init = { method: options.method || "GET", credentials: "same-origin", headers: {} };
    if (options.body) {
      init.method = "POST";
      init.headers["Content-Type"] = "application/json";
      init.body = JSON.stringify(options.body);
    }
    return fetch(path, init).then(function (r) {
      return r.json().catch(function () { return { ok: false, error: "Bad answer from the server." }; });
    }).catch(function () {
      return { ok: false, error: "No connection to the server." };
    });
  }

  function pad(n, len) { n = String(Math.max(0, n | 0)); while (n.length < len) n = "0" + n; return n; }
  function fmt(n) { return Number(n || 0).toLocaleString("en-US"); }
  function esc(s) { return String(s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
  function ordinal(n) {
    if (n % 100 >= 11 && n % 100 <= 13) return n + "TH";
    return n + ({ 1: "ST", 2: "ND", 3: "RD" }[n % 10] || "TH");
  }

  // ------------------------------------------------------------------------------------------ header, menu, reveal
  var header = $("#top");
  function onScroll() { header.classList.toggle("solid", window.scrollY > 24); }
  window.addEventListener("scroll", onScroll, { passive: true });
  onScroll();

  var nav = $("#nav"), menuBtn = $("#menuBtn");
  function closeMenu() {
    nav.classList.remove("open");
    menuBtn.setAttribute("aria-expanded", "false");
    menuBtn.querySelector("use").setAttribute("href", "#i-menu");
  }
  menuBtn.addEventListener("click", function () {
    var open = !nav.classList.contains("open");
    nav.classList.toggle("open", open);
    menuBtn.setAttribute("aria-expanded", String(open));
    menuBtn.querySelector("use").setAttribute("href", open ? "#i-x" : "#i-menu");
  });
  $$("a", nav).forEach(function (a) { a.addEventListener("click", closeMenu); });
  document.addEventListener("keydown", function (e) { if (e.key === "Escape") closeMenu(); });

  if ("IntersectionObserver" in window && !reduceMotion) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add("in"); io.unobserve(en.target); } });
    }, { threshold: 0.12, rootMargin: "0px 0px -6% 0px" });
    $$(".reveal").forEach(function (el) { io.observe(el); });

    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        $$("a", nav).forEach(function (a) { a.classList.toggle("active", a.getAttribute("href") === "#" + en.target.id); });
      });
    }, { threshold: 0.45 });
    $$("main section[id]").forEach(function (s) { spy.observe(s); });
  } else {
    $$(".reveal").forEach(function (el) { el.classList.add("in"); });
  }

  // ------------------------------------------------------------------------------------------ dust (little squares)
  (function dust() {
    var canvas = $("#dust");
    if (!canvas || reduceMotion) return;
    var ctx = canvas.getContext("2d");
    var w = 0, h = 0, dpr = Math.min(window.devicePixelRatio || 1, 2), specks = [], running = true;

    function resize() {
      var r = canvas.getBoundingClientRect();
      w = r.width; h = r.height;
      canvas.width = w * dpr; canvas.height = h * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      var count = Math.round(Math.min(70, Math.max(24, w * h / 26000)));
      specks = [];
      for (var i = 0; i < count; i++) specks.push(make(true));
    }
    function make(anywhere) {
      return { x: Math.random() * w, y: anywhere ? Math.random() * h : h + 10, s: 1 + Math.random() * 2.6, v: 6 + Math.random() * 16, a: 0.12 + Math.random() * 0.34, p: Math.random() * 6.28, sw: 8 + Math.random() * 26 };
    }
    var last = 0;
    function frame(t) {
      if (!running) return;
      var dt = Math.min(0.05, (t - last) / 1000 || 0.016);
      last = t;
      ctx.clearRect(0, 0, w, h);
      for (var i = 0; i < specks.length; i++) {
        var p = specks[i];
        p.y -= p.v * dt;
        if (p.y < -10) specks[i] = p = make(false);
        var x = p.x + Math.sin(t / 2600 + p.p) * p.sw;
        ctx.fillStyle = "rgba(255, 214, 166, " + p.a + ")";
        ctx.fillRect(x, p.y, p.s, p.s);
      }
      requestAnimationFrame(frame);
    }
    resize();
    window.addEventListener("resize", resize);
    document.addEventListener("visibilitychange", function () {
      running = !document.hidden;
      if (running) requestAnimationFrame(frame);
    });
    requestAnimationFrame(frame);
  })();

  // ------------------------------------------------------------------------------------------ numbers
  function countTo(el, to) {
    to = to | 0;
    var from = parseInt((el.dataset.v || "0"), 10) || 0;
    el.dataset.v = to;
    if (reduceMotion || from === to) { el.textContent = fmt(to); return; }
    var start = performance.now(), dur = 900;
    function step(t) {
      var k = Math.min(1, (t - start) / dur), e = 1 - Math.pow(1 - k, 3);
      el.textContent = fmt(Math.round(from + (to - from) * e));
      if (k < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  }
  function loadStats() {
    if (!$("#statAgents")) return;       // the numbers are not on the page any more
    api("/api/stats").then(function (s) {
      if (!s.ok) return;
      countTo($("#statAgents"), s.agents);
      countTo($("#statSouls"), s.souls);
      countTo($("#statRuns"), s.runs);
      countTo($("#statTop"), s.topScore);
    });
  }

  // ------------------------------------------------------------------------------------------ the account
  var me = null;
  var mode = "login";

  // The robot check (Google reCAPTCHA) for creating an agent. It only exists when the server has the keys (/api/config).
  var captchaKey = "", captchaWidget = null, captchaLoading = false;

  function loadCaptcha() {
    if (!captchaKey || captchaWidget !== null || captchaLoading) return;
    captchaLoading = true;
    window.lhCaptchaReady = function () {
      captchaWidget = window.grecaptcha.render("captcha", { sitekey: captchaKey, theme: "dark" });
    };
    var s = document.createElement("script");
    s.src = "https://www.google.com/recaptcha/api.js?onload=lhCaptchaReady&render=explicit";
    s.async = true;
    s.defer = true;
    document.head.appendChild(s);
  }
  function resetCaptcha() {
    if (captchaWidget !== null && window.grecaptcha) window.grecaptcha.reset(captchaWidget);
  }
  api("/api/config").then(function (r) {
    if (r.ok && r.captcha && r.siteKey) { captchaKey = r.siteKey; if (mode === "register") setMode("register"); }
  });

  function setMode(m) {
    mode = m;
    var needCaptcha = m === "register" && !!captchaKey;
    $("#captchaBox").hidden = !needCaptcha;
    $("#genderBox").hidden = m !== "register";
    if (needCaptcha) loadCaptcha();
    $("#tabLogin").setAttribute("aria-selected", String(m === "login"));
    $("#tabRegister").setAttribute("aria-selected", String(m === "register"));
    $("#formSubmitText").textContent = m === "login" ? "Sign in" : "Create my Agent ID";
    $("#fPass").setAttribute("autocomplete", m === "login" ? "current-password" : "new-password");
    $("#formMsg").textContent = "";
  }
  // Male / Female (a new agent must choose one)
  var gender = "";
  function pickGender(box, attr, value) {
    $$("[" + attr + "]", box).forEach(function (b) { b.setAttribute("aria-checked", String(b.getAttribute(attr) === value)); });
  }
  $$("[data-gender]").forEach(function (b) {
    b.addEventListener("click", function () { gender = b.getAttribute("data-gender"); pickGender($("#genderBox"), "data-gender", gender); });
  });
  // an agent made before the choice existed is asked when the account is opened
  $$("[data-ask]").forEach(function (b) {
    b.addEventListener("click", function () {
      var value = b.getAttribute("data-ask");
      pickGender($("#genderAsk"), "data-ask", value);
      $("#askMsg").textContent = "";
      api("/api/gender", { body: { gender: value } }).then(function (r) {
        if (!r.ok) { $("#askMsg").textContent = r.error || "Something went wrong."; return; }
        showAgent(r.agent);
      });
    });
  });

  $("#tabLogin").addEventListener("click", function () { setMode("login"); });
  $("#tabRegister").addEventListener("click", function () { setMode("register"); });

  // the game sends a new player here (the robot check is on this page): ?register=1&gender=male opens the Create agent form
  function openFromGame() {
    var q = new URLSearchParams(location.search);
    if (q.get("register") !== "1") return;
    setMode("register");
    var g = q.get("gender");
    if (g === "male" || g === "female") { gender = g; pickGender($("#genderBox"), "data-gender", g); }
  }

  function showAgent(agent) {
    me = agent;
    var chip = $("#agentChip"), text = $("#agentChipText");
    if (agent) {
      chip.classList.add("on");
      text.textContent = agent.agentId;
      $("#badge").hidden = false;
      $("#authForm").hidden = true;
      $("#badgeId").textContent = agent.agentId;
      $("#badgeName").textContent = agent.codename;
      var g = $("#badgeGender"), ask = !agent.gender;
      g.hidden = ask;
      g.textContent = agent.gender === "female" ? "Female" : "Male";
      $("#genderAsk").hidden = !ask;
      $("#badgeStats").hidden = ask;
      $("#badgePlay").hidden = ask;
      $("#badgeRank").textContent = agent.rank ? "#" + agent.rank : "-";
      $("#badgeScore").textContent = fmt(agent.bestScore);
      $("#badgeRuns").textContent = fmt(agent.runs);
      $("#boardPrompt").textContent = "Press play to beat your best";
    } else {
      chip.classList.remove("on");
      text.textContent = "Sign in";
      $("#badge").hidden = true;
      $("#authForm").hidden = false;
      $("#boardPrompt").textContent = "Press play to enter the board";
    }
  }
  $("#agentChip").addEventListener("click", function () {
    var t = $("#agent");
    t.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth" });
    if (!me) setTimeout(function () { $("#fName").focus({ preventScroll: true }); }, 500);
  });

  $("#authForm").addEventListener("submit", function (e) {
    e.preventDefault();
    var msg = $("#formMsg"), btn = $("#formSubmit");
    var name = $("#fName").value.trim(), pass = $("#fPass").value;
    msg.className = "form-msg";
    if (name.length < 3) { msg.textContent = "The codename needs at least 3 characters."; return; }
    if (pass.length < 6) { msg.textContent = "The password needs at least 6 characters."; return; }
    var body = { codename: name, password: pass };
    if (mode === "register") {
      if (!gender) { msg.textContent = "Choose Male or Female."; return; }
      body.gender = gender;
    }
    if (mode === "register" && captchaKey) {
      var answer = captchaWidget !== null && window.grecaptcha ? window.grecaptcha.getResponse(captchaWidget) : "";
      if (!answer) { msg.textContent = "Please tick \"I'm not a robot\" first."; return; }
      body.captcha = answer;
    }
    btn.disabled = true;
    msg.textContent = mode === "login" ? "Signing in" : "Creating your Agent ID";
    api(mode === "login" ? "/api/login" : "/api/register", { body: body }).then(function (r) {
      btn.disabled = false;
      if (mode === "register") resetCaptcha();      // an answer is good once
      if (!r.ok) { msg.textContent = r.error || "Something went wrong."; return; }
      $("#fPass").value = "";
      msg.textContent = "";
      showAgent(r.agent);
      loadBoard();
      loadStats();
    });
  });

  $("#logoutBtn").addEventListener("click", function () {
    api("/api/logout", { body: {} }).then(function () { showAgent(null); loadBoard(); });
  });

  function loadMe() {
    return api("/api/me").then(function (r) { showAgent(r.ok ? r.agent : null); });
  }

  // ------------------------------------------------------------------------------------------ the arcade leaderboard
  var boardLimit = 10;
  function rowHtml(e, mine) {
    var cls = "row" + (e.rank <= 3 ? " r" + e.rank : "") + (mine ? " me" : "");
    return '<li class="' + cls + '">' +
      '<span class="rank">' + ordinal(e.rank) + '</span>' +
      '<span class="who"><b>' + esc(e.codename) + (mine ? '<i class="you">YOU</i>' : "") + '</b><small>' + esc(e.agentId) + '</small>' +
      '<small class="stats-line">' + e.captures + " souls &middot; " + e.cages + " cages &middot; " + e.nights + " nights</small></span>" +
      '<span class="n">' + e.captures + '</span><span class="n">' + e.cages + '</span><span class="n">' + e.nights + '</span>' +
      '<span class="s">' + pad(e.score, 7) + "</span></li>";
  }
  function loadBoard() {
    api("/api/leaderboard?limit=" + boardLimit).then(function (r) {
      var list = $("#board");
      if (!r.ok) { list.innerHTML = '<li class="board-empty">' + esc(r.error || "The board is not reachable") + "</li>"; return; }
      if (!r.entries.length) { list.innerHTML = '<li class="board-empty">No scores yet. Be the first agent</li>'; $("#boardMore").hidden = true; return; }
      var html = "", meShown = false;
      r.entries.forEach(function (e) {
        var mine = r.me && r.me.agentId === e.agentId;
        if (mine) meShown = true;
        html += rowHtml(e, mine);
      });
      if (r.me && !meShown) html += '<li class="board-gap">. . .</li>' + rowHtml(r.me, true);
      list.innerHTML = html;
      $("#boardMore").hidden = !(r.total > boardLimit && boardLimit < 50);
    });
  }
  $("#boardRefresh").addEventListener("click", function () { loadBoard(); loadStats(); });
  $("#boardMore").addEventListener("click", function () { boardLimit = Math.min(50, boardLimit + 15); loadBoard(); });
  setInterval(function () { if (!document.hidden) { loadBoard(); loadStats(); } }, 30000);

  // ------------------------------------------------------------------------------------------ the protected 3D models
  function b64ToBytes(b64) {
    var bin = atob(b64), out = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
    return out;
  }
  function loadModel(el, id) {
    if (window.lhBlocked) return Promise.reject(new Error("blocked"));
    // a short-lived link and a key for this one request: the file is scrambled on the way and put together here, in memory
    return api("/api/model/" + id + "/token").then(function (t) {
      if (!t.ok) throw new Error(t.error || "no link");
      return fetch("/api/model/" + id + "/data?t=" + encodeURIComponent(t.t), { credentials: "same-origin", cache: "no-store" })
        .then(function (r) { if (!r.ok) throw new Error("no data"); return r.arrayBuffer(); })
        .then(function (buf) {
          var data = new Uint8Array(buf), key = b64ToBytes(t.k), n = key.length;
          for (var i = 0; i < data.length; i++) data[i] ^= key[i % n];
          var url = URL.createObjectURL(new Blob([data], { type: "model/gltf-binary" }));
          el.addEventListener("load", function () { setTimeout(function () { URL.revokeObjectURL(url); }, 1500); }, { once: true });
          el.src = url;
        });
    });
  }
  function guard(el) {
    ["contextmenu", "dragstart", "copy", "cut"].forEach(function (ev) { el.addEventListener(ev, function (e) { e.preventDefault(); }); });
  }

  // the exported files lose the -90 degree turn of the Unity prefabs: this puts every model back as it is in the game
  var UPRIGHT = "0deg -90deg 0deg";

  // The big frame at the top: the models of the game one after the other (a carousel). It can not be touched: it only turns, and
  // after a few seconds the next model fades in. Two viewers take turns, so the next model is ready before it shows.
  var hero = $("#heroOrb");
  function startHeroCarousel(first, models) {
    var frame = first.parentNode;
    var tag = $(".frame-tag", frame), credit = $(".frame-credit", frame), dots = $("#heroDots");
    var second = first.cloneNode(false);
    second.removeAttribute("id");
    second.classList.remove("on");
    frame.insertBefore(second, first.nextSibling);
    var views = [first, second], front = 0, next = 0;
    var SECONDS = 7;

    dots.innerHTML = models.map(function () { return "<i></i>"; }).join("");
    function mark(i) { Array.prototype.forEach.call(dots.children, function (d, k) { d.classList.toggle("on", k === i); }); }

    function step() {
      var m = models[next % models.length], idx = next % models.length;
      var back = views[front ^ 1], shown = views[front];
      var done = false;
      function show() {
        if (done) return;
        done = true;
        back.classList.add("on");
        shown.classList.remove("on");
        front ^= 1;
        var textNode = tag.lastChild;
        if (textNode) textNode.nodeValue = m.title;
        credit.textContent = "Model by " + (m.author || "May Thinzar Khin");
        mark(idx);
        next++;
        setTimeout(step, SECONDS * 1000);
      }
      back.addEventListener("load", show, { once: true });
      // an invisible viewer may not send "load": once the file is handed to it, it is also checked by itself
      // (and the model is shown anyway after 20 seconds)
      var handedAt = 0, started = Date.now();
      var watch = setInterval(function () {
        if (done) { clearInterval(watch); return; }
        var ready = handedAt && Date.now() - handedAt > 500 && back.loaded;
        if (ready || Date.now() - started > 20000) { clearInterval(watch); show(); }
      }, 250);
      loadModel(back, m.id).then(function () { handedAt = Date.now(); }).catch(function () {
        clearInterval(watch);
        if (done) return;
        done = true; next++;                       // this one did not load: the next one
        setTimeout(step, 1500);
      });
    }
    step();
  }
  if (hero) {
    guard(hero.parentNode);
    api("/api/models").then(function (r) {
      if (!r.ok || !r.models || !r.models.length) { hero.parentNode.style.display = "none"; return; }
      startHeroCarousel(hero, r.models);
    });
  }

  var soon = '<div class="model-soon reveal in"><div><svg class="ico" aria-hidden="true"><use href="#i-cube"/></svg><p>More models are coming.</p></div></div>';
  api("/api/models").then(function (r) {
    var grid = $("#gallery-grid");
    if (!r.ok || !r.models || !r.models.length) { grid.innerHTML = soon; return; }
    var html = "";
    r.models.forEach(function (m) {
      html += '<article class="model-card reveal"><div class="model-stage" data-id="' + esc(m.id) + '">' +
        '<div class="model-load">Loading the model</div>' +
        '<div class="watermark"><svg class="ico" aria-hidden="true"><use href="#i-lock"/></svg>&copy; ' + esc(m.year || 2026) + " " + esc(m.author || "May Thinzar Khin") + "</div></div>" +
        '<div class="model-info"><h3>' + esc(m.title) + "</h3><p>" + esc(m.description || "") + '</p><div class="tags">' +
        (m.tags || []).map(function (t) { return "<span>" + esc(t) + "</span>"; }).join("") +
        '</div><div class="model-by"><svg class="ico" aria-hidden="true"><use href="#i-user"/></svg>Art by ' + esc(m.author || "May Thinzar Khin") + "</div></div></article>";
    });
    html += soon;
    grid.innerHTML = html;
    $$(".model-card", grid).forEach(function (card) { if (io) io.observe(card); else card.classList.add("in"); });

    // each viewer loads when it comes near the screen
    var lazy = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        lazy.unobserve(en.target);
        var stage = en.target, id = stage.dataset.id;
        var mv = document.createElement("model-viewer");
        mv.setAttribute("alt", "A 3D model");
        mv.setAttribute("camera-controls", "");
        mv.setAttribute("auto-rotate", "");
        mv.setAttribute("rotation-per-second", "18deg");
        mv.setAttribute("interaction-prompt", "none");
        mv.setAttribute("touch-action", "pan-y");
        mv.setAttribute("shadow-intensity", "1");
        mv.setAttribute("exposure", "0.38");
        mv.setAttribute("environment-image", "neutral");
        mv.setAttribute("camera-orbit", "25deg 72deg auto");
        mv.setAttribute("orientation", UPRIGHT);
        mv.setAttribute("disable-pan", "");
        stage.insertBefore(mv, stage.firstChild);
        guard(stage);
        mv.addEventListener("load", function () { var l = $(".model-load", stage); if (l) l.remove(); });
        loadModel(mv, id).catch(function () { var l = $(".model-load", stage); if (l) l.textContent = "The model could not be loaded"; });
      });
    }, { rootMargin: "200px" });
    $$(".model-stage", grid).forEach(function (s) { lazy.observe(s); });
  });

  // ------------------------------------------------------------------------------------------ the game files (download once)
  var Game = (function () {
    var info = null;          // /play/version.json
    var swReady = false;
    var $pill = $("#cacheState"), $text = $("#cacheText"), $note = $("#cacheNote");
    var $meter = $("#cacheMeter"), $bar = $("#cacheBar"), $row = $("#cacheRow"), $pct = $("#cachePct"), $size = $("#cacheSize");
    var $prep = $("#prepareBtn"), $clear = $("#clearBtn");
    var secure = window.isSecureContext && "serviceWorker" in navigator && "caches" in window;

    function mb(n) { return (n / 1048576).toFixed(n > 10485760 ? 0 : 1) + " MB"; }
    function cookie(name) { var m = document.cookie.match(new RegExp("(?:^|; )" + name + "=([^;]*)")); return m ? decodeURIComponent(m[1]) : ""; }
    function setCookie(name, v) { document.cookie = name + "=" + encodeURIComponent(v) + "; Path=/; Max-Age=31536000; SameSite=Lax" + (location.protocol === "https:" ? "; Secure" : ""); }
    function state(kind, label, text) {
      $pill.className = "pill" + (kind ? " " + kind : "");
      $pill.textContent = label;
      $text.textContent = text;
    }

    function allCached() {
      if (!info || !secure) return Promise.resolve(false);
      return caches.open("lh-game").then(function (c) {
        return Promise.all(info.files.map(function (f) { return c.match(f.url); }));
      }).then(function (hits) { return hits.every(Boolean); }).catch(function () { return false; });
    }

    function refresh() {
      if (!info) { state("warn", "Offline", "The game files could not be checked."); return; }
      $size.textContent = mb(info.total);
      if (!secure) {
        // no service worker on plain http: the browser keeps the files by itself (they never change under one version)
        $prep.hidden = true;
        state("", "Browser cache", "The game is " + mb(info.total) + ". Your browser keeps it after the first visit.");
        $note.textContent = "The extra instant-start storage needs the secure address (https). A small cookie remembers your version.";
        return;
      }
      allCached().then(function (ok) {
        if (ok) {
          setCookie("lh_game_v", info.version);
          state("ok", "On this device", "The game files are saved here. The game starts at once, without a new download.");
          $prep.hidden = true; $clear.hidden = false;
        } else {
          var had = cookie("lh_game_v");
          state(had ? "warn" : "", had ? "Update ready" : "Not saved yet", "The game is " + mb(info.total) + ". Download it once now, or just press Play and it is saved as it loads.");
          $prep.hidden = false; $clear.hidden = true;
        }
        $meter.hidden = true; $row.hidden = true;
      });
    }

    function send(msg) {
      return navigator.serviceWorker.ready.then(function (reg) {
        (reg.active || navigator.serviceWorker.controller).postMessage(msg);
      });
    }

    function init() {
      fetch("/play/version.json", { cache: "no-store" }).then(function (r) { return r.ok ? r.json() : null; }).catch(function () { return null; }).then(function (v) {
        info = v;
        if (secure) {
          navigator.serviceWorker.register("/sw.js").then(function () { swReady = true; refresh(); }).catch(function () { secure = false; refresh(); });
          navigator.serviceWorker.addEventListener("message", function (ev) {
            var d = ev.data || {};
            if (d.type === "progress") {
              $meter.hidden = false; $row.hidden = false;
              var pct = Math.min(100, Math.round(d.done / Math.max(1, d.total) * 100));
              $bar.style.width = pct + "%";
              $pct.textContent = pct + "%";
              $size.textContent = mb(d.done) + " of " + mb(d.total);
              state("warn", "Downloading", "Saving the game on this device. You can keep this page open and read.");
            } else if (d.type === "done") {
              $prep.disabled = false;
              refresh();
            } else if (d.type === "error") {
              $prep.disabled = false;
              state("warn", "Stopped", "The download stopped (" + (d.message || "no connection") + "). Press Download now to continue.");
            } else if (d.type === "cleared") {
              setCookie("lh_game_v", "");
              refresh();
            }
          });
        } else {
          refresh();
        }
      });
    }

    $prep.addEventListener("click", function () {
      if (!info || !secure) return;
      $prep.disabled = true;
      $meter.hidden = false; $row.hidden = false;
      state("warn", "Downloading", "Starting");
      send({ type: "prepare", files: info.files, version: info.version });
    });
    $clear.addEventListener("click", function () { send({ type: "clear" }); });

    init();
    return { refresh: refresh };
  })();

  // ------------------------------------------------------------------------------------------ the license contact
  if (CONTACT_EMAIL) {
    var lc = $("#licenseContact");
    lc.innerHTML = 'To ask for written permission, write to <a href="mailto:' + esc(CONTACT_EMAIL) + '">' + esc(CONTACT_EMAIL) + "</a>.";
  }

  // ------------------------------------------------------------------------------------------ start
  setMode("login");
  openFromGame();
  loadMe();
  loadStats();
  loadBoard();
})();
