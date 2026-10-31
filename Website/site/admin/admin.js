/* Leikpyar Hunters - the admin panel (the browser side). No libraries. */
(function () {
  "use strict";

  // ------------------------------------------------------------------------------------------ helpers
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
  function fmt(n) { return Number(n || 0).toLocaleString("en-US"); }
  function ago(t) {
    if (!t) return "never";
    var s = Math.max(0, Math.floor(Date.now() / 1000 - t));
    if (s < 60) return "just now";
    if (s < 3600) return Math.floor(s / 60) + " min ago";
    if (s < 86400) return Math.floor(s / 3600) + " h ago";
    if (s < 86400 * 30) return Math.floor(s / 86400) + " d ago";
    return date(t);
  }
  function date(t) { return t ? new Date(t * 1000).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) : "-"; }
  function dateTime(t) { return t ? new Date(t * 1000).toLocaleString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "-"; }

  var ICONS = {
    dashboard: "M3 3h7v9H3zM14 3h7v5h-7zM14 12h7v9h-7zM3 16h7v5H3z",
    users: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8",
    text: "M4 7V4h16v3M9 20h6M12 4v16",
    image: "M3 5h18v14H3zM8.5 10a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3M21 15l-5-5L5 19",
    shield: "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6zM9 12l2 2 4-4",
    key: "M21 2l-2 2M15.5 7.5l3 3L22 7l-3-3M10.6 12.4a5.5 5.5 0 1 1-7.8 7.8 5.5 5.5 0 0 1 7.8-7.8zM10.6 12.4 15.5 7.5",
    activity: "M22 12h-4l-3 9L9 3l-3 9H2",
    logout: "M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9",
    ext: "M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6M15 3h6v6M10 14 21 3",
    search: "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM21 21l-4.3-4.3",
    download: "M12 3.5v11M7 10l5 5 5-5M4.5 20.5h15",
    menu: "M4 7h16M4 12h16M4 17h16",
    refresh: "M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7",
    upload: "M12 15V4M7 9l5-5 5 5M4.5 20.5h15"
  };
  function icon(name) { return '<svg class="ico" viewBox="0 0 24 24" aria-hidden="true"><path d="' + (ICONS[name] || "") + '"/></svg>'; }

  function toast(text, kind) {
    var t = document.createElement("div");
    t.className = "toast " + (kind || "");
    t.textContent = text;
    $("#toasts").appendChild(t);
    setTimeout(function () { t.remove(); }, kind === "bad" ? 6000 : 3200);
  }

  // ------------------------------------------------------------------------------------------ the api
  function api(path, body) {
    var init = { credentials: "same-origin", headers: {} };
    if (body !== undefined) {
      init.method = "POST";
      init.headers["Content-Type"] = "application/json";
      init.headers["X-LH-Admin"] = "1";
      init.body = JSON.stringify(body);
    }
    return fetch("/api/admin/" + path, init).then(function (r) {
      return r.json().catch(function () { return { ok: false, error: "Bad answer from the server." }; }).then(function (j) {
        if (r.status === 401 && path !== "login" && path !== "me") { showLogin(); }
        return j;
      });
    }).catch(function () { return { ok: false, error: "No connection to the server." }; });
  }

  // ------------------------------------------------------------------------------------------ modal
  function modal(opts) {
    return new Promise(function (resolve) {
      var bg = document.createElement("div");
      bg.className = "modal-bg";
      bg.innerHTML = '<div class="modal" role="dialog" aria-modal="true"><h2></h2><p></p><div class="mbody"></div><div class="btns"></div></div>';
      $("h2", bg).textContent = opts.title || "";
      $("p", bg).textContent = opts.text || "";
      if (!opts.text) $("p", bg).remove();
      var body = $(".mbody", bg), input = null;
      if (opts.input) {
        var label = document.createElement("label");
        label.className = "field";
        label.innerHTML = "<span></span><input>";
        $("span", label).textContent = opts.input.label || "";
        input = $("input", label);
        input.type = opts.input.type || "text";
        input.autocomplete = "off";
        input.value = opts.input.value || "";
        if (opts.input.placeholder) input.placeholder = opts.input.placeholder;
        body.appendChild(label);
      }
      var btns = $(".btns", bg);
      var cancel = document.createElement("button");
      cancel.className = "btn ghost"; cancel.type = "button"; cancel.textContent = "Cancel";
      var go = document.createElement("button");
      go.className = "btn " + (opts.danger ? "danger" : "primary"); go.type = "button"; go.textContent = opts.confirm || "OK";
      btns.appendChild(cancel); btns.appendChild(go);
      function close(v) { document.removeEventListener("keydown", onKey); bg.remove(); resolve(v); }
      function onKey(e) { if (e.key === "Escape") close(null); if (e.key === "Enter" && input) go.click(); }
      cancel.onclick = function () { close(null); };
      go.onclick = function () { close(input ? input.value : true); };
      bg.addEventListener("mousedown", function (e) { if (e.target === bg) close(null); });
      document.addEventListener("keydown", onKey);
      $("#modalRoot").appendChild(bg);
      (input || go).focus();
    });
  }

  // ------------------------------------------------------------------------------------------ charts (plain SVG)
  function niceMax(v) {
    if (v <= 4) return 4;
    var p = Math.pow(10, Math.floor(Math.log(v) / Math.LN10)), n = v / p;
    return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p;
  }

  function frame(W, H, pad, max, ticks) {
    var s = "";
    for (var i = 0; i <= ticks; i++) {
      var y = pad.t + (H - pad.t - pad.b) * (1 - i / ticks);
      s += '<line class="grid-line" x1="' + pad.l + '" x2="' + (W - pad.r) + '" y1="' + y + '" y2="' + y + '"/>';
      s += '<text x="' + (pad.l - 8) + '" y="' + (y + 4) + '" text-anchor="end">' + fmt(Math.round(max * i / ticks)) + "</text>";
    }
    return s;
  }

  // bars: [{l: label, v: value, tip: text}]
  function barChart(data, cls, h) {
    var W = 720, H = h || 220, pad = { l: 44, r: 8, t: 10, b: 26 };
    var max = niceMax(Math.max.apply(null, data.map(function (d) { return d.v; }).concat([1])));
    var inner = W - pad.l - pad.r, ih = H - pad.t - pad.b, n = data.length;
    var s = '<svg class="chart" viewBox="0 0 ' + W + " " + H + '" role="img">' + frame(W, H, pad, max, 4);
    var w = inner / n, bw = Math.max(2, Math.min(34, w * 0.66));
    var step = Math.max(1, Math.ceil(n / 9));
    data.forEach(function (d, i) {
      var x = pad.l + w * i + (w - bw) / 2, bh = ih * d.v / max;
      s += '<rect class="hit" x="' + (pad.l + w * i) + '" y="' + pad.t + '" width="' + w + '" height="' + ih + '" data-tip="' + esc(d.tip) + '"/>';
      if (d.v > 0) s += '<rect class="' + cls + '" x="' + x + '" y="' + (pad.t + ih - bh) + '" width="' + bw + '" height="' + bh + '" rx="3" pointer-events="none"/>';
      if (i % step === 0) s += '<text x="' + (pad.l + w * i + w / 2) + '" y="' + (H - 7) + '" text-anchor="middle">' + esc(d.l) + "</text>";
    });
    return s + "</svg>";
  }

  function lineChart(data, lineCls, areaCls, h) {
    var W = 720, H = h || 220, pad = { l: 44, r: 12, t: 12, b: 26 };
    var hi = Math.max.apply(null, data.map(function (d) { return d.v; }).concat([1]));
    var max = niceMax(hi);
    var inner = W - pad.l - pad.r, ih = H - pad.t - pad.b, n = data.length;
    var pts = data.map(function (d, i) {
      return [pad.l + (n === 1 ? inner / 2 : inner * i / (n - 1)), pad.t + ih - ih * d.v / max];
    });
    var s = '<svg class="chart" viewBox="0 0 ' + W + " " + H + '" role="img">' + frame(W, H, pad, max, 4);
    var path = pts.map(function (p, i) { return (i ? "L" : "M") + p[0].toFixed(1) + " " + p[1].toFixed(1); }).join(" ");
    s += '<path class="' + areaCls + '" d="' + path + " L" + pts[n - 1][0] + " " + (pad.t + ih) + " L" + pts[0][0] + " " + (pad.t + ih) + ' Z"/>';
    s += '<path class="' + lineCls + '" d="' + path + '" fill="none" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"/>';
    var step = Math.max(1, Math.ceil(n / 9)), w = inner / n;
    data.forEach(function (d, i) {
      s += '<rect class="hit" x="' + (pts[i][0] - w / 2) + '" y="' + pad.t + '" width="' + w + '" height="' + ih + '" data-tip="' + esc(d.tip) + '"/>';
      if (i % step === 0) s += '<text x="' + pts[i][0] + '" y="' + (H - 7) + '" text-anchor="middle">' + esc(d.l) + "</text>";
    });
    pts.length <= 40 && pts.forEach(function (p) { s += '<circle class="dot ' + lineCls + '" cx="' + p[0] + '" cy="' + p[1] + '" r="3" pointer-events="none"/>'; });
    return s + "</svg>";
  }

  function hbars(rows, max) {
    max = max || Math.max.apply(null, rows.map(function (r) { return r.count; }).concat([1]));
    return '<div class="hbars">' + rows.map(function (r) {
      return '<div class="hbar"><span>' + esc(r.label) + '</span><span class="track"><i class="fill" style="width:' + (100 * r.count / max) + '%"></i></span><span class="v">' + fmt(r.count) + "</span></div>";
    }).join("") + "</div>";
  }

  // one floating tip for every element with data-tip
  (function () {
    var tip = $("#tip");
    document.addEventListener("mousemove", function (e) {
      var t = e.target.closest ? e.target.closest("[data-tip]") : null;
      if (!t) { tip.hidden = true; return; }
      tip.textContent = t.getAttribute("data-tip");
      tip.hidden = false;
      var x = e.clientX + 14, y = e.clientY + 16;
      if (x + tip.offsetWidth > window.innerWidth - 8) x = e.clientX - tip.offsetWidth - 14;
      tip.style.left = x + "px"; tip.style.top = y + "px";
    });
    document.addEventListener("mouseleave", function () { tip.hidden = true; });
  })();

  // ------------------------------------------------------------------------------------------ the shell
  var me = null;
  var PAGES = [
    { id: "dashboard", title: "Dashboard", icon: "dashboard" },
    { id: "agents", title: "Agents", icon: "users" },
    { id: "texts", title: "Website texts", icon: "text" },
    { id: "photos", title: "Photos and creators", icon: "image" },
    { id: "pages", title: "Privacy and license", icon: "shield" },
    { id: "admins", title: "Admins", icon: "key" },
    { id: "activity", title: "Activity log", icon: "activity" }
  ];
  var current = null;
  var leave = null;             // called before the page changes (the text editor asks about unsaved changes)

  function showLogin() {
    me = null;
    $("#boot").hidden = true; $("#app").hidden = true; $("#login").hidden = false;
    $("#lPass").value = "";
    setTimeout(function () { $("#lEmail").focus(); }, 30);
  }

  function showApp() {
    $("#boot").hidden = true; $("#login").hidden = true; $("#app").hidden = false;
    $("#who").textContent = me.email;
    $("#nav").innerHTML = PAGES.map(function (p) { return '<a href="#/' + p.id + '" data-page="' + p.id + '">' + icon(p.icon) + esc(p.title) + "</a>"; }).join("");
    $("#viewSite").innerHTML = icon("ext") + "View the website";
    $("#logout").innerHTML = icon("logout") + "Sign out";
    $("#menuBtn").innerHTML = icon("menu");
    route();
  }

  function route() {
    if (!me) return;
    var id = (location.hash.replace(/^#\/?/, "") || "dashboard").split("?")[0];
    var page = PAGES.filter(function (p) { return p.id === id; })[0] || PAGES[0];
    if (current === page.id && !route.force) return;
    route.force = false;
    if (leave) {
      var l = leave;
      if (l() === false) { location.hash = "#/" + current; return; }
      leave = null;
    }
    current = page.id;
    $$("#nav a").forEach(function (a) { if (a.dataset.page === page.id) a.setAttribute("aria-current", "page"); else a.removeAttribute("aria-current"); });
    $("#pageTitle").textContent = page.title;
    $("#side").classList.remove("open");
    var view = $("#view");
    view.innerHTML = '<p class="muted">Loading</p>';
    window.scrollTo(0, 0);
    VIEWS[page.id](view);
  }
  window.addEventListener("hashchange", route);
  window.addEventListener("beforeunload", function (e) { if (leave && leave(true) === false) { e.preventDefault(); e.returnValue = ""; } });

  // ------------------------------------------------------------------------------------------ dashboard
  var days = 30;

  function dashboard(view) {
    api("overview?days=" + days).then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      var k = r.kpi, s = r.series;
      var kp = [
        ["Agents", k.agents, k.newToday + " new today"],
        ["New in 7 days", k.new7, ""],
        ["Signed in, 24 h", k.active24, ""],
        ["Signed in, 7 days", k.active7, ""],
        ["Have played", k.playing, Math.round(100 * k.playing / Math.max(1, k.agents)) + "% of agents"],
        ["Nights played", k.runs, k.runs7 + " in 7 days"],
        ["Souls sealed", k.souls, ""],
        ["Cages checked", k.cages, ""],
        ["Top score", k.topScore, "average " + fmt(k.avgScore)],
        ["Saved games", k.saves, ""],
        ["Blocked", k.blocked, ""],
        ["Admins", k.admins, ""]
      ];
      var short = function (d) { return d.day.slice(5); };
      var devTotal = Math.max(1, r.devices.mobile + r.devices.desktop);
      var pm = Math.round(100 * r.devices.mobile / devTotal), pd = 100 - pm;
      var hmax = Math.max.apply(null, r.hours.concat([1]));
      var g = r.genders || { male: 0, female: 0, unset: 0 };
      var gTotal = Math.max(1, g.male + g.female + g.unset);
      var gp = function (n) { return Math.round(100 * n / gTotal); };

      view.innerHTML =
        '<div class="toolbar"><div class="range" id="range">' +
        [7, 30, 90, 180].map(function (d) { return '<button type="button" data-d="' + d + '" aria-pressed="' + (d === days) + '">' + d + " days</button>"; }).join("") +
        '</div><span class="muted">Days are in Myanmar time (' + esc(r.tz) + ").</span>" +
        '<button class="btn ghost sm push" id="reload" type="button">' + icon("refresh") + "Refresh</button></div>" +
        '<dl class="grid kpis">' + kp.map(function (x) { return '<div class="kpi"><dt>' + esc(x[0]) + "</dt><dd>" + fmt(x[1]) + "</dd>" + (x[2] ? "<small>" + esc(x[2]) + "</small>" : "") + "</div>"; }).join("") + "</dl>" +
        '<div class="grid cols2">' +
        '<div class="card"><h2>New agents</h2><p class="sub">Accounts created each day</p>' + barChart(s.map(function (d) { return { l: short(d), v: d.signups, tip: d.day + ": " + d.signups + " new" }; }), "bar-red") + "</div>" +
        '<div class="card"><h2>All agents</h2><p class="sub">How many accounts there are</p>' + lineChart(s.map(function (d) { return { l: short(d), v: d.total, tip: d.day + ": " + fmt(d.total) + " agents" }; }), "line-red", "area-red") + "</div>" +
        '<div class="card"><h2>Active agents</h2><p class="sub">Agents who signed in or finished a night, each day</p>' + barChart(s.map(function (d) { return { l: short(d), v: d.active, tip: d.day + ": " + d.active + " active" }; }), "bar-cold") + "</div>" +
        '<div class="card"><h2>Nights played</h2><p class="sub">Scores sent by the game, each day</p>' + barChart(s.map(function (d) { return { l: short(d), v: d.runs, tip: d.day + ": " + d.runs + " nights, " + fmt(d.souls) + " souls" }; }), "bar-amber") + "</div>" +
        '<div class="card"><h2>Best score of each agent</h2><p class="sub">How many agents are in each range</p>' + hbars(r.ranges) + "</div>" +
        '<div class="card"><h2>When agents sign in</h2><p class="sub">Hour of the day, last 30 days</p>' + barChart(r.hours.map(function (v, h) { return { l: String(h), v: v, tip: (h < 10 ? "0" : "") + h + ":00 - " + v + " sign ins" }; }), "bar-green", 180) + "</div>" +
        '<div class="card"><h2>Devices</h2><p class="sub">Agents who signed in, last 30 days</p>' +
        '<div class="split"><i style="width:' + pm + '%;background:var(--amber)"></i><i style="width:' + pd + '%;background:var(--cold)"></i></div>' +
        '<div class="legend"><span><i class="dotk" style="background:var(--amber)"></i>Phone <b>' + fmt(r.devices.mobile) + "</b> (" + pm + '%)</span><span><i class="dotk" style="background:var(--cold)"></i>Computer <b>' + fmt(r.devices.desktop) + "</b> (" + pd + "%)</span></div>" +
        '<p class="hint">Devices are counted from the sign ins since this panel was installed.</p></div>' +
        '<div class="card"><h2>Male and female</h2><p class="sub">What the agents chose when they made their account</p>' +
        '<div class="split"><i style="width:' + (100 * g.male / gTotal) + '%;background:var(--cold)"></i><i style="width:' + (100 * g.female / gTotal) + '%;background:var(--blood-2)"></i><i style="width:' + (100 * g.unset / gTotal) + '%;background:var(--dim)"></i></div>' +
        '<div class="legend"><span><i class="dotk" style="background:var(--cold)"></i>Male <b>' + fmt(g.male) + "</b> (" + gp(g.male) + '%)</span><span><i class="dotk" style="background:var(--blood-2)"></i>Female <b>' + fmt(g.female) + "</b> (" + gp(g.female) + '%)</span><span><i class="dotk" style="background:var(--dim)"></i>Not chosen yet <b>' + fmt(g.unset) + "</b> (" + gp(g.unset) + "%)</span></div>" +
        '<p class="hint">Agents made before this choice existed are asked when they open their account.</p></div>' +
        '<div class="card"><h2>New agents</h2><p class="sub">The last 8</p>' + smallTable(["Agent", "Codename", "Joined"], r.recent.map(function (u) { return [u.agentId, esc(u.codename), ago(u.created)]; })) + "</div>" +
        "</div>" +
        '<div class="card" style="margin-top:14px"><h2>Top 10</h2><p class="sub">The best nights</p>' +
        smallTable(["#", "Agent", "Codename", "Score", "Souls", "Nights"], r.top.map(function (u, i) { return [i + 1, u.agentId, esc(u.codename), fmt(u.score), fmt(u.souls), fmt(u.nights)]; }), [3, 4, 5]) + "</div>";

      $$("#range button").forEach(function (b) { b.onclick = function () { days = +b.dataset.d; dashboard(view); }; });
      $("#reload").onclick = function () { dashboard(view); };
    });
  }

  function smallTable(head, rows, numCols) {
    numCols = numCols || [];
    if (!rows.length) return '<p class="empty">Nothing yet.</p>';
    return '<div class="table-wrap"><table><thead><tr>' + head.map(function (h, i) { return '<th class="' + (numCols.indexOf(i) >= 0 ? "num" : "") + '">' + esc(h) + "</th>"; }).join("") + "</tr></thead><tbody>" +
      rows.map(function (r) { return "<tr>" + r.map(function (c, i) { return '<td class="' + (numCols.indexOf(i) >= 0 ? "num" : "") + '">' + c + "</td>"; }).join("") + "</tr>"; }).join("") + "</tbody></table></div>";
  }

  // ------------------------------------------------------------------------------------------ agents
  var ag = { q: "", filter: "all", sort: "created", dir: "desc", page: 1 };

  function agentsPage(view) {
    view.innerHTML =
      '<div class="toolbar">' +
      '<div class="grow"><input type="search" id="aq" placeholder="Search a codename or an Agent ID" value="' + esc(ag.q) + '"></div>' +
      '<select id="af" aria-label="Filter"><option value="all">All agents</option><option value="new7">New in 7 days</option><option value="scored">With a score</option><option value="noscore">No score yet</option><option value="blocked">Blocked</option></select>' +
      '<select id="as" aria-label="Sort"><option value="created">Newest first</option><option value="last">Last sign in</option><option value="score">Best score</option><option value="runs">Nights played</option><option value="name">Codename</option></select>' +
      '<a class="btn ghost" id="acsv" href="#">' + icon("download") + "Export CSV</a></div>" +
      '<div id="atable"></div><div class="pager" id="apager"></div>';
    $("#af").value = ag.filter; $("#as").value = ag.sort;
    var timer = null;
    $("#aq").oninput = function () { clearTimeout(timer); timer = setTimeout(function () { ag.q = $("#aq").value.trim(); ag.page = 1; loadAgents(); }, 260); };
    $("#af").onchange = function () { ag.filter = this.value; ag.page = 1; loadAgents(); };
    $("#as").onchange = function () { ag.sort = this.value; ag.dir = this.value === "name" ? "asc" : "desc"; ag.page = 1; loadAgents(); };
    loadAgents();
  }

  function agentQuery(withPage) {
    return "q=" + encodeURIComponent(ag.q) + "&filter=" + ag.filter + "&sort=" + ag.sort + "&dir=" + ag.dir + (withPage ? "&page=" + ag.page : "");
  }

  function loadAgents() {
    $("#acsv").href = "/api/admin/users.csv?" + agentQuery(false);
    api("users?" + agentQuery(true)).then(function (r) {
      var box = $("#atable");
      if (!box) return;
      if (!r.ok) { box.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      if (!r.users.length) { box.innerHTML = '<div class="table-wrap"><p class="empty">No agent found.</p></div>'; $("#apager").innerHTML = ""; return; }
      box.innerHTML = '<div class="table-wrap"><table><thead><tr><th>Agent</th><th>Codename</th><th>Gender</th><th>Joined</th><th>Last sign in</th><th class="num">Best score</th><th class="num">Souls</th><th class="num">Nights</th><th class="num">Runs</th><th>Status</th><th></th></tr></thead><tbody>' +
        r.users.map(function (u) {
          return '<tr data-id="' + u.id + '"><td>' + esc(u.agentId) + "</td><td><b>" + esc(u.codename) + "</b></td><td>" + (u.gender === "male" ? "Male" : u.gender === "female" ? "Female" : '<span class="muted">-</span>') + "</td><td>" + date(u.created) + "</td><td>" + ago(u.lastLogin) + '</td><td class="num">' + fmt(u.score) +
            '</td><td class="num">' + fmt(u.souls) + '</td><td class="num">' + fmt(u.nights) + '</td><td class="num">' + fmt(u.runs) + "</td><td>" + (u.blocked ? '<span class="pill red">Blocked</span>' : '<span class="pill green">Active</span>') +
            '</td><td class="actions"><button class="btn sm" data-act="block" type="button">' + (u.blocked ? "Unblock" : "Block") + '</button><button class="btn sm" data-act="password" type="button">New password</button><button class="btn sm" data-act="signout" type="button">Sign out</button><button class="btn sm danger" data-act="delete" type="button">Delete</button></td></tr>';
        }).join("") + "</tbody></table></div>";
      $("#apager").innerHTML = '<button class="btn sm" id="pp" type="button"' + (r.page <= 1 ? " disabled" : "") + '>Previous</button><span>Page ' + r.page + " of " + r.pages + " (" + fmt(r.total) + ' agents)</span><button class="btn sm" id="pn" type="button"' + (r.page >= r.pages ? " disabled" : "") + ">Next</button>";
      $("#pp").onclick = function () { ag.page--; loadAgents(); };
      $("#pn").onclick = function () { ag.page++; loadAgents(); };
      var byId = {}; r.users.forEach(function (u) { byId[u.id] = u; });
      $$("#atable [data-act]").forEach(function (b) {
        b.onclick = function () { agentAction(b.dataset.act, byId[+b.closest("tr").dataset.id]); };
      });
    });
  }

  function agentAction(act, u) {
    var done = function (r, msg) { if (r.ok) { toast(msg, "good"); loadAgents(); } else toast(r.error, "bad"); };
    if (act === "block") {
      modal({ title: (u.blocked ? "Unblock " : "Block ") + u.codename + "?", text: u.blocked ? "The agent can sign in again." : "The agent is signed out and can not sign in or send scores.", confirm: u.blocked ? "Unblock" : "Block", danger: !u.blocked })
        .then(function (v) { if (v) api("users/block", { id: u.id, blocked: !u.blocked }).then(function (r) { done(r, u.blocked ? "Unblocked" : "Blocked"); }); });
    } else if (act === "signout") {
      modal({ title: "Sign " + u.codename + " out everywhere?", text: "Every phone and computer of the agent is signed out. The password stays.", confirm: "Sign out" })
        .then(function (v) { if (v) api("users/signout", { id: u.id }).then(function (r) { done(r, "Signed out"); }); });
    } else if (act === "password") {
      modal({ title: "New password for " + u.codename, text: "6 to 64 characters. Tell the agent the new password. The agent is signed out everywhere.", input: { label: "New password", type: "text" }, confirm: "Change" })
        .then(function (v) { if (v) api("users/password", { id: u.id, password: v }).then(function (r) { done(r, "Password changed"); }); });
    } else if (act === "delete") {
      modal({ title: "Delete " + u.codename + "?", text: "The agent, its scores and its saved game are removed for good. This can not be undone. Type the codename to confirm.", input: { label: "Codename", placeholder: u.codename }, confirm: "Delete for good", danger: true })
        .then(function (v) {
          if (v === null) return;
          if (String(v).trim().toLowerCase() !== u.codename.toLowerCase()) { toast("The codename did not match. Nothing was deleted.", "bad"); return; }
          api("users/delete", { id: u.id }).then(function (r) { done(r, "Deleted"); });
        });
    }
  }

  // ------------------------------------------------------------------------------------------ website texts
  function textsPage(view) {
    api("texts").then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      var items = r.items;
      items.forEach(function (it) {
        it.curEn = it.en || it.key; it.curMy = it.my;
        it.origEn = it.curEn; it.origMy = it.curMy;
        it.edited = !!it.edited;
      });
      var dirty = {};
      var sections = [];
      items.forEach(function (it) { if (sections.indexOf(it.section) < 0) sections.push(it.section); });
      var state = { q: "", section: "", filter: "all" };

      view.innerHTML =
        '<p class="muted" style="margin-top:0">Every text of the website is here, in English and in Burmese. Change a text and press Save. An empty Burmese box shows the English text in Burmese mode. Changes appear on the website at once (reload the page).</p>' +
        '<div class="toolbar"><div class="grow"><input type="search" id="tq" placeholder="Search in English or Burmese"></div>' +
        '<select id="ts" aria-label="Section"><option value="">All sections</option>' + sections.map(function (s) { return "<option>" + esc(s) + "</option>"; }).join("") + "</select>" +
        '<select id="tf" aria-label="Filter"><option value="all">All texts</option><option value="edited">Edited</option><option value="nomy">No Burmese</option></select>' +
        '<a class="btn ghost" href="/" target="_blank" rel="noopener">' + icon("ext") + "Open the website</a></div>" +
        '<div id="tlist"></div>' +
        '<div class="savebar" id="tsave"><span class="grow" id="tcount">No changes</span><button class="btn ghost" id="tresetall" type="button">Reset all texts</button><button class="btn primary" id="tgo" type="button" disabled>Save changes</button></div>';

      function matches(it) {
        if (state.section && it.section !== state.section) return false;
        if (state.filter === "edited" && !(it.edited || dirty[it.key])) return false;
        if (state.filter === "nomy" && it.curMy) return false;
        if (state.q) {
          var q = state.q.toLowerCase();
          if (it.key.toLowerCase().indexOf(q) < 0 && it.curEn.toLowerCase().indexOf(q) < 0 && it.curMy.toLowerCase().indexOf(q) < 0) return false;
        }
        return true;
      }

      function rowHtml(it, idx) {
        var rows = function (s) { return Math.max(1, Math.min(9, Math.ceil(s.length / 48) + (s.split("\n").length - 1))); };
        var changed = it.curEn !== it.origEn || it.curMy !== it.origMy;
        return '<div class="trow' + (changed ? " changed" : "") + '" data-i="' + idx + '">' +
          '<label><span>English</span><textarea class="en" rows="' + rows(it.curEn) + '"' + (it.template ? " disabled" : "") + ">" + esc(it.curEn) + "</textarea></label>" +
          '<label><span>Burmese</span><textarea class="my" lang="my" rows="' + rows(it.curMy || it.curEn) + '">' + esc(it.curMy) + "</textarea></label>" +
          '<div class="meta">' +
          (it.dynamic ? '<span class="pill amber">Shown by the page</span>' : (it.uses > 1 ? '<span class="pill">used ' + it.uses + " times</span>" : "")) +
          (it.template ? '<span class="pill">has {0} - keep it in the Burmese text</span>' : "") +
          (it.kind !== "text" && !it.dynamic ? '<span class="pill">' + esc(it.kind === "attr" ? "label" : it.kind) + "</span>" : "") +
          (it.curEn !== it.key ? '<span class="orig" title="' + esc(it.key) + '">was: ' + esc(it.key) + "</span>" : "") +
          '<button class="btn sm ghost" data-reset type="button" style="margin-left:auto"' + ((it.curEn !== it.key || it.curMy !== it.defMy) ? "" : " hidden") + ">Back to the original</button></div></div>";
      }

      function render() {
        var groups = {}, order = [];
        items.forEach(function (it, i) {
          if (!matches(it)) return;
          if (!groups[it.section]) { groups[it.section] = []; order.push(it.section); }
          groups[it.section].push(i);
        });
        var open = state.q || state.section || state.filter !== "all";
        $("#tlist").innerHTML = order.length ? order.map(function (sec, n) {
          return '<details class="sec-group"' + (open || n === 0 ? " open" : "") + "><summary>" + esc(sec) + ' <span class="count">' + groups[sec].length + ' texts</span></summary><div class="rows">' +
            groups[sec].map(function (i) { return rowHtml(items[i], i); }).join("") + "</div></details>";
        }).join("") : '<div class="table-wrap"><p class="empty">No text found.</p></div>';
        $$("#tlist textarea").forEach(grow);
      }

      function grow(t) { t.style.height = "auto"; t.style.height = Math.max(40, t.scrollHeight + 2) + "px"; }

      function count() {
        var n = Object.keys(dirty).length;
        $("#tcount").textContent = n ? n + " unsaved change" + (n > 1 ? "s" : "") : "No changes";
        $("#tgo").disabled = !n;
      }

      $("#tlist").addEventListener("input", function (e) {
        var ta = e.target;
        if (ta.tagName !== "TEXTAREA") return;
        var row = ta.closest(".trow"), it = items[+row.dataset.i];
        if (ta.classList.contains("en")) it.curEn = ta.value; else it.curMy = ta.value;
        var changed = it.curEn !== it.origEn || it.curMy !== it.origMy;
        if (changed) dirty[it.key] = it; else delete dirty[it.key];
        row.classList.toggle("changed", changed);
        $("[data-reset]", row).hidden = !(it.curEn !== it.key || it.curMy !== it.defMy);
        grow(ta); count();
      });

      $("#tlist").addEventListener("click", function (e) {
        var b = e.target.closest("[data-reset]");
        if (!b) return;
        var row = b.closest(".trow"), it = items[+row.dataset.i];
        it.curEn = it.key; it.curMy = it.defMy;
        if (it.curEn !== it.origEn || it.curMy !== it.origMy) dirty[it.key] = it; else delete dirty[it.key];
        row.outerHTML = rowHtml(it, +row.dataset.i);
        $$("#tlist textarea").forEach(grow);
        count();
      });

      var timer = null;
      $("#tq").oninput = function () { clearTimeout(timer); timer = setTimeout(function () { state.q = $("#tq").value.trim(); render(); }, 220); };
      $("#ts").onchange = function () { state.section = this.value; render(); };
      $("#tf").onchange = function () { state.filter = this.value; render(); };

      $("#tgo").onclick = function () {
        var payload = Object.keys(dirty).map(function (k) { var it = dirty[k]; return { key: it.key, en: it.curEn, my: it.curMy }; });
        this.disabled = true;
        api("texts", { items: payload }).then(function (r) {
          if (!r.ok) { toast(r.error, "bad"); $("#tgo").disabled = false; return; }
          Object.keys(dirty).forEach(function (k) {
            var it = dirty[k];
            it.origEn = it.curEn; it.origMy = it.curMy;
            it.edited = it.curEn !== it.key || it.curMy !== it.defMy;
          });
          dirty = {};
          toast("Saved. Reload the website to see it.", "good");
          render(); count();
        });
      };

      $("#tresetall").onclick = function () {
        modal({ title: "Reset every text?", text: "All the English and Burmese edits are removed and the website goes back to the texts it came with.", confirm: "Reset all", danger: true }).then(function (v) {
          if (!v) return;
          api("texts/reset", { all: true }).then(function (r) { if (r.ok) { toast("All texts are back to the original", "good"); leave = null; route.force = true; route(); } else toast(r.error, "bad"); });
        });
      };

      leave = function (silent) {
        if (!Object.keys(dirty).length) return true;
        if (silent) return false;
        return window.confirm("You have unsaved text changes. Leave this page and lose them?");
      };
      render(); count();
    });
  }

  // ------------------------------------------------------------------------------------------ photos
  function photosPage(view) {
    api("images").then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      var crew = r.images.filter(function (i) { return i.key.indexOf("/img/crew/") === 0; });
      var other = r.images.filter(function (i) { return i.key.indexOf("/img/crew/") !== 0; });

      function card(im) {
        var src = im.custom || im.key;
        return '<div class="pic" data-key="' + esc(im.key) + '"><div class="thumb"><img src="' + esc(src) + (im.custom ? "" : "?t=" + Date.now()) + '" alt=""></div>' +
          "<h3>" + esc(im.alt || im.key.split("/").pop()) + "</h3>" +
          '<div class="path">' + esc(im.key) + (im.custom ? " - replaced" : "") + "</div>" +
          '<div class="btns"><label class="btn sm primary" style="cursor:pointer">' + icon("upload") + 'Replace<input type="file" accept="image/png,image/jpeg,image/webp' + (im.key.slice(-4) === ".ico" ? ",.ico" : "") + '" hidden></label>' +
          '<button class="btn sm" data-reset type="button"' + (im.custom ? "" : " hidden") + ">Back to the original</button></div></div>";
      }

      view.innerHTML =
        '<p class="muted" style="margin-top:0">Replace a picture of the website. Creator photos are cropped to a square automatically. Use a PNG, JPG or WebP picture. The names and texts of the creators are in <a href="#/texts">Website texts</a>, section "Credits and license".</p>' +
        '<h2 style="margin:18px 0 10px">Creators</h2><div class="grid pics">' + crew.map(card).join("") + "</div>" +
        '<div class="card" style="margin-top:14px"><h2>Links of the creators</h2><p class="sub">A web address for each creator (a portfolio, a profile). On the website the name becomes a link that opens in a new tab. Leave it empty for no link.</p>' +
        '<div id="creatorLinks">' + crew.map(function (im) {
          var slot = im.key.split("/").pop().replace(/\.[^.]+$/, "");
          return '<label class="field"><span>' + esc(im.alt || slot) + '</span><input data-slot="' + esc(slot) + '" maxlength="300" placeholder="https://..."></label>';
        }).join("") + '</div><button class="btn primary" id="creatorLinksSave" type="button">Save the links</button></div>' +
        '<h2 style="margin:26px 0 10px">Logo and icons</h2><div class="grid pics">' + other.map(card).join("") + "</div>" +
        '<h2 style="margin:26px 0 4px">More contributors</h2>' +
        '<p class="muted" style="margin:0 0 12px">Small boxes under the four creators on the website: a picture on the left, then a label, the name and a copyright line (for example label "Song", name "Wai Yan", line "&copy; 2026 Wai Yan"). Press Add or Edit to open the form. Every change is saved at once.</p>' +
        '<div id="credBox"></div>' +
        '<h2 style="margin:30px 0 4px">Special thanks</h2>' +
        '<p class="muted" style="margin:0 0 12px">A separate list under the contributors, for the people or places that gave you pictures or sounds. A box can have a list of sounds (a title and the artist of each): on the website the card opens to show them.</p>' +
        '<div id="thxBox"></div>' +
        '<p class="hint">The creator photos in the game (the Credits screen) are part of the game build and are not changed here.</p>';
      boxesEditor($("#credBox"), { kind: "credits", noun: "contributor", sounds: false, empty: "No more contributors yet." });

      api("credits").then(function (res) {
        var links = (res && res.links) || {};
        $$("#creatorLinks input").forEach(function (inp) { inp.value = links[inp.dataset.slot] || ""; });
      });
      $("#creatorLinksSave").onclick = function () {
        var map = {};
        $$("#creatorLinks input").forEach(function (inp) { if (inp.value.trim()) map[inp.dataset.slot] = inp.value.trim(); });
        api("creators/links", { links: map }).then(function (res) {
          if (res.ok) toast("Saved. Reload the website to see it.", "good"); else toast(res.error, "bad");
        });
      };
      boxesEditor($("#thxBox"), { kind: "thanks", noun: "special thanks box", sounds: true, empty: "No special thanks yet." });

      $$(".pic").forEach(function (box) {
        var key = box.dataset.key;
        var input = $("input[type=file]", box);
        input.onchange = function () {
          var file = input.files && input.files[0];
          if (!file) return;
          prepareImage(file, key.indexOf("/img/crew/") === 0).then(function (b64) {
            return api("images", { key: key, data: b64 });
          }).then(function (res) {
            if (!res.ok) { toast(res.error, "bad"); return; }
            toast("Picture saved. Reload the website to see it.", "good");
            route.force = true; route();
          }).catch(function (e) { toast(e.message || "The picture could not be read.", "bad"); });
        };
        $("[data-reset]", box).onclick = function () {
          api("images/reset", { key: key }).then(function (res) { if (res.ok) { toast("Back to the original picture", "good"); route.force = true; route(); } else toast(res.error, "bad"); });
        };
      });
    });
  }

  // a creator photo is made square (480 x 480, WebP); other pictures are sent as they are
  function prepareImage(file, square, size) {
    return new Promise(function (resolve, reject) {
      if (!square) {
        if (file.size > 700 * 1024) { reject(new Error("The picture is too big (700 KB at most).")); return; }
        var fr = new FileReader();
        fr.onload = function () { resolve(String(fr.result).split(",")[1]); };
        fr.onerror = function () { reject(new Error("The picture could not be read.")); };
        fr.readAsDataURL(file);
        return;
      }
      var url = URL.createObjectURL(file), img = new Image();
      img.onload = function () {
        size = size || 480;
        var c = document.createElement("canvas");
        c.width = c.height = size;
        var ctx = c.getContext("2d"), side = Math.min(img.width, img.height);
        ctx.imageSmoothingQuality = "high";
        ctx.drawImage(img, (img.width - side) / 2, (img.height - side) / 2, side, side, 0, 0, size, size);
        URL.revokeObjectURL(url);
        c.toBlob(function (blob) {
          if (!blob) { reject(new Error("The picture could not be converted.")); return; }
          var fr = new FileReader();
          fr.onload = function () { resolve(String(fr.result).split(",")[1]); };
          fr.readAsDataURL(blob);
        }, "image/webp", 0.9);
      };
      img.onerror = function () { URL.revokeObjectURL(url); reject(new Error("This file is not a picture.")); };
      img.src = url;
    });
  }

  // ------------------------------------------------------------------------------------------ more contributors and special thanks
  // A list with Add and Edit: the form opens only then, and every change is saved at once.
  function boxesEditor(box, cfg) {
    var items = [];
    var kind = cfg.kind;          // "credits" or "thanks": the name of the list on the server

    function blank() {
      var b = { name: "", role: "", roleMy: "", note: "", noteMy: "", image: "", link: "" };
      if (cfg.sounds) b.sounds = [];
      return b;
    }
    function count(c) {
      var n = (c.sounds || []).length, f = (c.sounds || []).filter(function (s) { return s.file; }).length;
      return n ? n + (n === 1 ? " sound" : " sounds") + (f ? ", " + f + " with a file" : "") : "";
    }

    function mini(c) {
      return '<article class="credit-mini' + (c.image ? "" : " noimg") + '">' + (c.image ? '<img src="' + esc(c.image) + '" alt="">' : "") +
        "<div>" + (c.role ? "<span>" + esc(c.role) + "</span>" : "") + "<h4" + (c.link ? ' class="ext"' : "") + ">" + esc(c.name) + "</h4>" + (c.note ? "<small>" + esc(c.note) + "</small>" : "") +
        (count(c) ? "<small>" + count(c) + " (opens in the card)</small>" : "") + "</div></article>";
    }

    function draw() {
      box.innerHTML = (items.length ? '<div class="lrows">' + items.map(function (c, i) {
        return '<div class="lrow" data-i="' + i + '">' + (c.image ? '<img src="' + esc(c.image) + '" alt="">' : '<div class="none"></div>') +
          '<div class="lmain"><b>' + esc(c.name) + "</b><span>" + esc([c.role, c.note].filter(Boolean).join("  -  ") || "-") + "</span>" + (c.link ? '<span class="pill green">link</span> ' : "") + (count(c) ? '<span class="pill">' + count(c) + "</span>" : "") + "</div>" +
          '<div class="lbtns"><button class="btn sm" data-act="edit" type="button">Edit</button><button class="btn sm" data-act="up" type="button"' + (i === 0 ? " disabled" : "") + '>Up</button><button class="btn sm" data-act="down" type="button"' + (i === items.length - 1 ? " disabled" : "") + '>Down</button><button class="btn sm danger" data-act="del" type="button">Remove</button></div></div>';
      }).join("") + "</div>" : '<div class="table-wrap"><p class="empty">' + esc(cfg.empty) + "</p></div>") +
        '<div class="toolbar" style="margin-top:12px"><button class="btn primary" data-act="add" type="button">Add a ' + esc(cfg.noun) + "</button></div>" +
        (items.length ? '<h3 style="margin:16px 0 10px;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:var(--muted)">How it looks on the website</h3><div class="credits-grid">' + items.map(mini).join("") + "</div>" : "");
    }

    // saves a new list; ok = the server took it
    function persist(list, done) {
      api(kind, { items: list }).then(function (r) {
        if (r.ok) { items = list; toast("Saved. Reload the website to see it.", "good"); draw(); }
        done(r);
      });
    }

    box.addEventListener("click", function (e) {
      var b = e.target.closest("[data-act]");
      if (!b) return;
      var act = b.dataset.act, row = b.closest(".lrow"), i = row ? +row.dataset.i : -1;
      if (act === "add") { openForm(-1); return; }
      if (act === "edit") { openForm(i); return; }
      var list = items.slice();
      if (act === "up" && i > 0) list.splice(i - 1, 0, list.splice(i, 1)[0]);
      else if (act === "down" && i < list.length - 1) list.splice(i + 1, 0, list.splice(i, 1)[0]);
      else if (act === "del") {
        modal({ title: "Remove " + items[i].name + "?", text: "The box is removed from the website.", confirm: "Remove", danger: true }).then(function (v) {
          if (!v) return;
          list.splice(i, 1);
          persist(list, function (r) { if (!r.ok) toast(r.error, "bad"); });
        });
        return;
      } else return;
      persist(list, function (r) { if (!r.ok) toast(r.error, "bad"); });
    });

    function openForm(index) {
      var it = index < 0 ? blank() : JSON.parse(JSON.stringify(items[index]));
      if (cfg.sounds && !it.sounds) it.sounds = [];
      var bg = document.createElement("div");
      bg.className = "modal-bg";
      bg.innerHTML = '<div class="modal wide" role="dialog" aria-modal="true"><h2></h2><div class="fbody"></div><p class="msg" id="fMsg" role="alert"></p>' +
        '<div class="btns"><button class="btn ghost" data-x="cancel" type="button">Cancel</button><button class="btn primary" data-x="save" type="button">Save</button></div></div>';
      $("h2", bg).textContent = (index < 0 ? "Add a " : "Edit the ") + cfg.noun;
      var fb = $(".fbody", bg);
      fb.innerHTML =
        '<div class="fgrid"><div class="fpic" id="fPic"></div><div>' +
        '<label class="field"><span>Name</span><input data-f="name" maxlength="60" value="' + esc(it.name) + '"></label>' +
        '<label class="field"><span>Link (optional): a portfolio, or the page of the asset</span><input data-f="link" maxlength="300" placeholder="https://..." value="' + esc(it.link || "") + '"></label>' +
        '<div class="in2"><label class="field"><span>Label (English)</span><input data-f="role" maxlength="40" placeholder="' + (cfg.sounds ? "SFX" : "Song") + '" value="' + esc(it.role) + '"></label>' +
        '<label class="field"><span>Label (Burmese)</span><input data-f="roleMy" class="my" lang="my" maxlength="80" value="' + esc(it.roleMy) + '"></label></div>' +
        '<div class="in2"><label class="field"><span>Copyright line (English)</span><input data-f="note" maxlength="120" placeholder="&copy; 2026 Name" value="' + esc(it.note) + '"></label>' +
        '<label class="field"><span>Copyright line (Burmese)</span><input data-f="noteMy" class="my" lang="my" maxlength="240" value="' + esc(it.noteMy) + '"></label></div></div></div>' +
        (cfg.sounds ? '<div class="sounds-ed"><h3>Sounds</h3><p class="hint" style="margin:0 0 10px">The sounds this person or place gave you. On the website the card opens and shows each title with its artist.</p>' +
          '<div id="sRows"></div><div class="toolbar" style="margin:10px 0 0"><button class="btn sm" data-x="addSound" type="button">Add a sound</button>' +
          '<label class="btn sm primary" style="cursor:pointer">' + icon("upload") + 'Upload sound files<input type="file" id="sFiles" multiple accept="audio/*,.mp3,.ogg,.wav,.m4a" hidden></label>' +
          '<span class="hint" style="margin:0">MP3, OGG, WAV or M4A, 10 MB at most for each file. Each file becomes a sound with its file name as the title.</span></div>' +
          '<details style="margin-top:12px"><summary class="hint" style="cursor:pointer">Paste a list of sounds</summary><textarea id="sPaste" rows="6" placeholder="One sound on each line:&#10;Door creak - Artist name&#10;Wind at night - Another artist"></textarea>' +
          '<div class="toolbar" style="margin:8px 0 0"><button class="btn sm" data-x="paste" type="button">Add these sounds</button></div></details></div>' : "");
      document.getElementById("modalRoot").appendChild(bg);

      function drawPic() {
        $("#fPic", bg).innerHTML = (it.image ? '<img src="' + esc(it.image) + '" alt="">' : '<div class="none">No picture</div>') +
          '<label class="btn sm" style="cursor:pointer">' + icon("upload") + 'Picture<input type="file" accept="image/png,image/jpeg,image/webp" hidden></label>' +
          (it.image ? '<button class="btn sm ghost" data-x="nopic" type="button">Remove picture</button>' : "");
        var file = $("input[type=file]", bg);
        file.onchange = function () {
          if (!file.files || !file.files[0]) return;
          prepareImage(file.files[0], true, 192).then(function (b64) { return api("credits/image", { data: b64 }); }).then(function (res) {
            if (!res.ok) { $("#fMsg", bg).textContent = res.error; return; }
            $("#fMsg", bg).textContent = ""; it.image = res.url; drawPic();
          }).catch(function (e) { $("#fMsg", bg).textContent = e.message || "The picture could not be read."; });
        };
      }

      function drawSounds(focusLast) {
        if (!cfg.sounds) return;
        var rows = $("#sRows", bg);
        rows.innerHTML = it.sounds.length ? it.sounds.map(function (s, j) {
          return '<div class="srow" data-j="' + j + '"><input data-s="title" maxlength="90" placeholder="Sound title" value="' + esc(s.title) + '"><input data-s="artist" maxlength="70" placeholder="Artist" value="' + esc(s.artist) + '">' +
            '<div class="sact"><label class="btn sm" style="cursor:pointer">' + icon("upload") + (s.file ? "Change the sound file" : "Sound file") + '<input type="file" data-audio accept="audio/*,.mp3,.ogg,.wav,.m4a" hidden></label>' +
            (s.file ? '<audio controls preload="none" src="' + esc(s.file) + '"></audio><button class="btn sm ghost" data-x="unfile" type="button">Remove the file</button>' : '<span class="hint" style="margin:0">No file: the title is only listed.</span>') +
            '<button class="btn sm danger" data-x="delSound" type="button" style="margin-left:auto">Remove the sound</button></div></div>';
        }).join("") : '<p class="hint" style="margin:0">No sounds yet.</p>';
        if (focusLast) { var last = $$("input[data-s=title]", rows).pop(); if (last) last.focus(); }
      }

      drawPic(); drawSounds(false);

      bg.addEventListener("input", function (e) {
        var t = e.target;
        if (t.dataset.f) it[t.dataset.f] = t.value;
        else if (t.dataset.s) it.sounds[+t.closest(".srow").dataset.j][t.dataset.s] = t.value;
      });

      // sound files: one for a row, or many at once (each becomes a new sound)
      function readB64(file) {
        return new Promise(function (resolve, reject) {
          var fr = new FileReader();
          fr.onload = function () { resolve(String(fr.result).split(",")[1]); };
          fr.onerror = function () { reject(new Error("The file could not be read.")); };
          fr.readAsDataURL(file);
        });
      }
      function sendSound(file) {
        if (file.size > 10 * 1024 * 1024) return Promise.reject(new Error(file.name + " is too big (10 MB at most)."));
        return readB64(file).then(function (b64) { return api("credits/audio", { data: b64 }); }).then(function (res) {
          if (!res.ok) throw new Error(file.name + ": " + res.error);
          return res.url;
        });
      }
      bg.addEventListener("change", function (e) {
        var t = e.target, msg = $("#fMsg", bg);
        if (t.matches && t.matches("input[data-audio]")) {
          var row = t.closest(".srow"), j = +row.dataset.j;
          if (!t.files || !t.files[0]) return;
          msg.textContent = "Uploading " + t.files[0].name + " ...";
          sendSound(t.files[0]).then(function (url) {
            it.sounds[j].file = url;
            if (!it.sounds[j].title.trim()) it.sounds[j].title = t.files[0].name.replace(/\.[^.]+$/, "");
            msg.textContent = ""; drawSounds(false);
          }).catch(function (err) { msg.textContent = err.message; });
        } else if (t.id === "sFiles") {
          var files = Array.prototype.slice.call(t.files || []);
          if (!files.length) return;
          var done = 0, failed = [];
          var next = function () {
            if (!files.length) {
              msg.textContent = failed.join(" ");
              if (done) toast(done + " sound file" + (done === 1 ? "" : "s") + " added", "good");
              drawSounds(false); t.value = ""; return;
            }
            var f = files.shift();
            msg.textContent = "Uploading " + f.name + " (" + (done + failed.length + 1) + ") ...";
            sendSound(f).then(function (url) {
              it.sounds.push({ title: f.name.replace(/\.[^.]+$/, ""), artist: "", file: url }); done++;
            }).catch(function (err) { failed.push(err.message); }).then(next);
          };
          next();
        }
      });

      function close() { document.removeEventListener("keydown", onKey); bg.remove(); }
      function onKey(e) { if (e.key === "Escape") close(); }
      document.addEventListener("keydown", onKey);

      bg.addEventListener("click", function (e) {
        var b = e.target.closest("[data-x]");
        if (!b) return;
        var x = b.dataset.x;
        if (x === "cancel") close();
        else if (x === "nopic") { it.image = ""; drawPic(); }
        else if (x === "addSound") { it.sounds.push({ title: "", artist: "" }); drawSounds(true); }
        else if (x === "delSound") { it.sounds.splice(+b.closest(".srow").dataset.j, 1); drawSounds(false); }
        else if (x === "unfile") { delete it.sounds[+b.closest(".srow").dataset.j].file; drawSounds(false); }
        else if (x === "paste") {
          var added = 0;
          $("#sPaste", bg).value.split("\n").forEach(function (line) {
            line = line.trim();
            if (!line) return;
            var m = line.split(/\s+[-\u2013\u2014|]\s+|\t/);
            it.sounds.push({ title: m[0].trim(), artist: (m.slice(1).join(" - ")).trim() });
            added++;
          });
          $("#sPaste", bg).value = "";
          if (added) { drawSounds(false); toast(added + " sound" + (added === 1 ? "" : "s") + " added", "good"); }
        }
        else if (x === "save") {
          if (!it.name.trim()) { $("#fMsg", bg).textContent = "Write a name."; return; }
          if (cfg.sounds) it.sounds = it.sounds.filter(function (s) { return s.title.trim(); });
          var list = items.slice();
          if (index < 0) list.push(it); else list[index] = it;
          b.disabled = true;
          persist(list, function (r) {
            if (r.ok) close();
            else { b.disabled = false; $("#fMsg", bg).textContent = r.error; }
          });
        }
      });
      setTimeout(function () { var n = $("input[data-f=name]", bg); if (n) n.focus(); }, 30);
    }

    api("credits").then(function (r) {
      if (!r.ok) { box.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      items = (kind === "credits" ? r.credits : r.thanks) || [];
      draw();
    });
  }

  // ------------------------------------------------------------------------------------------ privacy and license
  function mdPreview(text) {
    var out = "", list = false;
    String(text || "").replace(/\r/g, "").split("\n").forEach(function (raw) {
      var l = raw.trim();
      if (!l) return;
      if (l.indexOf("- ") === 0) { if (!list) { out += "<ul>"; list = true; } out += "<li>" + esc(l.slice(2)) + "</li>"; return; }
      if (list) { out += "</ul>"; list = false; }
      out += l.indexOf("## ") === 0 ? "<h3>" + esc(l.slice(3)) + "</h3>" : "<p>" + esc(l) + "</p>";
    });
    return out + (list ? "</ul>" : "");
  }
  function lineCount(t) { return String(t || "").split("\n").filter(function (l) { return l.trim(); }).length; }

  function pagesPage(view) {
    api("pages").then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      var p = r.pages.privacy;
      view.innerHTML =
        '<div class="card"><h2>Privacy page</h2><p class="sub">Shown on its own page, <a href="/privacy" target="_blank" rel="noopener">leikpyarhunters.org/privacy</a> (the Privacy link in the footer of the website). Write one line for each paragraph. Start a line with <code>## </code> for a heading and with <code>- </code> for a list item. The Burmese text must have the same lines in the same order: line 5 in English is line 5 in Burmese.</p>' +
        '<div class="toolbar"><button class="btn" id="pStart" type="button">Load the text that is on the website now</button><button class="btn ghost" id="pDefault" type="button">Use the text that came with the site</button></div>' +
        '<div class="two"><label class="field"><span>English</span><textarea id="pEn" spellcheck="true"></textarea></label><label class="field"><span>Burmese</span><textarea id="pMy" class="my" lang="my"></textarea></label></div>' +
        '<p class="hint" id="pCount"></p>' +
        '<div class="toolbar" style="margin-top:12px"><div class="range" id="pTabs"><button type="button" data-t="en" aria-pressed="true">Preview in English</button><button type="button" data-t="my" aria-pressed="false">Preview in Burmese</button></div></div>' +
        '<div class="preview" id="pPrev"></div>' +
        '<div class="savebar"><span class="grow" id="pState">Saved text is on the website.</span><button class="btn primary" id="pSave" type="button">Save the Privacy page</button></div></div>' +

        '<div class="card" style="margin-top:14px"><h2>License</h2><p class="sub">The License text (who may do what, the creators, the copyright) is edited in <a href="#/texts">Website texts</a>, section "Credits and license", in English and in Burmese. The creators\' photos are in <a href="#/photos">Photos and creators</a>.</p>' +
        '<label class="field" style="max-width:420px"><span>Contact e-mail for the license</span><input id="sMail" type="email" placeholder="name@example.com" value="' + esc(r.settings.contactEmail) + '"></label>' +
        '<p class="hint" style="margin:-6px 0 12px">Shown in the license: "To ask for written permission, write to ...". Leave it empty to keep the general sentence.</p>' +
        '<button class="btn primary" id="sSave" type="button">Save the contact e-mail</button></div>';

      var en = $("#pEn"), my = $("#pMy"), tab = "en", changed = false;
      en.value = p.en; my.value = p.my;

      function update() {
        $("#pPrev").className = "preview" + (tab === "my" ? " my" : "");
        $("#pPrev").innerHTML = mdPreview(tab === "my" ? my.value : en.value) || '<p class="dim">The text that came with the site is shown.</p>';
        var a = lineCount(en.value), b = lineCount(my.value);
        $("#pCount").textContent = "English: " + a + " lines. Burmese: " + b + " lines." + (b && a !== b ? " The numbers should be the same, or some lines stay in English." : "");
        $("#pState").textContent = changed ? "Unsaved changes." : "Saved text is on the website.";
      }
      en.oninput = my.oninput = function () { changed = true; update(); };
      $$("#pTabs button").forEach(function (b) { b.onclick = function () { tab = b.dataset.t; $$("#pTabs button").forEach(function (x) { x.setAttribute("aria-pressed", String(x === b)); }); update(); }; });

      $("#pStart").onclick = function () {
        // reads the page and the Burmese dictionary that came with the site
        Promise.all([
          fetch("/privacy", { credentials: "same-origin" }).then(function (x) { return x.text(); }),
          fetch("/js/i18n-my.js", { credentials: "same-origin" }).then(function (x) { return x.text(); })
        ]).then(function (res) {
          var doc = new DOMParser().parseFromString(res[0], "text/html"), body = doc.getElementById("privacyBody");
          var dict = {};
          try { dict = (new Function("var window={};" + res[1] + ";return window.LH_MY;"))() || {}; } catch (e) { dict = {}; }
          if (!body) { toast("The Privacy section was not found on the page.", "bad"); return; }
          var enL = [], myL = [];
          var add = function (prefix, text) {
            var key = text.replace(/\s+/g, " ").trim();
            enL.push(prefix + key); myL.push(prefix + (dict[key] || ""));
          };
          Array.prototype.forEach.call(body.children, function (el) {
            if (el.tagName === "H3") add("## ", el.textContent);
            else if (el.tagName === "UL") Array.prototype.forEach.call(el.children, function (li) { add("- ", li.textContent); });
            else add("", el.textContent);
          });
          en.value = enL.join("\n\n").replace(/\n\n- /g, "\n- ").replace(/\n\n(## )/g, "\n\n$1");
          my.value = myL.join("\n\n").replace(/\n\n- /g, "\n- ");
          changed = true; update();
          toast("Loaded. Edit the texts, then save.", "good");
        });
      };
      $("#pDefault").onclick = function () {
        modal({ title: "Use the original Privacy text?", text: "The text you saved is removed and the website shows the Privacy text it came with.", confirm: "Use the original" }).then(function (v) {
          if (!v) return;
          api("pages", { page: "privacy", en: "", my: "" }).then(function (x) { if (x.ok) { toast("The original text is back", "good"); route.force = true; route(); } else toast(x.error, "bad"); });
        });
      };
      $("#pSave").onclick = function () {
        if (!en.value.trim()) { toast("Write the English text first, or press 'Use the text that came with the site'.", "bad"); return; }
        api("pages", { page: "privacy", en: en.value, my: my.value }).then(function (x) {
          if (x.ok) { changed = false; update(); toast("Saved. Reload the website to see it.", "good"); } else toast(x.error, "bad");
        });
      };
      $("#sSave").onclick = function () {
        api("settings", { contactEmail: $("#sMail").value }).then(function (x) { if (x.ok) toast("Saved", "good"); else toast(x.error, "bad"); });
      };
      leave = function (silent) { if (!changed) return true; return silent ? false : window.confirm("The Privacy page has unsaved changes. Leave this page and lose them?"); };
      update();
    });
  }

  // ------------------------------------------------------------------------------------------ admins
  function adminsPage(view) {
    api("admins").then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      view.innerHTML =
        '<div class="grid cols2" style="align-items:start">' +
        '<div class="card"><h2>Admins</h2><p class="sub">Every admin can do everything, also add and remove other admins.</p>' +
        smallTable(["E-mail", "Added by", "Last sign in", ""], r.admins.map(function (a) {
          return [esc(a.email) + (a.id === r.me ? ' <span class="pill green">you</span>' : ""), esc(a.createdBy || "-"), ago(a.lastLogin),
            a.id === r.me ? "" : '<button class="btn sm danger" data-del="' + a.id + '" data-mail="' + esc(a.email) + '" type="button">Remove</button>'];
        })) + "</div>" +
        '<div class="grid">' +
        '<form class="card" id="addForm" novalidate><h2>Add an admin</h2><p class="sub">The new admin signs in at this page with the e-mail and the password.</p>' +
        '<label class="field"><span>E-mail</span><input id="nEmail" type="email" autocomplete="off" required></label>' +
        '<label class="field"><span>Password (8 characters or more)</span><input id="nPass" type="text" autocomplete="off" required></label>' +
        '<p class="msg" id="nMsg" role="alert"></p><button class="btn primary" type="submit">Add the admin</button></form>' +
        '<form class="card" id="pwForm" novalidate><h2>Change my password</h2><p class="sub">You stay signed in here. Your other sessions are signed out.</p>' +
        '<label class="field"><span>Current password</span><input id="cCur" type="password" autocomplete="current-password" required></label>' +
        '<label class="field"><span>New password (8 characters or more)</span><input id="cNew" type="password" autocomplete="new-password" required></label>' +
        '<p class="msg" id="cMsg" role="alert"></p><button class="btn primary" type="submit">Change the password</button></form>' +
        "</div></div>";

      $$("[data-del]").forEach(function (b) {
        b.onclick = function () {
          modal({ title: "Remove " + b.dataset.mail + "?", text: "This admin can no longer sign in.", confirm: "Remove", danger: true }).then(function (v) {
            if (v) api("admins/delete", { id: +b.dataset.del }).then(function (x) { if (x.ok) { toast("Removed", "good"); route.force = true; route(); } else toast(x.error, "bad"); });
          });
        };
      });
      $("#addForm").onsubmit = function (e) {
        e.preventDefault();
        api("admins", { email: $("#nEmail").value, password: $("#nPass").value }).then(function (x) {
          if (x.ok) { toast("Admin added", "good"); route.force = true; route(); } else { $("#nMsg").textContent = x.error; }
        });
      };
      $("#pwForm").onsubmit = function (e) {
        e.preventDefault();
        api("password", { current: $("#cCur").value, password: $("#cNew").value }).then(function (x) {
          if (x.ok) { toast("Password changed", "good"); $("#cCur").value = $("#cNew").value = ""; $("#cMsg").textContent = ""; } else { $("#cMsg").textContent = x.error; }
        });
      };
    });
  }

  // ------------------------------------------------------------------------------------------ activity
  function activityPage(view) {
    api("log").then(function (r) {
      if (!r.ok) { view.innerHTML = '<p class="msg">' + esc(r.error) + "</p>"; return; }
      view.innerHTML = '<p class="muted" style="margin-top:0">What the admins did, the newest first (the last 200).</p>' +
        smallTable(["When", "Admin", "Action", "Detail"], r.log.map(function (l) { return [dateTime(l.at), esc(l.admin), '<span class="pill">' + esc(l.action) + "</span>", esc(l.detail)]; }));
    });
  }

  var VIEWS = { dashboard: dashboard, agents: agentsPage, texts: textsPage, photos: photosPage, pages: pagesPage, admins: adminsPage, activity: activityPage };

  // ------------------------------------------------------------------------------------------ start
  $("#loginForm").onsubmit = function (e) {
    e.preventDefault();
    var go = $("#lGo");
    go.disabled = true; $("#lMsg").textContent = "";
    api("login", { email: $("#lEmail").value, password: $("#lPass").value }).then(function (r) {
      go.disabled = false;
      if (!r.ok) { $("#lMsg").textContent = r.error; return; }
      me = r.admin; current = null; showApp();
    });
  };
  $("#logout").onclick = function () { api("logout", {}).then(function () { current = null; showLogin(); }); };
  $("#menuBtn").onclick = function () { $("#side").classList.toggle("open"); };

  api("me").then(function (r) {
    if (r.ok && r.admin) { me = r.admin; showApp(); } else showLogin();
  });
})();
