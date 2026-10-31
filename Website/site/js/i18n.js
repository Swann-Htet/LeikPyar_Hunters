/* Leikpyar Hunters: English / Burmese (EN | MM).
   The Burmese texts are in i18n-my.js (English text -> Burmese). Every text of the page that has a Burmese version is swapped, also the texts
   the page makes later (the scores, the messages). The font is the game's own Burmese font (MM.ttf, see site.css).
   A text with no Burmese version stays English: names, numbers, keys. */
(function () {
  "use strict";

  var DICT = window.LH_MY || {};
  var KEY = "lh_lang";
  var exact = {}, templates = [];

  function norm(s) { return s.replace(/\s+/g, " ").trim(); }
  function escRe(s) { return s.replace(/[.*+?^$()|[\]\\{}]/g, "\\$&"); }

  // "Model by {0}" becomes a pattern: ^Model by (.+?)$  (the {0} part is put into the Burmese sentence)
  Object.keys(DICT).forEach(function (k) {
    if (/\{\d+\}/.test(k)) {
      var parts = k.split(/\{(\d+)\}/);          // text, index, text, index, text
      var re = "^", order = [];
      for (var i = 0; i < parts.length; i++) {
        if (i % 2 === 0) re += escRe(parts[i]);
        else { re += "(.+?)"; order.push(parseInt(parts[i], 10)); }
      }
      templates.push({ re: new RegExp(re + "$"), order: order, out: DICT[k], size: k.replace(/\{\d+\}/g, "").length });
    } else {
      exact[k] = DICT[k];
    }
  });
  templates.sort(function (a, b) { return b.size - a.size; });   // the one with the most fixed text first

  function translate(text) {
    var key = norm(text);
    if (!key) return null;
    if (Object.prototype.hasOwnProperty.call(exact, key)) return exact[key];
    for (var i = 0; i < templates.length; i++) {
      var m = key.match(templates[i].re);
      if (!m) continue;
      var out = templates[i].out;
      for (var j = 0; j < templates[i].order.length; j++) {
        var part = m[j + 1], mine = translate(part);
        out = out.split("{" + templates[i].order[j] + "}").join(mine !== null ? mine : part);
      }
      return out;
    }
    return null;
  }

  var lang = "en";
  var texts = [];            // { node, en, my }  every text node that was swapped
  var byNode = typeof WeakMap === "function" ? new WeakMap() : null;
  var busy = false;
  var SKIP = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1, CODE: 1 };

  function skipped(node) {
    for (var e = node.parentNode; e && e.nodeType === 1; e = e.parentNode) {
      if (SKIP[e.tagName] || e.hasAttribute("data-no-i18n")) return true;
    }
    return false;
  }

  function swapText(node) {
    if (skipped(node)) return;
    var rec = byNode.get(node);
    var now = node.nodeValue;
    if (rec && now === rec.my) return;                   // already Burmese
    var mine = translate(now);
    if (mine === null) { if (rec) byNode.delete(node); return; }
    var lead = now.match(/^\s*/)[0], tail = now.match(/\s*$/)[0];
    rec = { node: node, en: now, my: lead + mine + tail };
    texts.push(rec);
    byNode.set(node, rec);
    node.nodeValue = rec.my;
  }

  var ATTRS = ["placeholder", "aria-label", "title", "alt", "content"];
  var attrs = [];            // { el, name, en, my }

  function swapAttrs(el) {
    if (el.nodeType !== 1 || el.hasAttribute("data-no-i18n")) return;
    for (var i = 0; i < ATTRS.length; i++) {
      var name = ATTRS[i];
      if (!el.hasAttribute(name)) continue;
      if (name === "content" && !(el.tagName === "META" && el.getAttribute("name") === "description")) continue;
      var en = el.getAttribute(name), mine = translate(en);
      if (mine === null) continue;
      attrs.push({ el: el, name: name, en: en, my: mine });
      el.setAttribute(name, mine);
    }
  }

  function walk(root) {
    if (root.nodeType === 3) { swapText(root); return; }
    if (root.nodeType !== 1) return;
    swapAttrs(root);
    var w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, null, false), n;
    while ((n = w.nextNode())) {
      if (n.nodeType === 3) swapText(n); else swapAttrs(n);
    }
  }

  function restore() {
    busy = true;
    texts.forEach(function (r) { if (r.node.nodeValue === r.my) r.node.nodeValue = r.en; byNode.delete(r.node); });
    texts = [];
    attrs.forEach(function (a) { if (a.el.getAttribute(a.name) === a.my) a.el.setAttribute(a.name, a.en); });
    attrs = [];
    busy = false;
  }

  // what the page makes later (scores, messages, the models) is swapped at once
  var observer = new MutationObserver(function (list) {
    if (lang !== "my" || busy) return;
    busy = true;
    list.forEach(function (m) {
      if (m.type === "characterData") swapText(m.target);
      else if (m.type === "childList") m.addedNodes.forEach(function (n) { walk(n); });
    });
    busy = false;
  });

  var titleEn = document.title;

  function setLang(next, save) {
    if (next !== "my") next = "en";
    if (next === lang && save !== "force") return;

    if (lang === "my") restore();
    lang = next;
    document.documentElement.setAttribute("lang", next);

    if (next === "my") {
      busy = true;
      walk(document.body);
      swapAttrs(document.head.querySelector("meta[name=description]") || document.body);
      var t = translate(titleEn);
      if (t !== null) document.title = t;
      busy = false;
      observer.observe(document.body, { childList: true, subtree: true, characterData: true });
    } else {
      observer.disconnect();
      document.title = titleEn;
    }

    var buttons = document.querySelectorAll(".lang button");
    for (var i = 0; i < buttons.length; i++) buttons[i].setAttribute("aria-pressed", String(buttons[i].getAttribute("data-lang") === next));

    if (save) { try { localStorage.setItem(KEY, next); } catch (e) { /* nothing */ } }
  }

  function start() {
    var buttons = document.querySelectorAll(".lang button");
    for (var i = 0; i < buttons.length; i++) {
      buttons[i].addEventListener("click", function (e) { setLang(e.currentTarget.getAttribute("data-lang"), true); });
    }

    var saved = null;
    try { saved = localStorage.getItem(KEY); } catch (e) { /* private window */ }
    if (!saved) saved = /^my\b/i.test(navigator.language || "") ? "my" : "en";
    // the page builds more of itself right after it loads: give it a moment before the first swap
    if (saved === "my") setTimeout(function () { setLang("my", false); }, 0);
  }

  window.LHLang = { set: function (l) { setLang(l, true); }, get: function () { return lang; }, translate: translate };

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
  else start();
})();
