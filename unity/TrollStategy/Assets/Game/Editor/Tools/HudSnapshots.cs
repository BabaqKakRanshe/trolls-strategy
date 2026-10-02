using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Support;
using TrollStrategy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Editor.Tools
{
    /// <summary>
    /// Pictures of the colony HUD without Play Mode, for checking layouts and fonts: the scene's HUD documents
    /// render into a texture over a sandbox colony, in a few states (the ladder, the menu, the notice, the guild's
    /// card) and languages. Batch entry: -executeMethod TrollStrategy.Editor.Tools.HudSnapshots.RunBatch
    /// -hudSnapshots &lt;folder&gt; (with graphics, no -quit). Writes PNGs and exits.
    /// </summary>
    [InitializeOnLoad]
    public static class HudSnapshots
    {
        // a domain reload after the batch entry (a recompile, an import) drops the update callback: start again
        static HudSnapshots()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hudSnapshots") >= 0 && UnityEditorInternal.InternalEditorUtility.inBatchMode)
                EditorApplication.delayCall += RunBatch;
        }

        private static bool s_started;
        private const string ScenePath = "Assets/Game/Scenes/MainColonyScene.unity";
        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";

        private static readonly string[] Languages = { "ru", "en", "ja", "hi", "zh", "ko", "kk" };

        private sealed class Shot
        {
            public string Name;
            public Action Setup;
        }

        private static readonly Queue<Shot> Pending = new();
        private static string s_folder;
        private static RenderTexture s_texture;
        private static PanelSettings s_panel;
        // the panel settings are the game's asset, saved when the batch editor quits: put back all we change
        private static RenderTexture s_originalTarget;
        private static bool s_originalClear;
        private static Color s_originalClearValue;
        private static double s_shotAt;
        private static Shot s_current;
        private static int s_exit;

        public static void RunBatch()
        {
            if (s_started) return;
            s_started = true;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-hudSnapshots");
            s_folder = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Builds/HudSnapshots");
            try
            {
                Prepare();
                EditorApplication.update += Tick;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RestorePanel();
                EditorApplication.Exit(1);
            }
        }

        private static void Prepare()
        {
            Directory.CreateDirectory(s_folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            var world = UnityEngine.Object.FindAnyObjectByType<TilemapWorldView>();
            var layout = SceneBuildingPlacements.Collect(world, catalog).Select(p => p.Building).ToList();
            var session = new GameSession(catalog, layout, campaign: true);
            session.EnableDebugBattleAccess();
            session.DebugAddGold(20000);
            session.DebugUnlockAllBuildings();
            Build(session, BuildingKind.HaulersGuild);
            Build(session, BuildingKind.Tavern);
            session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 3, session.FindSpawnCell()));
            session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 2, session.FindSpawnCell()));
            var interaction = new InteractionController(session);

            var hud = UnityEngine.Object.FindAnyObjectByType<ColonyHud>(FindObjectsInactive.Include);
            if (hud == null) throw new InvalidOperationException("The scene has no ColonyHud");
            var document = hud.GetComponent<UIDocument>();
            s_panel = document.panelSettings;
            s_originalTarget = s_panel.targetTexture;
            s_originalClear = s_panel.clearColor;
            s_originalClearValue = s_panel.colorClearValue;
            s_texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            s_panel.targetTexture = s_texture;
            s_panel.clearColor = true;
            s_panel.colorClearValue = new Color(.62f, .78f, .58f, 1f);

            var context = new ColonyHudContext(session, interaction)
            {
                OpenBattle = _ => { },
                ToggleGuides = () => { },
                GuidesVisible = () => false,
                Languages = Localization.Languages,
                CurrentLanguage = () => Localization.Current
            };
            ColonyHudView view = null;
            void Ensure()
            {
                if (view != null) return;
                view = new ColonyHudView(hud.CollectRoots(d => d.rootVisualElement), context);
            }

            foreach (string language in Languages)
            {
                string code = language;
                Pending.Enqueue(new Shot { Name = $"{code}-colony", Setup = () =>
                {
                    Ensure();
                    Localization.Select(code);
                    view.Refresh(session.CurrentSnapshot);
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-arena", Setup = () =>
                {
                    view.Arena.Open();
                    view.Arena.Refresh();
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-menu", Setup = () =>
                {
                    view.Arena.Close();
                    view.Menu.Open();
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-settings", Setup = () => view.Menu.Show(MenuPage.Settings) });
                Pending.Enqueue(new Shot { Name = $"{code}-languages", Setup = () => view.Menu.Show(MenuPage.Languages) });
                Pending.Enqueue(new Shot { Name = $"{code}-about", Setup = () => view.Menu.Show(MenuPage.About) });
                Pending.Enqueue(new Shot { Name = $"{code}-restart", Setup = () => view.Menu.Show(MenuPage.Restart) });
                Pending.Enqueue(new Shot { Name = $"{code}-intro", Setup = () =>
                {
                    view.Menu.Close();
                    view.Intro.SetEdition(BuildEdition.Alpha);
                    view.Intro.Open();
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-intro-demo", Setup = () => view.Intro.SetEdition(BuildEdition.SteamDemo) });
                Pending.Enqueue(new Shot { Name = $"{code}-guild", Setup = () =>
                {
                    view.Intro.Close();
                    var guild = session.CurrentSnapshot.Buildings.FirstOrDefault(b => b.Kind == BuildingKind.HaulersGuild);
                    if (guild != null) interaction.SelectBuilding(guild.Id);
                    view.Refresh(session.CurrentSnapshot);
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-catalog", Setup = () =>
                {
                    interaction.CancelOrClear();
                    interaction.CancelOrClear();
                    view.Refresh(session.CurrentSnapshot);
                } });
            }
        }

        private static void Build(GameSession session, BuildingKind kind)
        {
            var cell = session.FindFirstBuildingCell(kind);
            if (cell != null) session.Dispatch(new BuildBuildingCommand(kind, cell.Value));
        }

        // each shot: set up, let the panel lay out and repaint for a few editor frames, then read the texture
        private static void Tick()
        {
            try
            {
                if (s_current == null)
                {
                    if (Pending.Count == 0)
                    {
                        Finish();
                        return;
                    }
                    s_current = Pending.Dequeue();
                    s_current.Setup();
                    // pop-ins and counters settle in under a second
                    s_shotAt = EditorApplication.timeSinceStartup + 1.2;
                    return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < s_shotAt) return;
                var previous = RenderTexture.active;
                RenderTexture.active = s_texture;
                var image = new Texture2D(s_texture.width, s_texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, s_texture.width, s_texture.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(s_folder, s_current.Name + ".png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("[HudSnapshots] " + s_current.Name);
                s_current = null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                s_exit = 1;
                Pending.Clear();
                s_current = null;
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            RestorePanel();
            Localization.Unload();
            GameSettings.SetLanguage(string.Empty);
            Debug.Log("[HudSnapshots] done");
            EditorApplication.Exit(s_exit);
        }

        // a clear colour left on the asset paints over the whole world in the game
        private static void RestorePanel()
        {
            if (s_panel == null) return;
            s_panel.targetTexture = s_originalTarget;
            s_panel.clearColor = s_originalClear;
            s_panel.colorClearValue = s_originalClearValue;
        }
    }
}
