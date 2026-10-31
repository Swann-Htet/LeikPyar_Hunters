mergeInto(LibraryManager.library, {
  JS_IsMobileBrowser: function () {
    var ua = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase();
    var uaIsMobile = /android|iphone|ipad|ipod|iemobile|blackberry|bada|webos|mobile/.test(ua);
    var hasTouch = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);

    // iPadOS Safari reports a desktop Mac user agent, so treat a touch-capable
    // "Macintosh" UA with multiple touch points as an iPad.
    var isIpadOS = /macintosh/.test(ua) && hasTouch && navigator.maxTouchPoints > 1;

    return (uaIsMobile || isIpadOS) ? 1 : 0;
  }
});
