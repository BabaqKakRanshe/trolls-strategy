using System.Runtime.InteropServices;

namespace TrollStrategy.Support
{
    /// <summary>
    /// The page's half of a report in a WebGL player (TrollReport.jslib over the template's report.js): the
    /// browser log with GPU shader failures, the device and GPU limits, the page's "Отчёт" button and saving a
    /// file to the browser's downloads. Everywhere else it is unavailable and every call is a no-op.
    /// </summary>
    public static class BrowserReport
    {
        public const string File = "browser.txt";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string TrollReport_Text();
        [DllImport("__Internal")] private static extern int TrollReport_ShaderErrors();
        [DllImport("__Internal")] private static extern int TrollReport_TakeRequest();
        [DllImport("__Internal")] private static extern void TrollReport_Status(string message);
        [DllImport("__Internal")] private static extern int TrollReport_Download(string name, byte[] data, int length);

        public static bool Available => true;
        public static string Text() => TrollReport_Text();
        public static int ShaderErrors => TrollReport_ShaderErrors();
        public static bool TakeRequest() => TrollReport_TakeRequest() != 0;
        public static void Status(string message) => TrollReport_Status(message ?? string.Empty);
        public static bool Download(string name, byte[] data) =>
            data != null && TrollReport_Download(name, data, data.Length) != 0;
#else
        public static bool Available => false;
        public static string Text() => null;
        public static int ShaderErrors => 0;
        public static bool TakeRequest() => false;
        public static void Status(string message) { }
        public static bool Download(string name, byte[] data) => false;
#endif
    }
}
