using System;
using System.Threading.Tasks;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Support;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Scene end of the support corner: version and FPS above the colony and battle HUDs in every build, the
    /// tech panel (F8) with "send logs" and the play statistics switch, which also sends the statistics on
    /// time. Works before and without a game session; the bootstrap adds how the
    /// game looks for reports. In a web player the page's "Отчёт" button asks for a report too, and the first
    /// error of a session (a game error or a GPU shader failure) sends one by itself.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SupportHud : MonoBehaviour
    {
        [Header("Part documents nested under this one")]
        [SerializeField] private UIDocument _techInfo;

        /// <summary>A report goes by itself only for errors this early: later ones are the tester's to report.</summary>
        public const float AutoReportWindowSeconds = 120f;
        /// <summary>Wait after the first error, so the report also holds what followed it.</summary>
        public const float AutoReportDelaySeconds = 3f;

        private readonly FrameRateMeter _frames = new();
        private UIDocument _document;
        private TechInfoPanel _panel;
        private VisualElement _builtFrom;
        private Func<string> _gameState;
        private Action _openCheats;
        private BugReporter _reporter;
        private BugReporter _autoReporter;
        private bool _autoReported;
        private bool _pageRequestRunning;

        public TechInfoPanel Panel => _panel;
        public FrameRateMeter Frames => _frames;

        /// <summary>How the game looks right now, as text for reports; null leaves it out.</summary>
        public void Init(Func<string> gameState) => _gameState = gameState;

        /// <summary>Opens the developer cheat menu from the tech panel; null hides the button.</summary>
        public void SetCheats(Action open)
        {
            _openCheats = open;
            _panel?.SetCheats(open);
        }

        /// <summary>The tech panel's document root, taken by <paramref name="rootOf"/>.</summary>
        public VisualElement TechInfoRoot(Func<UIDocument, VisualElement> rootOf)
        {
            if (rootOf == null) throw new ArgumentNullException(nameof(rootOf));
            return _techInfo != null ? rootOf(_techInfo) : null;
        }

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            UIInputUtils.RegisterDocument(_document);
        }

        private void OnDisable() => UIInputUtils.UnregisterDocument(_document);

        private void Update()
        {
            _frames.Add(Time.unscaledDeltaTime);
            Telemetry.Game.Tick(Time.unscaledDeltaTime);
            // the page button works even when this screen cannot be drawn or read
            if (BrowserReport.TakeRequest()) SendForPage();
            CheckAutoReport();
            if (!EnsurePanel()) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) _panel.Toggle();
            _panel.Tick(Time.unscaledDeltaTime);
        }

        // a browser tab closes without warning: send what is recorded whenever the game loses focus
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Telemetry.Game.Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Telemetry.Game.Flush();
        }

        private bool EnsurePanel()
        {
            var root = TechInfoRoot(document => document.rootVisualElement);
            if (root == null || root.panel == null) return false;
            var content = root.childCount > 0 ? root[0] : null;
            // UI Builder live reload replaces the tree; build on the new one
            if (_panel != null && _builtFrom == content) return true;
            var build = BuildInfo.Current;
            _panel = new TechInfoPanel(root, new SupportContext(build, _frames,
                () => SystemReport.Rows(build, _frames), SendReportAsync, Telemetry.Game));
            _builtFrom = content;
            _panel.SetCheats(_openCheats);
            TrollStrategy.Presentation.Localization.TranslateTree(root);
            content?.EnableInClassList("tech-info--touch", IsTouchScreen());
            return true;
        }

        /// <summary>The menu's bug report: the tech panel opens and sends it, showing the progress.</summary>
        public void ReportFromMenu()
        {
            if (!EnsurePanel()) return;
            _panel.Show();
            _panel.SendReport();
        }

        /// <summary>Phones and tablets, including a browser on them: tap the strip instead of F8.</summary>
        private static bool IsTouchScreen() =>
            UnityEngine.Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null);

        private async void SendForPage()
        {
            if (_pageRequestRunning)
            {
                BrowserReport.Status("Отчёт уже собирается");
                return;
            }
            _pageRequestRunning = true;
            try
            {
                var outcome = await SendReportAsync(progress =>
                    BrowserReport.Status($"Отправляю… {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%"));
                BrowserReport.Status(outcome.Result switch
                {
                    ReportResult.Sent => "Отчёт отправлен",
                    ReportResult.Saved => "Отчёт сохранён в загрузки",
                    _ => "Отчёт не собран"
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Support] Page report failed: {exception.Message}");
                BrowserReport.Status("Отчёт не собран");
            }
            finally
            {
                _pageRequestRunning = false;
            }
        }

        private void CheckAutoReport()
        {
            if (_autoReported || UnityEngine.Application.isEditor) return;
            if (Time.realtimeSinceStartup > AutoReportWindowSeconds) return;
            int errors = (LogRecorder.Game?.Errors ?? 0) + BrowserReport.ShaderErrors;
            if (errors == 0) return;
            _autoReported = true;
            AutoReportAsync();
        }

        /// <summary>One report per session, sent only: no file lands in the player's downloads unasked.</summary>
        private async void AutoReportAsync()
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(AutoReportDelaySeconds);
                _autoReporter ??= new BugReporter(new UnityReportUploader(), BugReporter.DefaultFolder, null,
                    (_, _) => false);
                var outcome = await SendReportAsync(null, _autoReporter, "Автоотчёт");
                Debug.Log($"[Support] Automatic report after an error: {outcome.Result}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Support] Automatic report failed: {exception.Message}");
            }
        }

        private Task<ReportOutcome> SendReportAsync(Action<float> progress) =>
            SendReportAsync(progress, _reporter ??= new BugReporter(new UnityReportUploader(),
                BugReporter.DefaultFolder, RevealFolder, BrowserReport.Available ? BrowserReport.Download : null));

        private async Task<ReportOutcome> SendReportAsync(Action<float> progress, BugReporter reporter,
            string note = null)
        {
            byte[] screenshot = await CaptureScreenAsync();
            string game;
            try
            {
                game = _gameState?.Invoke();
            }
            catch (Exception exception)
            {
                game = "Не удалось описать игру: " + exception.Message;
            }
            var build = BuildInfo.Current;
            if (note != null) game = note + "\n\n" + game;
            var report = BugReport.Create(build, SystemReport.Rows(build, _frames), game, BugReport.LogPaths(),
                screenshot, DateTime.Now, LogRecorder.Game?.Text(), BrowserReport.Text());
            return await reporter.SendAsync(report, progress);
        }

        private static async Task<byte[]> CaptureScreenAsync()
        {
            try
            {
                await Awaitable.EndOfFrameAsync();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    return texture.EncodeToJPG(80);
                }
                finally
                {
                    Destroy(texture);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Support] No screenshot for the report: {exception.Message}");
                return null;
            }
        }

        private static void RevealFolder(string folder) => UnityEngine.Application.OpenURL(new Uri(folder).AbsoluteUri);
    }
}
