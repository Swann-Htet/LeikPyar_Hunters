/* Leikpyar Hunters - the background song and the disc in the bottom right corner.
   - The song starts when the website opens. A browser does not let a page make sound before the visitor does something (a click, a key, a touch):
     then the song starts at the first one. The visitor's choice is remembered: after a pause the song does not start by itself next time.
   - The disc turns while the song plays. A click (or a tap) on it pauses the song, another one plays it again.
   - Point at the disc (a tap on a phone) and the time shows: how far the song is, and how long it is. A ring around the disc fills as it plays. */
(function () {
  "use strict";

  var cd = document.getElementById("cd");
  var btn = document.getElementById("cdBtn");
  var audio = document.getElementById("bgm");
  var timeEl = document.getElementById("cdTime");
  if (!cd || !btn || !audio || !timeEl) return;

  var bar = cd.querySelector(".cd-bar");
  var CIRC = 2 * Math.PI * 47;                 // the ring (r = 47 in a 100 x 100 picture)
  var KEY = "lh_music";
  var wantsOff = false;
  try { wantsOff = localStorage.getItem(KEY) === "off"; } catch (e) { /* private window */ }

  audio.volume = 0.4;

  function fmt(s) {
    if (!isFinite(s) || s < 0) s = 0;
    s = Math.floor(s);
    return Math.floor(s / 60) + ":" + ("0" + (s % 60)).slice(-2);
  }

  function paint() {
    var d = audio.duration || 0, t = audio.currentTime || 0;
    timeEl.textContent = fmt(t) + " / " + fmt(d);
    if (bar) bar.style.strokeDashoffset = String(CIRC * (1 - (d ? Math.min(1, t / d) : 0)));
  }

  function state() {
    var playing = !audio.paused;
    cd.classList.toggle("playing", playing);
    btn.setAttribute("aria-pressed", String(playing));
    btn.setAttribute("aria-label", playing ? "Pause the music" : "Play the music");
  }

  function remember(off) {
    wantsOff = off;
    try { localStorage.setItem(KEY, off ? "off" : "on"); } catch (e) { /* nothing */ }
  }

  // shows the time for a moment (a phone has no hover)
  var flashTimer = null;
  function flash() {
    cd.classList.add("show");
    clearTimeout(flashTimer);
    flashTimer = setTimeout(function () { cd.classList.remove("show"); }, 2400);
  }

  // the first touch, key or click of the visitor starts the song (if the browser did not allow it before)
  var armed = false;
  function arm() {
    if (armed) return;
    armed = true;
    var go = function (e) {
      if (e && e.target && cd.contains(e.target)) return;      // a click on the disc is the visitor's own choice (see below)
      if (!wantsOff && audio.paused) play();
      if (!audio.paused || wantsOff) disarm();
    };
    var events = ["pointerdown", "keydown", "touchend"];
    function disarm() { events.forEach(function (n) { window.removeEventListener(n, go, true); }); armed = false; }
    events.forEach(function (n) { window.addEventListener(n, go, true); });
  }

  function play() {
    var p = audio.play();
    if (p && typeof p.catch === "function") p.catch(function () { arm(); });
  }

  btn.addEventListener("click", function () {
    if (audio.paused) { remember(false); play(); }
    else { audio.pause(); remember(true); }
    flash();
  });

  ["timeupdate", "loadedmetadata", "durationchange"].forEach(function (n) { audio.addEventListener(n, paint); });
  ["play", "pause", "ended"].forEach(function (n) { audio.addEventListener(n, state); });
  paint();
  state();

  // the opening: the disc rolls in from the right, turning
  function open() {
    cd.classList.add("ready", "intro");
    setTimeout(function () { cd.classList.remove("intro"); }, 1700);
    if (!wantsOff) play();
  }
  if (document.readyState === "complete") setTimeout(open, 350);
  else window.addEventListener("load", function () { setTimeout(open, 350); });
})();
