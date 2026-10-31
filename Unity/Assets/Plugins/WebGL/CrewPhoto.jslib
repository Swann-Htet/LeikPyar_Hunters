// Loads a picture from the server in the browser (the browser can read PNG, JPG and WebP), cuts the middle square out of it, makes it 384 x 384
// and sends it to Unity as a JPG in base64: "<slot>|<base64>". Used by CreditsScreen for the photos of the real people.
mergeInto(LibraryManager.library, {

  JS_LoadCrewPhoto: function (urlPtr, slotPtr, objectPtr, methodPtr) {
    var url = UTF8ToString(urlPtr);
    var slot = UTF8ToString(slotPtr);
    var objectName = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);

    var image = new Image();
    image.onload = function () {
      try {
        var size = 384;
        var canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        var side = Math.min(image.naturalWidth, image.naturalHeight);
        var sx = (image.naturalWidth - side) / 2;
        var sy = (image.naturalHeight - side) / 2;
        canvas.getContext('2d').drawImage(image, sx, sy, side, side, 0, 0, size, size);
        var data = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];

        var send = (typeof SendMessage === 'function') ? SendMessage : (typeof Module !== 'undefined' ? Module.SendMessage : null);
        if (send) send(objectName, method, slot + '|' + data);
      } catch (e) {
        console.warn('crew photo: ' + e);
      }
    };
    image.onerror = function () { console.warn('crew photo could not be loaded: ' + url); };
    image.src = url;
  }
});
