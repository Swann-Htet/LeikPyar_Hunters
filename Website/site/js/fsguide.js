/* Leikpyar Hunters: the full screen guide. A moving picture of how to hide the browser bar on a phone (Android and iPhone), in English and Burmese.
   Open it with any element that has data-fsg-open. The phone is made here, the movement is in css/fsguide.css. */
(function () {
  "use strict";

  var T = {
    en: {
      title: "Full screen on your phone",
      android: "Android", ios: "iPhone",
      badge: "FULL SCREEN",
      go: "Play now",
      close: "Close",
      aCap1: "Tap Play Fullscreen", aCap2: "The browser bar goes away",
      iCap1: "Tap aA in the address bar", iCap2: "Tap Hide Toolbar", iCap3: "Done: the toolbar is hidden",
      aHead: "On Android",
      aSteps: ["Open the game in Chrome.", "Tap Play Fullscreen when the game asks.", "The browser bar goes away. Turn the phone sideways for the best view."],
      aNote: "If the button does not show, tap the three dots in Chrome and choose Add to Home screen, then open the game from the new icon.",
      iHead: "On iPhone (Safari)",
      iSteps: ["Tap aA in the address bar.", "Tap Hide Toolbar.", "The toolbar goes away. Tap the top edge of the screen to bring it back."],
      iNote: "For no toolbar at all: tap Share, choose Add to Home Screen, then open the game from the new icon."
    },
    my: {
      title: "ဖုန်းတွင် ဖန်သားပြင်အပြည့်",
      android: "Android", ios: "iPhone",
      badge: "ဖန်သားပြင်အပြည့်",
      go: "ယခု ကစားမည်",
      close: "ပိတ်ရန်",
      aCap1: "Play Fullscreen ကို နှိပ်ပါ", aCap2: "ဘရောက်ဇာဘား ပျောက်သွားမည်",
      iCap1: "လိပ်စာဘားရှိ aA ကို နှိပ်ပါ", iCap2: "Hide Toolbar ကို နှိပ်ပါ", iCap3: "ပြီးပါပြီ - ကိရိယာဘား ပျောက်သွားသည်",
      aHead: "Android တွင်",
      aSteps: ["Chrome ဖြင့် ဂိမ်းကို ဖွင့်ပါ။", "ဂိမ်းက မေးသောအခါ Play Fullscreen ကို နှိပ်ပါ။", "ဘရောက်ဇာဘား ပျောက်သွားမည်။ အကောင်းဆုံးမြင်ကွင်းအတွက် ဖုန်းကို ဘေးတိုက်လှည့်ပါ။"],
      aNote: "ခလုတ်မပေါ်လျှင် Chrome ၏ အစက်သုံးစက်ကို နှိပ်ပြီး Add to Home screen ကို ရွေးပါ၊ ထို့နောက် အိုင်ကွန်အသစ်မှ ဂိမ်းကို ဖွင့်ပါ။",
      iHead: "iPhone (Safari) တွင်",
      iSteps: ["လိပ်စာဘားရှိ aA ကို နှိပ်ပါ။", "Hide Toolbar ကို နှိပ်ပါ။", "ကိရိယာဘား ပျောက်သွားမည်။ ပြန်ပေါ်စေရန် ဖန်သားပြင် အပေါ်ဆုံးအစွန်းကို နှိပ်ပါ။"],
      iNote: "ကိရိယာဘား လုံးဝမရှိစေလိုလျှင် Share ကို နှိပ်၍ Add to Home Screen ကို ရွေးပါ၊ ထို့နောက် အိုင်ကွန်အသစ်မှ ဂိမ်းကို ဖွင့်ပါ။"
    }
  };

  var root = null, tab = "android", lastFocus = null;

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function lang() { return document.documentElement.getAttribute("lang") === "my" ? "my" : "en"; }
  function guessIOS() {
    var ua = navigator.userAgent || "";
    return /iPad|iPhone|iPod/.test(ua) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  }

  function sk(cls) { return el("i", "sk " + cls); }

  function stage(ios, s) {
    var st = el("div", "fsg-stage " + (ios ? "fsg-ios" : "fsg-android"));
    var phone = el("div", "fsg-phone");
    var screen = el("div", "fsg-screen");
    var page = el("div", "fsg-page");
    ["pic", "l1", "l2", "l3", "l4"].forEach(function (c) { page.appendChild(sk(c)); });
    if (!ios) page.appendChild(el("div", "fsg-btn", "Play Fullscreen"));
    screen.appendChild(page);

    var bar = el("div", "fsg-bar");
    if (ios) {
      bar.appendChild(el("div", "fsg-aa", "aA"));
      var u = el("div", "fsg-url"); u.style.left = "95px"; u.style.width = "420px"; bar.appendChild(u);
    } else {
      var u2 = el("div", "fsg-url"); u2.style.left = "16px"; u2.style.width = "520px"; bar.appendChild(u2);
      for (var i = 0; i < 3; i++) { var d = el("i", "fsg-dot"); d.style.top = (14 + i * 8) + "px"; bar.appendChild(d); }
    }
    screen.appendChild(bar);

    if (ios) {
      var menu = el("div", "fsg-menu");
      menu.appendChild(sk("r1")); menu.appendChild(sk("r3")); menu.appendChild(sk("r4"));
      menu.appendChild(el("div", "fsg-hot", "Hide Toolbar"));
      screen.appendChild(menu);
    }
    screen.appendChild(el("div", "fsg-badge", s.badge));
    screen.appendChild(el("div", "fsg-ring"));
    screen.appendChild(el("div", "fsg-finger"));
    phone.appendChild(screen);
    st.appendChild(phone);

    var caps = el("div", "fsg-caps");
    var list = ios ? [s.iCap1, s.iCap2, s.iCap3] : [s.aCap1, s.aCap2];
    list.forEach(function (c, n) { caps.appendChild(el("span", "c" + (n + 1), c)); });
    st.appendChild(caps);
    return st;
  }

  function fit() {
    if (!root) return;
    var box = root.querySelector(".fsg-fit");
    var st = box && box.firstChild;
    if (!st) return;
    var k = Math.min(1, box.clientWidth / 640);
    st.style.transform = "scale(" + k + ")";
    box.style.height = Math.round(400 * k) + "px";
  }

  function render() {
    var s = T[lang()];
    root.setAttribute("aria-label", s.title);
    root.querySelector(".fsg-top h2").textContent = s.title;
    root.querySelector(".fsg-x").setAttribute("aria-label", s.close);

    var tabs = root.querySelectorAll(".fsg-tabs button");
    tabs[0].textContent = s.android; tabs[1].textContent = s.ios;
    tabs[0].setAttribute("aria-selected", String(tab === "android"));
    tabs[1].setAttribute("aria-selected", String(tab === "ios"));

    var ios = tab === "ios";
    var box = root.querySelector(".fsg-fit");
    box.innerHTML = "";
    box.appendChild(stage(ios, s));

    var steps = root.querySelector(".fsg-steps");
    steps.innerHTML = "";
    steps.appendChild(el("h3", "", ios ? s.iHead : s.aHead));
    var ol = el("ol");
    (ios ? s.iSteps : s.aSteps).forEach(function (t) { ol.appendChild(el("li", "", t)); });
    steps.appendChild(ol);
    steps.appendChild(el("p", "fsg-note", ios ? s.iNote : s.aNote));
    var go = el("a", "fsg-go", s.go);
    go.href = "/play/";
    steps.appendChild(go);
    fit();
  }

  function build() {
    root = el("div", "fsg");
    root.setAttribute("role", "dialog");
    root.setAttribute("aria-modal", "true");
    root.setAttribute("data-no-i18n", "");      // it has its own two languages
    var card = el("div", "fsg-card");
    var top = el("div", "fsg-top");
    top.appendChild(el("h2"));
    var x = el("button", "fsg-x");
    x.type = "button";
    x.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>';
    x.addEventListener("click", close);
    top.appendChild(x);
    card.appendChild(top);

    var tabs = el("div", "fsg-tabs");
    tabs.setAttribute("role", "tablist");
    ["android", "ios"].forEach(function (name) {
      var b = el("button");
      b.type = "button";
      b.setAttribute("role", "tab");
      b.addEventListener("click", function () { tab = name; render(); });
      tabs.appendChild(b);
    });
    card.appendChild(tabs);

    var grid = el("div", "fsg-grid");
    grid.appendChild(el("div", "fsg-fit"));
    grid.appendChild(el("div", "fsg-steps"));
    card.appendChild(grid);
    root.appendChild(card);
    root.addEventListener("click", function (e) { if (e.target === root) close(); });
    document.body.appendChild(root);
    window.addEventListener("resize", fit);
    new MutationObserver(function () { if (root.classList.contains("on")) render(); })
      .observe(document.documentElement, { attributes: true, attributeFilter: ["lang"] });
  }

  function open() {
    if (!root) build();
    lastFocus = document.activeElement;
    tab = guessIOS() ? "ios" : "android";
    render();
    root.classList.add("on");
    fit();                                  // it can only be measured once it is shown
    document.documentElement.classList.add("fsg-open");
    root.querySelector(".fsg-x").focus();
  }

  function close() {
    if (!root) return;
    root.classList.remove("on");
    document.documentElement.classList.remove("fsg-open");
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  }

  document.addEventListener("click", function (e) {
    var t = e.target.closest ? e.target.closest("[data-fsg-open]") : null;
    if (!t) return;
    e.preventDefault();
    open();
  });
  document.addEventListener("keydown", function (e) { if (e.key === "Escape" && root && root.classList.contains("on")) close(); });

  window.LHFullscreenGuide = { open: open, close: close };
})();
