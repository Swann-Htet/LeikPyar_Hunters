mergeInto(LibraryManager.library, {

  // iPhone, iPad and iPod (an iPad with iPadOS says it is a Mac, but it has a touch screen)
  JS_IsIOS: function () {
    var ua = navigator.userAgent || '';
    var ios = /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    return ios ? 1 : 0;
  },

  JS_CanFullscreen: function () {
    var el = document.documentElement;
    return (el.requestFullscreen || el.webkitRequestFullscreen || el.msRequestFullscreen) ? 1 : 0;
  },

  JS_IsFullscreenOrStandalone: function () {
    var fs = document.fullscreenElement || document.webkitFullscreenElement;
    var standalone = (window.navigator.standalone === true) ||
      (window.matchMedia && window.matchMedia('(display-mode: fullscreen), (display-mode: standalone)').matches);
    return (fs || standalone) ? 1 : 0;
  },

  JS_RequestFullscreen: function () {
    var el = document.documentElement;
    var req = el.requestFullscreen || el.webkitRequestFullscreen || el.msRequestFullscreen;
    if (!req) return 0;
    try {
      var p = req.call(el, { navigationUI: 'hide' });
      var lockLandscape = function () {
        try {
          if (screen.orientation && screen.orientation.lock)
            screen.orientation.lock('landscape').catch(function () {});
        } catch (e) {}
      };
      if (p && p.then) p.then(lockLandscape).catch(function () {});
      else lockLandscape();
    } catch (e) { return 0; }
    return 1;
  },

  // Browsers only allow fullscreen inside a real user tap. Unity handles taps a
  // frame later, so this listens for the very next tap directly in the browser.
  JS_ArmFullscreenOnTouch: function () {
    if (window.__lordsFsArmed) return;
    window.__lordsFsArmed = true;

    var handler = function () {
      document.removeEventListener('touchend', handler, true);
      document.removeEventListener('click', handler, true);
      window.__lordsFsArmed = false;

      var el = document.documentElement;
      var req = el.requestFullscreen || el.webkitRequestFullscreen || el.msRequestFullscreen;
      if (!req) return;
      try {
        var p = req.call(el, { navigationUI: 'hide' });
        var lockLandscape = function () {
          try {
            if (screen.orientation && screen.orientation.lock)
              screen.orientation.lock('landscape').catch(function () {});
          } catch (e) {}
        };
        if (p && p.then) p.then(lockLandscape).catch(function () {});
        else lockLandscape();
      } catch (e) {}
    };

    document.addEventListener('touchend', handler, true);
    document.addEventListener('click', handler, true);
  },

  // Phone sizing: exact viewport, no page scroll / pull-to-refresh / zoom,
  // and "Add to Home Screen" opens without the browser bar (iPhone).
  JS_ApplyMobileFixes: function () {
    if (window.__lordsMobileFixes) return;
    window.__lordsMobileFixes = true;

    var head = document.head;

    var vp = document.querySelector('meta[name=viewport]');
    if (!vp) {
      vp = document.createElement('meta');
      vp.name = 'viewport';
      head.appendChild(vp);
    }
    vp.content = 'width=device-width, height=device-height, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover';

    var addMeta = function (name, content) {
      if (document.querySelector('meta[name="' + name + '"]')) return;
      var m = document.createElement('meta');
      m.name = name;
      m.content = content;
      head.appendChild(m);
    };
    addMeta('apple-mobile-web-app-capable', 'yes');
    addMeta('mobile-web-app-capable', 'yes');
    addMeta('apple-mobile-web-app-status-bar-style', 'black-translucent');
    addMeta('theme-color', '#000000');

    var style = document.createElement('style');
    style.textContent =
      'html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:#000;' +
      'overscroll-behavior:none;touch-action:none;-webkit-touch-callout:none;-webkit-user-select:none;user-select:none;}' +
      '#unity-container.unity-mobile{position:fixed;left:0;top:0;right:0;bottom:0;width:100vw;height:100vh;height:100dvh;}' +
      '#unity-container.unity-mobile #unity-canvas,canvas.unity-mobile{width:100%;height:100%;display:block;}';
    head.appendChild(style);

    document.addEventListener('touchmove', function (e) { e.preventDefault(); }, { passive: false });
    document.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    document.addEventListener('gesturestart', function (e) { e.preventDefault(); });
    window.scrollTo(0, 1);

    // Back-swipe / back button should not leave the game
    try {
      history.pushState({ lords: 1 }, '', location.href);
      window.addEventListener('popstate', function () {
        history.pushState({ lords: 1 }, '', location.href);
      });
    } catch (e) {}

    // Keep landscape locked and go fullscreen again on the next tap if the player left it
    var relock = function () {
      var fs = document.fullscreenElement || document.webkitFullscreenElement;
      if (fs) {
        try {
          if (screen.orientation && screen.orientation.lock)
            screen.orientation.lock('landscape').catch(function () {});
        } catch (e) {}
        return;
      }
      var el = document.documentElement;
      if (!(el.requestFullscreen || el.webkitRequestFullscreen)) return;
      var again = function () {
        document.removeEventListener('touchend', again, true);
        var req = el.requestFullscreen || el.webkitRequestFullscreen;
        try { var p = req.call(el, { navigationUI: 'hide' }); if (p && p.catch) p.catch(function () {}); } catch (e) {}
      };
      document.addEventListener('touchend', again, true);
    };
    document.addEventListener('fullscreenchange', relock);
    document.addEventListener('webkitfullscreenchange', relock);
  }
});
