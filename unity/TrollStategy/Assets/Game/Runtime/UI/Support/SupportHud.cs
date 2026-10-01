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
    /// tech panel (F8) and "send logs". Works before and without a game session; the bootstrap adds how the
    /// game looks for reports.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SupportHud : MonoBehaviour
    {
        [Header("Part documents nested under this one")]
        [SerializeField] private UIDocument _techInfo;

        private readonly FrameRateMeter _frames = new();
        private UIDocument _document;
        private TechInfoPanel _panel;
        private VisualElement _builtFrom;
        private Func<string> _gameState;
        private BugReporter _reporter;

        public TechInfoPanel Panel => _panel;
        public FrameRateMeter Frames => _frames;

        /// <summary>How the game looks right now, as text for reports; null leaves it out.</summary>
        public void Init(Func<string> gameState) => _gameState = gameState;

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
            if (!EnsurePanel()) return;
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) _panel.Toggle();
            _panel.Tick(Time.unscaledDeltaTime);
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
                () => SystemReport.Rows(build, _frames), SendReportAsync));
            _builtFrom = content;
            content?.EnableInClassList("tech-info--touch", IsTouchScreen());
            return true;
        }

        /// <summary>Phones and tablets, including a browser on them: tap the strip instead of F8.</summary>
        private static bool IsTouchScreen() =>
            UnityEngine.Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null);

        private async Task<ReportOutcome> SendReportAsync(Action<float> progress)
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
            var report = BugReport.Create(build, SystemReport.Rows(build, _frames), game, BugReport.LogPaths(),
                screenshot, DateTime.Now);
            _reporter ??= new BugReporter(new UnityReportUploader(), BugReporter.DefaultFolder, RevealFolder);
            return await _reporter.SendAsync(report, progress);
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
