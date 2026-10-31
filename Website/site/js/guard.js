/* Leikpyar Hunters: keeps the browser's developer tools and the "save / view source" shortcuts away from the website and the game.
   This is a deterrent, not a lock: a browser always lets its owner look at what it receives. The real protection of the models
   is on the server (the 3D files are never served as plain files). */
(function () {
  "use strict";

  var d = document;
  function stop(e) { e.preventDefault(); e.stopPropagation(); return false; }
  function typing(t) { return t && /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName); }

  // no right-click menu (but it stays in the text boxes: copy and paste)
  d.addEventListener("contextmenu", function (e) { if (!typing(e.target)) return stop(e); }, true);

  // F12, Ctrl+Shift+I / J / C / K, Ctrl+U (view source), Ctrl+S (save page), and the Mac versions
  d.addEventListener("keydown", function (e) {
    var k = (e.key || "").toLowerCase();
    var ctrl = e.ctrlKey || e.metaKey;
    if (e.keyCode === 123) return stop(e);
    if (ctrl && e.shiftKey && (k === "i" || k === "j" || k === "c" || k === "k")) return stop(e);
    if (ctrl && (k === "u" || k === "s")) return stop(e);
    if (e.metaKey && e.altKey && (k === "i" || k === "j" || k === "c" || k === "u")) return stop(e);
  }, true);

  // pictures and models can not be dragged out of the page
  d.addEventListener("dragstart", function (e) { if (!typing(e.target)) return stop(e); }, true);

  // The developer tools are open (a "debugger" statement pauses the page only while they are): the page is covered, the 3D models are
  // taken away from it, and it works again after the tools are closed and the page is reloaded.
  var blocked = false;
  function block() {
    if (blocked) return;
    blocked = true;
    window.lhBlocked = true;
    try {
      var list = d.querySelectorAll("model-viewer");
      for (var i = 0; i < list.length; i++) list[i].remove();
      var cover = d.createElement("div");
      cover.setAttribute("style", "position:fixed;inset:0;z-index:2147483647;background:#080504;color:#efe4d2;display:grid;place-items:center;text-align:center;padding:24px;font:600 18px/1.5 'DM Sans',system-ui,sans-serif");
      cover.innerHTML = "<div><div style=\"font-size:26px;margin-bottom:10px;color:#e5333d\">Developer tools are not allowed here</div>Close them and reload the page.</div>";
      d.documentElement.appendChild(cover);
      var body = d.body;
      if (body) { body.style.visibility = "hidden"; cover.style.visibility = "visible"; }
    } catch (err) { /* nothing */ }
  }

  function watch() {
    if (blocked) return;
    var t = performance.now();
    /* eslint-disable-next-line no-debugger */
    debugger;
    if (performance.now() - t > 150) block();
  }
  // only on a computer (a phone has no developer tools open beside the page)
  var coarse = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;
  if (!coarse) setInterval(watch, 1000);
})();
