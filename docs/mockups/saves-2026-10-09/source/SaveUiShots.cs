using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Bots.SaveUi
{
    /// <summary>
    /// Private copy only (C:\tmp\TrollSaveUi): real Play-mode frames for the save-game UI mockups. Batch with
    /// graphics, no -quit: -executeMethod TrollStrategy.Bots.SaveUi.SaveUiShots.RunBatch -saveUiShots &lt;folder&gt;.
    /// A bot plays the live colony to a mid-campaign level (thumbnails of the island on the way), then the tool
    /// waits for commands in &lt;folder&gt;/cmd.txt: "shoot a b c" or "shoot all", "uss" (reimport styles),
    /// "refresh" (leave Play, reimport and recompile, come back), "exit". Each shot is world + HUD at
    /// 1920x1080 (&lt;name&gt;.png) with &lt;name&gt;.json: the rects of the parts the mockup marks.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveUiShots
    {
        private const string Flag = "-saveUiShots";
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string StageKey = "SaveUiShots.stage";
        public const int W = 1920, H = 1080;

        static SaveUiShots()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0 ||
                !UnityEditorInternal.InternalEditorUtility.inBatchMode) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Log("driver loaded, stage " + Stage);
        }

        public static void RunBatch() { }

        private static int Stage
        {
            get => SessionState.GetInt(StageKey, 0);
            set => SessionState.SetInt(StageKey, value);
        }

        public static string Folder
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, Flag);
                return at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Builds/SaveUiShots");
            }
        }

        private static double s_wait;
        private static double s_lastTick;
        private static double s_nextPoll;
        private static readonly Queue<SaveUiMocks.Shot> Shots = new();
        private static SaveUiMocks.Shot s_current;
        private static int s_step;
        private static double s_stepAt;
        private static RenderTexture s_ui, s_world;
        private static PanelSettings s_panel;
        private static RenderTexture s_panelTarget;
        private static bool s_panelClear;
        private static Color s_panelClearValue;
        private static Vector2Int s_panelResolution;
        private static GameBootstrap s_boot;
        private static ColonyHud s_hud;
        private static Camera s_camera;
        private static Kit s_kit;
        private static string s_lastMessage = "";

        public static void Log(string text)
        {
            s_lastMessage = text;
            Debug.Log("[SaveUiShots] " + text);
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "driver.log"), DateTime.Now.ToString("HH:mm:ss") + " " + text + "\n");
            }
            catch (Exception)
            {
                // the log is a convenience
            }
        }

        private static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                switch (Stage)
                {
                    case 0:
                        Directory.CreateDirectory(Folder);
                        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                        Stage = 1;
                        s_wait = now;
                        EditorApplication.EnterPlaymode();
                        Log("enter play 1");
                        break;
                    case 1:
                        // the first entry reloads scripts once in batch: leave and come back before driving the game
                        if (!EditorApplication.isPlaying || now < s_wait + 3) return;
                        Stage = 2;
                        EditorApplication.ExitPlaymode();
                        break;
                    case 2:
                        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                        Stage = 3;
                        s_wait = now;
                        EditorApplication.EnterPlaymode();
                        Log("enter play 2");
                        break;
                    case 3:
                        if (!EditorApplication.isPlaying || now < s_wait + 4) return;
                        Prepare();
                        Stage = 4;
                        Log("ready");
                        break;
                    case 4:
                        if (!EditorApplication.isPlaying)
                        {
                            Log("play stopped from outside; entering again");
                            RestorePanel();
                            Stage = 0;
                            return;
                        }
                        Run(now);
                        break;
                    case 5:
                        // refresh: out of Play, reimport (a script change reloads the domain and the ctor re-arms)
                        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                        Stage = 0;
                        AssetDatabase.Refresh();
                        Log("refreshed");
                        break;
                    case 6:
                        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                        Log("exit");
                        Stage = 99;
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Log("ERROR " + exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace);
                // a broken shot is skipped; a broken start leaves the editor waiting for commands
                s_current = null;
                if (Stage == 3) Stage = 4;
            }
        }

        // ---------------------------------------------------------------- commands and shots

        private static void Run(double now)
        {
            float dt = (float)Math.Min(.1, Math.Max(0, now - s_lastTick));
            s_lastTick = now;
            var view = s_hud.View;
            view.TrackPointer(null, false);
            view.Tick(dt);

            if (s_current == null && now >= s_nextPoll)
            {
                s_nextPoll = now + .5;
                ReadCommands();
                WriteStatus();
            }
            if (s_current == null)
            {
                if (Shots.Count == 0) return;
                s_current = Shots.Dequeue();
                s_step = 0;
                s_kit.Reset();
                s_kit.Pins.Clear();
                Log("shot " + s_current.Name);
                s_current.Steps[0](s_kit);
                s_stepAt = now + s_current.Wait;
                return;
            }
            if (now < s_stepAt) return;
            s_step++;
            if (s_step < s_current.Steps.Length)
            {
                s_current.Steps[s_step](s_kit);
                s_stepAt = now + s_current.Wait;
                return;
            }
            Capture(s_current.Name, s_current.WorldOnly);
            WriteRects(s_current.Name);
            s_kit.Reset();
            s_current = null;
        }

        private static void ReadCommands()
        {
            string path = Path.Combine(Folder, "cmd.txt");
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            File.Delete(path);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "shoot":
                        var names = parts.Skip(1).ToList();
                        foreach (var shot in SaveUiMocks.All)
                            if (names.Contains("all") || names.Contains(shot.Name) ||
                                names.Any(n => n.EndsWith("*") && shot.Name.StartsWith(n.TrimEnd('*'))))
                                Shots.Enqueue(shot);
                        Log($"queued {Shots.Count} shots for '{line}'");
                        break;
                    case "uss":
                        AssetDatabase.Refresh();
                        Log("styles reimported");
                        break;
                    case "refresh":
                        Shots.Clear();
                        RestorePanel();
                        Stage = 5;
                        EditorApplication.ExitPlaymode();
                        Log("refresh: leaving play");
                        return;
                    case "exit":
                        Shots.Clear();
                        RestorePanel();
                        Stage = 6;
                        EditorApplication.ExitPlaymode();
                        return;
                    default:
                        Log("unknown command " + line);
                        break;
                }
            }
        }

        private static void WriteStatus()
        {
            try
            {
                File.WriteAllText(Path.Combine(Folder, "status.txt"),
                    $"{DateTime.Now:HH:mm:ss} stage {Stage} queue {Shots.Count} last '{s_lastMessage}'\n");
            }
            catch (Exception)
            {
                // status is a convenience
            }
        }

        // ---------------------------------------------------------------- the colony

        private static void Prepare()
        {
            s_boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            s_hud = UnityEngine.Object.FindAnyObjectByType<ColonyHud>();
            if (s_boot == null || s_boot.Session == null || s_hud == null || s_hud.View == null)
                throw new InvalidOperationException("The game did not start");
            s_camera = s_boot.ColonyCamera;
            var rig = s_camera.GetComponent<IslandCameraRig>();
            var view = s_hud.View;
            var session = s_boot.Session;

            var support = UnityEngine.Object.FindAnyObjectByType<SupportHud>();
            if (support != null)
            {
                var doc = support.GetComponent<UIDocument>();
                if (doc != null && doc.rootVisualElement != null) doc.rootVisualElement.style.display = DisplayStyle.None;
            }
            // the notice is hidden, not closed: closing writes "seen" into the PlayerPrefs the real project shares
            Ui.Show(view.Root.Q("intro-overlay"), false);
            if (rig != null) rig.SkipIntro();
            s_hud.enabled = false;

            var document = s_hud.GetComponent<UIDocument>();
            s_panel = document.panelSettings;
            s_panelTarget = s_panel.targetTexture;
            s_panelClear = s_panel.clearColor;
            s_panelClearValue = s_panel.colorClearValue;
            s_panelResolution = s_panel.referenceResolution;
            s_ui = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            s_panel.targetTexture = s_ui;
            s_panel.clearColor = true;
            s_panel.colorClearValue = new Color(0, 0, 0, 0);
            s_panel.referenceResolution = new Vector2Int(W, H);
            s_world = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            s_camera.targetTexture = s_world;

            // the colony at three moments of one campaign: the start, the tutorial's end, mid-game now
            var kit = new Kit { View = view, Session = session, Catalog = session.Catalog, Camera = s_camera, Rig = rig };
            int[] stops = StopLevels();
            // the guides (cell grid, routes) are on for a new colony; the pictures show the island without them
            view.Refresh(session.CurrentSnapshot);
            var guides = view.Root.Q<Button>("grid-button");
            if (guides != null && guides.ClassListContains("is-on")) UiFeel.Press(guides);
            Play(session, stops[0]);
            kit.Early = Facts(session, rig, "early");
            Play(session, stops[1]);
            kit.Mid = Facts(session, rig, "mid");
            Play(session, stops[2]);
            CloseBattle();
            kit.Now = Facts(session, rig, "now");
            Log($"colony: {kit.Early} | {kit.Mid} | {kit.Now}");

            if (view.Reward.IsOpen) view.Reward.Hide();
            var grid = view.Root.Q<Button>("grid-button");
            if (grid != null && grid.ClassListContains("is-on")) UiFeel.Press(grid);
            if (rig != null)
            {
                rig.FrameOwned(true);
                rig.enabled = false;
            }
            view.Refresh(session.CurrentSnapshot);
            // the colony stands still between shots, so every picture tells the same numbers
            Time.timeScale = 0f;
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/UI/Styles/SaveMock.uss");
            if (sheet != null) view.Root.styleSheets.Add(sheet);
            else Log("SaveMock.uss is missing");
            s_kit = kit;
        }

        private static int[] StopLevels()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-saveUiLevels");
            if (at >= 0 && at + 1 < args.Length)
            {
                var parts = args[at + 1].Split(',');
                if (parts.Length == 3 && int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b) &&
                    int.TryParse(parts[2], out int c))
                    return new[] { a, b, c };
            }
            return new[] { 4, 9, 13 };
        }

        private static void Play(GameSession session, int stopAfter)
        {
            var started = DateTime.Now;
            try
            {
                var bot = new CampaignBot(session, BotPopulation.Persona(1), 240f, 30f) { StopAfterLevel = stopAfter };
                var run = bot.Run();
                Log($"bot to level {stopAfter}: {run.Outcome} in {(DateTime.Now - started).TotalSeconds:0} s");
            }
            catch (Exception exception)
            {
                Log("bot failed: " + exception.Message);
            }
        }

        private static ColonyFacts Facts(GameSession session, IslandCameraRig rig, string name)
        {
            var snapshot = session.CurrentSnapshot;
            var quest = snapshot.Progress.Quest;
            var facts = new ColonyFacts
            {
                Name = name,
                QuestId = quest?.Id,
                Level = quest?.Level ?? 0,
                Total = session.Catalog.Progression.Quests.Count,
                Title = quest?.Title ?? "",
                Tutorial = quest != null && quest.IsTutorial,
                TutorialStep = quest?.TutorialStep ?? 0,
                TutorialSteps = quest?.TutorialSteps ?? 0,
                Gold = snapshot.Gold,
                Creatures = snapshot.Units.Count,
                Buildings = snapshot.Buildings.Count,
                Land = snapshot.Land != null ? snapshot.Land.Blocks.Count(b => b.Owned) : 1,
                Battles = session.BattlesWon,
                Arena = session.HighestMissionLevel,
                PlayMs = session.ActiveTimeMs
            };
            facts.Thumb = Thumb(rig, name);
            return facts;
        }

        // a picture of the island as it is now: the colony camera over the owned land into a small texture
        private static Texture2D Thumb(IslandCameraRig rig, string name)
        {
            if (rig != null)
            {
                rig.enabled = true;
                rig.FrameOwned(true);
                // a little closer than the play framing: the colony fills the small picture
                rig.Frame(rig.Target, rig.Side * .78f, true);
            }
            var rt = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            var previous = s_camera.targetTexture;
            s_camera.targetTexture = rt;
            s_camera.Render();
            s_camera.targetTexture = previous;
            var texture = Read(rt);
            texture.name = "thumb-" + name;
            File.WriteAllBytes(Path.Combine(Folder, "thumb-" + name + ".png"), texture.EncodeToPNG());
            rt.Release();
            return texture;
        }

        private static void CloseBattle()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<BattleSceneController>();
            if (controller == null) return;
            controller.gameObject.SendMessage("Close", SendMessageOptions.DontRequireReceiver);
            Log("closed a battle the bot left open");
        }

        // ---------------------------------------------------------------- pictures

        private static void Capture(string name, bool worldOnly)
        {
            s_camera.Render();
            var w = Read(s_camera.targetTexture);
            int blur = worldOnly ? 0 : BlurRadius();
            if (blur > 0)
            {
                // URP blurs the screen under an overlay's backdrop-filter; a panel drawn into a texture cannot,
                // so the world is blurred here by the overlay's radius (three box passes ~ a Gaussian)
                var px = w.GetPixels32();
                for (int pass = 0; pass < 3; pass++) BoxBlur(px, w.width, w.height, blur);
                w.SetPixels32(px);
                w.Apply();
            }
            if (!worldOnly)
            {
                var u = Read(s_ui);
                var a = w.GetPixels32();
                var b = u.GetPixels32();
                for (int i = 0; i < a.Length; i++)
                {
                    // the panel is premultiplied: ui + world x (1 - alpha)
                    float k = 1f - b[i].a / 255f;
                    a[i] = new Color32((byte)Mathf.Min(255, b[i].r + a[i].r * k), (byte)Mathf.Min(255, b[i].g + a[i].g * k),
                        (byte)Mathf.Min(255, b[i].b + a[i].b * k), 255);
                }
                w.SetPixels32(a);
                w.Apply();
                UnityEngine.Object.DestroyImmediate(u);
            }
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), w.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(w);
            Log("wrote " + name);
        }

        private static void WriteRects(string name)
        {
            var text = new StringBuilder();
            text.Append("{\"pins\":[");
            for (int i = 0; i < s_kit.Pins.Count; i++)
            {
                var (label, element) = s_kit.Pins[i];
                var r = element.worldBound;
                if (i > 0) text.Append(',');
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "{{\"label\":\"{0}\",\"x\":{1:0.#},\"y\":{2:0.#},\"w\":{3:0.#},\"h\":{4:0.#}}}",
                    label.Replace("\"", "'"), r.x, r.y, r.width, r.height));
            }
            text.Append("]}");
            File.WriteAllText(Path.Combine(Folder, name + ".json"), text.ToString());
        }

        // backdrop-filter of the open overlay: .overlay 6 px, .overlay--deep 10 px (Theme.uss)
        private static int BlurRadius()
        {
            var root = s_hud.View.Root;
            bool Shown(string name)
            {
                var element = root.Q(name);
                return element != null && element.resolvedStyle.display != DisplayStyle.None &&
                       element.resolvedStyle.opacity > .01f;
            }
            if (Shown("intro-overlay") || Shown("reward-overlay")) return 10;
            if (Shown("menu-overlay")) return 6;
            return 0;
        }

        private static void BoxBlur(Color32[] px, int w, int h, int r)
        {
            var tmp = new Color32[px.Length];
            int n = r * 2 + 1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w, sr = 0, sg = 0, sb = 0;
                for (int i = -r; i <= r; i++)
                {
                    var c = px[row + Mathf.Clamp(i, 0, w - 1)];
                    sr += c.r; sg += c.g; sb += c.b;
                }
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
                    var add = px[row + Mathf.Min(x + r + 1, w - 1)];
                    var sub = px[row + Mathf.Max(x - r, 0)];
                    sr += add.r - sub.r; sg += add.g - sub.g; sb += add.b - sub.b;
                }
            }
            for (int x = 0; x < w; x++)
            {
                int sr = 0, sg = 0, sb = 0;
                for (int i = -r; i <= r; i++)
                {
                    var c = tmp[Mathf.Clamp(i, 0, h - 1) * w + x];
                    sr += c.r; sg += c.g; sb += c.b;
                }
                for (int y = 0; y < h; y++)
                {
                    px[y * w + x] = new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255);
                    var add = tmp[Mathf.Min(y + r + 1, h - 1) * w + x];
                    var sub = tmp[Mathf.Max(y - r, 0) * w + x];
                    sr += add.r - sub.r; sg += add.g - sub.g; sb += add.b - sub.b;
                }
            }
        }

        public static Texture2D Read(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = previous;
            return t;
        }

        private static void RestorePanel()
        {
            Time.timeScale = 1f;
            if (s_panel == null) return;
            s_panel.targetTexture = s_panelTarget;
            s_panel.clearColor = s_panelClear;
            s_panel.colorClearValue = s_panelClearValue;
            s_panel.referenceResolution = s_panelResolution;
            if (s_camera != null) s_camera.targetTexture = null;
            s_panel = null;
        }
    }

    /// <summary>The numbers a save's header would carry, read from the live colony at one moment.</summary>
    public sealed class ColonyFacts
    {
        public string Name, QuestId, Title;
        public int Level, Total, TutorialStep, TutorialSteps;
        public bool Tutorial;
        public int Gold, Creatures, Buildings, Land, Battles, Arena, PlayMs;
        public Texture2D Thumb;

        /// <summary>The quest line in the tracker's words, with the chain's length.</summary>
        public string Chapter => Tutorial
            ? $"Обучение, уровень {TutorialStep} из {TutorialSteps}"
            : $"Задание, уровень {Level} из {Total}";

        public string PlayTime
        {
            get
            {
                int minutes = Math.Max(1, PlayMs / 60000);
                return minutes >= 60 ? $"{minutes / 60} ч {minutes % 60} мин" : $"{minutes} мин";
            }
        }

        public override string ToString() =>
            $"{Name}: L{Level} '{Title}' gold {Gold} units {Creatures} buildings {Buildings} land {Land} wins {Battles} arena {Arena} {PlayTime}";
    }

    /// <summary>What a mockup step may touch, and what it changed, so the next shot starts from the real HUD.</summary>
    public sealed class Kit
    {
        public ColonyHudView View;
        public GameSession Session;
        public GameContentCatalog Catalog;
        public Camera Camera;
        public IslandCameraRig Rig;
        public ColonyFacts Early, Mid, Now;
        public readonly List<(string Label, VisualElement Element)> Pins = new();
        private readonly List<VisualElement> _added = new();
        private readonly List<(VisualElement Element, StyleEnum<DisplayStyle> Display)> _hidden = new();
        private readonly List<(TextElement Element, string Text)> _texts = new();
        private readonly List<(VisualElement Element, string Class, bool Had)> _classes = new();
        private readonly List<VisualElement> _tooltipTargets = new();

        public VisualElement Root => View.Root;

        /// <summary>The hint card HudTooltip shows (one per HUD).</summary>
        public VisualElement TooltipCard => View.Root.Q(className: "tooltip");

        public VisualElement Q(string name) =>
            View.Root.Q(name) ?? throw new InvalidOperationException("No element '" + name + "'");

        public T Q<T>(string name) where T : VisualElement =>
            View.Root.Q<T>(name) ?? throw new InvalidOperationException("No " + typeof(T).Name + " '" + name + "'");

        public T Add<T>(VisualElement parent, T child, int index = -1) where T : VisualElement
        {
            if (index < 0 || index > parent.childCount) parent.Add(child);
            else parent.Insert(index, child);
            _added.Add(child);
            return child;
        }

        public void Hide(VisualElement element)
        {
            if (element == null) return;
            _hidden.Add((element, element.style.display));
            element.style.display = DisplayStyle.None;
        }

        public void Show(VisualElement element)
        {
            if (element == null) return;
            _hidden.Add((element, element.style.display));
            element.style.display = DisplayStyle.Flex;
        }

        public void Text(TextElement element, string text)
        {
            _texts.Add((element, element.text));
            Ui.SetText(element, text);
        }

        public void Class(VisualElement element, string name, bool on)
        {
            _classes.Add((element, name, element.ClassListContains(name)));
            element.EnableInClassList(name, on);
        }

        public void Pin(VisualElement element, string label) => Pins.Add((label, element));

        public void Tooltip(VisualElement target, string title, string body, string key = null)
        {
            _tooltipTargets.Add(target);
            View.Tooltip.Show(target, title, body, key);
        }

        public void Reset()
        {
            foreach (var target in _tooltipTargets) View.Tooltip.Hide(target);
            _tooltipTargets.Clear();
            for (int i = _added.Count - 1; i >= 0; i--) _added[i].RemoveFromHierarchy();
            _added.Clear();
            for (int i = _texts.Count - 1; i >= 0; i--) Ui.SetText(_texts[i].Element, _texts[i].Text);
            _texts.Clear();
            for (int i = _classes.Count - 1; i >= 0; i--) _classes[i].Element.EnableInClassList(_classes[i].Class, _classes[i].Had);
            _classes.Clear();
            for (int i = _hidden.Count - 1; i >= 0; i--) _hidden[i].Element.style.display = _hidden[i].Display;
            _hidden.Clear();
            if (View.Menu.IsOpen) View.Menu.Close();
            Ui.Show(View.Root.Q("intro-overlay"), false);
            if (View.Reward.IsOpen) View.Reward.Hide();
            Time.timeScale = 0f;
        }
    }
}
