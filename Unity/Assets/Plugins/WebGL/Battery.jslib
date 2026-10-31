// The battery level of the phone / laptop for the status bar (WebGL).
// Chrome and Android browsers support it. iPhone Safari does not, then the battery is simply not shown.
mergeInto(LibraryManager.library, {

  JS_BatteryInit: function () {
    try {
      if (window.__lordsBattery === undefined && navigator.getBattery) {
        window.__lordsBattery = null;
        navigator.getBattery().then(function (b) { window.__lordsBattery = b; }).catch(function () {});
      }
    } catch (e) {}
  },

  // 0 - 100, or -1 when the browser does not tell
  JS_BatteryPercent: function () {
    var b = window.__lordsBattery;
    return b ? Math.round(b.level * 100) : -1;
  },

  JS_BatteryCharging: function () {
    var b = window.__lordsBattery;
    return (b && b.charging) ? 1 : 0;
  }
});
