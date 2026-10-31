// A picture from the game goes to the player: the phone's own share sheet (Instagram, Messenger, Telegram...) when the browser has one,
// otherwise the picture is downloaded as a file.
mergeInto(LibraryManager.library, {

  // data = the PNG bytes, size = how many, name = the file name
  LH_ShareImage: function (data, size, namePtr, textPtr, urlPtr) {
    var name = UTF8ToString(namePtr), text = UTF8ToString(textPtr), url = UTF8ToString(urlPtr);
    var bytes = new Uint8Array(HEAPU8.buffer, data, size).slice();   // a copy: the game's memory can move
    var blob = new Blob([bytes], { type: 'image/png' });

    function download() {
      try {
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 4000);
      } catch (e) {}
    }

    try {
      var file = new File([blob], name, { type: 'image/png' });
      if (navigator.share && navigator.canShare && navigator.canShare({ files: [file] })) {
        navigator.share({ files: [file], title: 'Leikpyar Hunters', text: text, url: url }).catch(function (e) {
          // the player closed the sheet: nothing to do. Any other problem: the file is downloaded instead
          if (!e || e.name !== 'AbortError') download();
        });
        return;
      }
    } catch (e) {}
    download();
  }
});
