// Browser half of the bug report. Loads before Unity and keeps what Unity cannot see itself: console errors,
// page errors, WebGL shader compile and link failures, and what the device and its GPU can do. The game reads
// it through TrollReport.jslib; the "Отчёт" button on the page asks the game for a report and, when the game
// does not answer, saves the browser part alone.
(function () {
  "use strict";
  var MAX_LINES = 1000;
  var MAX_TEXT = 4000;
  var started = Date.now();
  var lines = [];
  var report = {
    shaderErrors: 0,
    errors: 0,
    requested: false
  };

  function stamp() {
    return "+" + ((Date.now() - started) / 1000).toFixed(1) + "s";
  }

  function text(value) {
    if (value instanceof Error) return value.name + ": " + value.message + (value.stack ? "\n" + value.stack : "");
    if (typeof value === "string") return value;
    try {
      return JSON.stringify(value);
    } catch (e) {
      return String(value);
    }
  }

  function push(kind, message) {
    message = String(message);
    if (message.length > MAX_TEXT) message = message.slice(0, MAX_TEXT) + " …";
    lines.push(stamp() + " [" + kind + "] " + message);
    if (lines.length > MAX_LINES) lines.splice(0, lines.length - MAX_LINES);
    if (kind === "error" || kind === "page" || kind === "shader") report.errors++;
  }
  report.push = push;

  ["error", "warn"].forEach(function (level) {
    var original = console[level];
    console[level] = function () {
      try {
        push(level, Array.prototype.map.call(arguments, text).join(" "));
      } catch (e) { /* the log must never break the game */ }
      return original.apply(console, arguments);
    };
  });

  window.addEventListener("error", function (event) {
    push("page", (event.message || "error") + (event.filename ? " at " + event.filename + ":" + event.lineno : "") +
      (event.error && event.error.stack ? "\n" + event.error.stack : ""));
  });
  window.addEventListener("unhandledrejection", function (event) {
    push("page", "Unhandled promise: " + text(event.reason));
  });

  // Shader failures come from the GPU driver and reach neither Unity's log nor, on some browsers, the console.
  function firstLines(source, count) {
    return String(source || "").split("\n").slice(0, count).join("\n");
  }

  // Watch the answers the game itself asks for rather than asking right after compileShader/linkProgram:
  // an extra status query forces the driver to finish at once and defeats parallel shader compilation.
  var reported = typeof WeakSet === "function" ? new WeakSet() : null;

  function firstReport(object) {
    if (!reported) return true;
    if (reported.has(object)) return false;
    reported.add(object);
    return true;
  }

  function hookShaders(proto, name) {
    if (!proto || proto.__trollReport) return;
    proto.__trollReport = true;
    var shaderParameter = proto.getShaderParameter;
    proto.getShaderParameter = function (shader, pname) {
      var value = shaderParameter.call(this, shader, pname);
      try {
        if (pname === this.COMPILE_STATUS && !value && !this.isContextLost() && firstReport(shader)) {
          report.shaderErrors++;
          var kind = shaderParameter.call(this, shader, this.SHADER_TYPE) === this.VERTEX_SHADER ? "vertex" : "fragment";
          push("shader", name + " " + kind + " shader failed to compile:\n" + this.getShaderInfoLog(shader) +
            "\n--- source start ---\n" + firstLines(this.getShaderSource(shader), 40));
        }
      } catch (e) { /* keep going */ }
      return value;
    };
    var programParameter = proto.getProgramParameter;
    proto.getProgramParameter = function (program, pname) {
      var value = programParameter.call(this, program, pname);
      try {
        if (pname === this.LINK_STATUS && !value && !this.isContextLost() && firstReport(program)) {
          report.shaderErrors++;
          push("shader", name + " program failed to link:\n" + this.getProgramInfoLog(program));
        }
      } catch (e) { /* keep going */ }
      return value;
    };
  }
  if (window.WebGLRenderingContext) hookShaders(WebGLRenderingContext.prototype, "WebGL1");
  if (window.WebGL2RenderingContext) hookShaders(WebGL2RenderingContext.prototype, "WebGL2");

  function device() {
    var rows = [];
    function row(key, value) {
      rows.push(key + ": " + (value === undefined || value === null ? "—" : value));
    }
    row("Страница", location.href);
    row("Браузер", navigator.userAgent);
    row("Платформа", navigator.platform);
    row("Экран", screen.width + "×" + screen.height + ", окно " + innerWidth + "×" + innerHeight +
      ", DPR " + devicePixelRatio);
    row("Память устройства, ГБ", navigator.deviceMemory);
    row("Ядер", navigator.hardwareConcurrency);
    row("Касание", navigator.maxTouchPoints);
    try {
      var canvas = document.createElement("canvas");
      var gl = canvas.getContext("webgl2");
      var version = "WebGL2";
      if (!gl) {
        gl = canvas.getContext("webgl");
        version = gl ? "WebGL1" : "нет WebGL";
      }
      row("API", version);
      if (gl) {
        var info = gl.getExtension("WEBGL_debug_renderer_info");
        row("GPU", info ? gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER));
        row("Вендор GPU", info ? gl.getParameter(info.UNMASKED_VENDOR_WEBGL) : gl.getParameter(gl.VENDOR));
        row("Версия GL", gl.getParameter(gl.VERSION));
        row("GLSL", gl.getParameter(gl.SHADING_LANGUAGE_VERSION));
        row("Текстур во фрагментном шейдере", gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS));
        row("Текстур всего", gl.getParameter(gl.MAX_COMBINED_TEXTURE_IMAGE_UNITS));
        row("Uniform-векторов, фрагмент", gl.getParameter(gl.MAX_FRAGMENT_UNIFORM_VECTORS));
        row("Uniform-векторов, вершины", gl.getParameter(gl.MAX_VERTEX_UNIFORM_VECTORS));
        row("Varying-векторов", gl.getParameter(gl.MAX_VARYING_VECTORS));
        row("Размер текстуры", gl.getParameter(gl.MAX_TEXTURE_SIZE));
        var high = gl.getShaderPrecisionFormat(gl.FRAGMENT_SHADER, gl.HIGH_FLOAT);
        row("highp во фрагменте", high && high.precision > 0 ? "да (" + high.precision + " бит)" : "нет");
        row("Расширения", (gl.getSupportedExtensions() || []).join(", "));
        var lose = gl.getExtension("WEBGL_lose_context");
        if (lose) lose.loseContext();
      }
    } catch (e) {
      row("WebGL", "ошибка: " + e.message);
    }
    return rows.join("\n");
  }

  report.text = function () {
    return "== Устройство ==\n" + device() +
      "\n\n== Ошибки шейдеров: " + report.shaderErrors + ", всего ошибок: " + report.errors + " ==\n\n" +
      "== Журнал браузера ==\n" + lines.join("\n") + "\n";
  };

  report.download = function (name, bytes, type) {
    var blob = new Blob([bytes], { type: type || "application/octet-stream" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = name;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    setTimeout(function () {
      URL.revokeObjectURL(url);
      link.remove();
    }, 10000);
  };

  // The page button: the game takes the request on its next frame; if it does not within a few seconds
  // (stopped, crashed, never started), the browser part is saved on its own.
  report.request = function () {
    report.requested = true;
    report.status("Собираю отчёт…");
    setTimeout(function () {
      if (!report.requested) return;
      report.requested = false;
      var name = "report-browser-" + new Date().toISOString().replace(/[:.]/g, "-") + ".txt";
      report.download(name, report.text(), "text/plain;charset=utf-8");
      report.status("Игра не ответила — сохранён журнал браузера");
    }, 4000);
  };

  report.status = function (message) {
    var button = document.getElementById("troll-report");
    if (!button) return;
    button.textContent = message;
    clearTimeout(report._statusTimer);
    report._statusTimer = setTimeout(function () { button.textContent = "Сообщить об ошибке"; }, 5000);
  };

  window.trollReport = report;
})();
