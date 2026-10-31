/* Leikpyar Hunters - the texts the admins edited (see /admin/).
   The server puts them into the page as window.LH_CONTENT = { v, texts, images, pages, settings }. This script applies them before i18n.js starts:
     - texts:    { "English text of the site": { en: "new English", my: "Burmese" } }   (my "" = no Burmese, the text stays English)
     - pages:    { privacy: { en: "...", my: "..." } }   one line = one paragraph, "## " heading, "- " bullet; line N of English <-> line N of Burmese
     - settings: { contactEmail }
   The pictures are replaced by the server inside index.html itself. A page with no edits has no LH_CONTENT and this script does nothing. */
(function () {
  "use strict";

  var C = window.LH_CONTENT;
  if (!C) return;

  var DICT = window.LH_MY = window.LH_MY || {};
  function norm(s) { return String(s).replace(/\s+/g, " ").trim(); }

  // ---- links: a web address (http or https) makes a name a link that opens in a new tab
  function safeUrl(u) { return typeof u === "string" && /^https?:\/\/[^\s<>"']+$/i.test(u) ? u : ""; }
  function linkify(el, url) {
    url = safeUrl(url);
    if (!el || !url) return;
    var a = document.createElement("a");
    a.className = "ext";
    a.href = url;
    a.target = "_blank";
    a.rel = "noopener noreferrer";
    a.addEventListener("click", function (e) { e.stopPropagation(); });     // a name inside a card that opens (the sounds) does not open it
    while (el.firstChild) a.appendChild(el.firstChild);
    el.appendChild(a);
  }

  // ---- Burmese: the edited Burmese goes into the dictionary of i18n.js
  var enMap = {};                           // original English -> new English
  Object.keys(C.texts || {}).forEach(function (key) {
    var t = C.texts[key] || {};
    var shown = t.en ? norm(t.en) : key;
    if (t.en && t.en !== key) enMap[key] = norm(t.en);
    if (Object.prototype.hasOwnProperty.call(t, "my")) {
      if (t.my) DICT[shown] = t.my; else delete DICT[shown];
    } else if (shown !== key && Object.prototype.hasOwnProperty.call(DICT, key)) {
      DICT[shown] = DICT[key];              // only the English changed: the old Burmese stays
    }
    if (shown !== key && Object.prototype.hasOwnProperty.call(DICT, key)) delete DICT[key];
  });

  // ---- English: the text nodes and the attributes that show an edited text
  var ATTRS = ["placeholder", "aria-label", "title", "alt", "content"];
  var busy = false;

  function fixNode(node) {
    var now = node.nodeValue;
    var key = norm(now);
    if (!key || !Object.prototype.hasOwnProperty.call(enMap, key)) return;
    var lead = now.match(/^\s*/)[0], tail = now.match(/\s*$/)[0];
    node.nodeValue = lead + enMap[key] + tail;
  }

  function fixAttrs(el) {
    for (var i = 0; i < ATTRS.length; i++) {
      var name = ATTRS[i];
      if (!el.hasAttribute(name)) continue;
      if (name === "content" && !(el.tagName === "META" && el.getAttribute("name") === "description")) continue;
      var key = norm(el.getAttribute(name));
      if (Object.prototype.hasOwnProperty.call(enMap, key)) el.setAttribute(name, enMap[key]);
    }
  }

  var SKIP = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1 };

  function walk(root) {
    if (root.nodeType === 3) { if (!(root.parentNode && SKIP[root.parentNode.tagName])) fixNode(root); return; }
    if (root.nodeType !== 1 || SKIP[root.tagName]) return;
    fixAttrs(root);
    var w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, null, false), n;
    while ((n = w.nextNode())) {
      if (n.nodeType === 3) { if (!SKIP[n.parentNode.tagName]) fixNode(n); } else if (!SKIP[n.tagName]) fixAttrs(n);
    }
  }

  // ---- the Privacy page: lines of text -> headings, paragraphs and lists
  function lines(text) {
    return String(text || "").replace(/\r/g, "").split("\n").map(function (l) { return l.trim(); }).filter(Boolean);
  }

  function renderPage(body, en, my) {
    var e = lines(en), m = lines(my);
    body.textContent = "";
    var list = null;
    e.forEach(function (line, i) {
      var kind = "p", text = line;
      if (line.indexOf("## ") === 0) { kind = "h3"; text = line.slice(3).trim(); }
      else if (line.indexOf("- ") === 0) { kind = "li"; text = line.slice(2).trim(); }
      var mine = m[i] || "";
      if (mine) mine = mine.replace(/^(##|-)(\s+|$)/, "").trim();
      if (mine && text) DICT[norm(text)] = mine;
      var el = document.createElement(kind);
      el.textContent = text;
      if (kind === "li") {
        if (!list) { list = document.createElement("ul"); body.appendChild(list); }
        list.appendChild(el);
      } else {
        list = null;
        body.appendChild(el);
      }
    });
  }

  // ---- more contributors: small boxes under the four creators (the label and the copyright line can have a Burmese text)
  function renderCredits() {
    var list = C.credits, box = document.getElementById("moreCredits"), grid = document.getElementById("moreCreditsGrid");
    if (!list || !list.length || !box || !grid) return;
    grid.textContent = "";
    list.forEach(function (c) {
      var card = document.createElement("article");
      card.className = "credit-mini" + (c.image ? "" : " noimg");
      if (c.image) {
        var img = document.createElement("img");
        img.src = c.image; img.alt = ""; img.width = 76; img.height = 76; img.loading = "lazy"; img.decoding = "async"; img.draggable = false;
        card.appendChild(img);
      }
      var body = document.createElement("div");
      if (c.role) { var r = document.createElement("span"); r.textContent = c.role; body.appendChild(r); if (c.roleMy) DICT[norm(c.role)] = c.roleMy; }
      var h = document.createElement("h4"); h.textContent = c.name; body.appendChild(h); linkify(h, c.link);
      if (c.note) { var n = document.createElement("small"); n.textContent = c.note; body.appendChild(n); if (c.noteMy) DICT[norm(c.note)] = c.noteMy; }
      card.appendChild(body);
      grid.appendChild(card);
    });
    box.hidden = false;
  }

  // ---- one player for all the sounds: a new sound stops the one that plays
  var player = null, playing = null;
  function playButton(url) {
    var b = document.createElement("button");
    b.type = "button"; b.className = "play"; b.setAttribute("aria-label", "Play");
    b.addEventListener("click", function () {
      if (!player) {
        player = new Audio();
        player.preload = "none";
        player.addEventListener("ended", function () { if (playing) playing.classList.remove("on"); playing = null; });
      }
      if (playing === b) { player.pause(); b.classList.remove("on"); playing = null; return; }
      if (playing) playing.classList.remove("on");
      player.src = url;
      playing = b;
      b.classList.add("on");
      var p = player.play();
      if (p && p.catch) p.catch(function () { b.classList.remove("on"); if (playing === b) playing = null; });
    });
    return b;
  }

  // ---- special thanks: a box can hold a list of sounds (a title and the artist) that opens inside the card
  function renderThanks() {
    var list = C.thanks, box = document.getElementById("specialThanks"), grid = document.getElementById("specialThanksGrid");
    if (!list || !list.length || !box || !grid) return;
    grid.textContent = "";
    list.forEach(function (c) {
      var sounds = c.sounds || [];
      var card = document.createElement(sounds.length ? "details" : "article");
      card.className = (sounds.length ? "thanks" : "credit-mini") + (c.image ? "" : " noimg");
      var head = sounds.length ? document.createElement("summary") : card;
      if (c.image) {
        var img = document.createElement("img");
        img.src = c.image; img.alt = ""; img.width = 76; img.height = 76; img.loading = "lazy"; img.decoding = "async"; img.draggable = false;
        head.appendChild(img);
      }
      var body = document.createElement("div");
      if (c.role) { var r = document.createElement("span"); r.className = "role"; r.textContent = c.role; body.appendChild(r); if (c.roleMy) DICT[norm(c.role)] = c.roleMy; }
      var h = document.createElement("h4"); h.textContent = c.name; body.appendChild(h); linkify(h, c.link);
      if (c.note) { var n = document.createElement("small"); n.textContent = c.note; body.appendChild(n); if (c.noteMy) DICT[norm(c.note)] = c.noteMy; }
      head.appendChild(body);
      if (sounds.length) {
        var hint = document.createElement("span");
        hint.className = "open-hint";
        hint.textContent = sounds.length === 1 ? "1 sound" : sounds.length + " sounds";
        head.appendChild(hint);
      }
      if (sounds.length) card.appendChild(head);
      if (sounds.length) {
        var ul = document.createElement("ul");
        ul.className = "sounds";
        sounds.forEach(function (s) {
          var li = document.createElement("li");
          if (s.file) li.appendChild(playButton(s.file));
          var t = document.createElement("b"); t.textContent = s.title; li.appendChild(t);
          if (s.artist) { var a = document.createElement("span"); a.textContent = s.artist; li.appendChild(a); }
          ul.appendChild(li);
        });
        card.appendChild(ul);
      }
      grid.appendChild(card);
    });
    box.hidden = false;
  }

  // the four creators: a link for each (portfolio ...), set in /admin/ (Photos and creators)
  function linkCreators() {
    var links = C.settings && C.settings.creatorLinks;
    if (!links) return;
    var cards = document.querySelectorAll(".creator[data-slot]");
    for (var i = 0; i < cards.length; i++) {
      var url = links[cards[i].getAttribute("data-slot")];
      if (url) linkify(cards[i].querySelector("h3"), url);
    }
  }

  function start() {
    linkCreators();
    renderCredits();
    renderThanks();
    if (document.title && enMap[norm(document.title)]) document.title = enMap[norm(document.title)];

    var privacy = C.pages && C.pages.privacy;
    var body = document.getElementById("privacyBody");
    if (privacy && privacy.en && body) renderPage(body, privacy.en, privacy.my);

    if (Object.keys(enMap).length) {
      walk(document.head);
      walk(document.body);
      // texts the page makes later (messages, scores) are fixed as they appear
      new MutationObserver(function (list) {
        if (busy) return;
        busy = true;
        list.forEach(function (m) {
          if (m.type === "characterData") { if (!(m.target.parentNode && SKIP[m.target.parentNode.tagName])) fixNode(m.target); }
          else if (m.type === "childList") m.addedNodes.forEach(function (n) { walk(n); });
        });
        busy = false;
      }).observe(document.body, { childList: true, subtree: true, characterData: true });
    }
  }

  window.LH_SETTINGS = C.settings || {};
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
  else start();
})();
