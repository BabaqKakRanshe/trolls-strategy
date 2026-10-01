// Bridge to window.trollReport, set up by the TrollStrategy WebGL template (TemplateData/report.js).
// Every function copes with a page that has no such object.
mergeInto(LibraryManager.library, {
  TrollReport_Text: function () {
    var report = window.trollReport;
    var text = report ? report.text() : "";
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  TrollReport_ShaderErrors: function () {
    return window.trollReport ? window.trollReport.shaderErrors : 0;
  },

  TrollReport_TakeRequest: function () {
    var report = window.trollReport;
    if (!report || !report.requested) return 0;
    report.requested = false;
    return 1;
  },

  TrollReport_Status: function (message) {
    if (window.trollReport) window.trollReport.status(UTF8ToString(message));
  },

  TrollReport_Download: function (name, data, length) {
    if (!window.trollReport) return 0;
    var bytes = HEAPU8.slice(data, data + length);
    window.trollReport.download(UTF8ToString(name), bytes, "application/zip");
    return 1;
  }
});
