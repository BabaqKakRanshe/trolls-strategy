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
            // the bootstrap hands the game its translations; here the shots do
            Localization.Use(AssetDatabase.LoadAssetAtPath<LanguageTable>(TrollStrategy.Editor.Setup.UiSetup.LanguagesPath));
            var world = UnityEngine.Object.FindAnyObjectByType<TilemapWorldView>();
            var layout = SceneBuildingPlacements.Collect(world, catalog).Select(p => p.Building).ToList();
            var session = new GameSession(catalog, layout, campaign: true);
            session.EnableDebugBattleAccess();
            session.DebugAddGold(20000);
            session.DebugUnlockAllBuildings();
            session.DebugOpenArenaLadder();
            Build(session, BuildingKind.HaulersGuild);
            Build(session, BuildingKind.Tavern);
            session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 3, session.FindSpawnCell()));
            session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 2, session.FindSpawnCell()));
            var interaction = new InteractionController(session);

            var hud = UnityEngine.Object.FindAnyObjectByType<ColonyHud>(FindObjectsInactive.Include);
            if (hud == null) throw new InvalidOperationException("The scene has no ColonyHud");
            // the support corner is built only in play (its tech panel would stand open over the shots)
            var support = UnityEngine.Object.FindAnyObjectByType<SupportHud>(FindObjectsInactive.Include);
            var supportDocument = support != null ? support.GetComponent<UIDocument>() : null;
            if (supportDocument != null && supportDocument.rootVisualElement != null)
                supportDocument.rootVisualElement.style.display = DisplayStyle.None;
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

            // the battle's gear row with every item of the catalog, as a late campaign fills it (playtest item 26)
            var battleHud = UnityEngine.Object.FindAnyObjectByType<BattleHud>(FindObjectsInactive.Include);
            var mission = catalog.Missions.FirstOrDefault(candidate => candidate != null);
            BattleHudView battle = null;
            void ShowBattle(bool shown)
            {
                if (battle == null) return;
                battle.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                view.Root.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
            }
            void EnsureBattle()
            {
                if (battle != null) return;
                var gearSession = new GameSession(catalog, layout, campaign: true);
                gearSession.EnableDebugBattleAccess();
                gearSession.DebugAddGold(20000);
                gearSession.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 2, gearSession.FindSpawnCell()));
                gearSession.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 2, gearSession.FindSpawnCell()));
                gearSession.DebugGrantGear(3);
                var board = mission.CreateBoard();
                var deployment = new BattleDeployment(gearSession, mission, board);
                battle = new BattleHudView(battleHud.CollectRoots(document => document.rootVisualElement));
                battle.Open(deployment);
                deployment.ClickCell(mission.PlayerDeployment.First(cell =>
                    board.CanPlace(cell) && deployment.UnitAt(cell) == null));
            }

            // a game of the itch.io alpha played to its end, for the notice that closes the version
            GameSnapshot endOfAlpha = null;
            GameSnapshot EndOfAlpha()
            {
                var game = new GameSession(catalog, layout, campaign: true, catalog.Progression.AlphaLastQuestId);
                for (int i = 0; i < 100 && game.CurrentSnapshot.Progress.Quest != null; i++)
                {
                    game.DebugCompleteQuest();
                    game.Dispatch(new ClaimQuestRewardCommand());
                }
                return game.CurrentSnapshot;
            }

            // -hudLanguages ru,en limits the run to those languages
            var args = Environment.GetCommandLineArgs();
            int only = Array.IndexOf(args, "-hudLanguages");
            var languages = only >= 0 && only + 1 < args.Length ? args[only + 1].Split(',') : Languages;
            foreach (string language in languages)
            {
                string code = language;
                Pending.Enqueue(new Shot { Name = $"{code}-colony", Setup = () =>
                {
                    Ensure();
                    ShowBattle(false);
                    view.Wiki?.Close();
                    Localization.Select(code);
                    view.Refresh(session.CurrentSnapshot);
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-arena", Setup = () =>
                {
                    view.Arena.Open();
                    view.Arena.Refresh();
                } });
                // a milestone in gear with its champion, and a level above it, as the poster shows them
                foreach (int level in new[] { 10, 25 })
                {
                    int shown = level;
                    Pending.Enqueue(new Shot { Name = $"{code}-arena-{shown}", Setup = () =>
                    {
                        var buttons = view.Arena.LevelButtons;
                        if (buttons.Count >= shown) UiFeel.Press(buttons[shown - 1]);
                        view.Arena.Refresh();
                    } });
                }
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
                Pending.Enqueue(new Shot { Name = $"{code}-end", Setup = () =>
                {
                    view.Intro.Close();
                    view.DemoEnd.SetEdition(BuildEdition.Alpha);
                    view.DemoEnd.Open(endOfAlpha ??= EndOfAlpha());
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-end-demo", Setup = () => view.DemoEnd.SetEdition(BuildEdition.SteamDemo) });
                Pending.Enqueue(new Shot { Name = $"{code}-guild", Setup = () =>
                {
                    view.Intro.Close();
                    view.DemoEnd.Close();
                    var guild = session.CurrentSnapshot.Buildings.FirstOrDefault(b => b.Kind == BuildingKind.HaulersGuild);
                    if (guild != null) interaction.SelectBuilding(guild.Id);
                    view.Refresh(session.CurrentSnapshot);
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-catalog", Setup = () =>
                {
                    interaction.CancelOrClear();
                    interaction.CloseInspect();
                    var buildings = view.Root.Q<Button>("tab-buildings");
                    if (buildings != null) UiFeel.Press(buildings);
                    view.Refresh(session.CurrentSnapshot);
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-catalog-page2", Setup = () => view.Catalog.BuildingPages.Turn(1) });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-creatures", Setup = () =>
                {
                    view.Catalog.BuildingPages.Turn(-1);
                    view.Wiki?.Open();
                    view.Wiki?.Go(WikiSection.Creatures, UnitKind.Dwarf.ToString());
                } });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-buildings", Setup = () => view.Wiki?.Go(WikiSection.Buildings, BuildingKind.Forge.ToString()) });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-goods", Setup = () => view.Wiki?.Go(WikiSection.Goods, ResourceKind.Leather.ToString()) });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-upgrades", Setup = () => view.Wiki?.Show(WikiSection.Upgrades) });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-arena", Setup = () => view.Wiki?.Go(WikiSection.Arena, "11") });
                if (battleHud != null && mission != null)
                    Pending.Enqueue(new Shot { Name = $"{code}-battle-gear", Setup = () =>
                    {
                        view.Wiki?.Close();
                        EnsureBattle();
                        battle.Gear.Pages.Turn(-battle.Gear.Pages.Page);
                        ShowBattle(true);
                        battle.Refresh();
                    } });
                if (battleHud != null && mission != null)
                    Pending.Enqueue(new Shot { Name = $"{code}-battle-gear-page2", Setup = () => battle.Gear.Pages.Turn(1) });
                Pending.Enqueue(new Shot { Name = $"{code}-wiki-search", Setup = () => view.Wiki?.Search(Localization.T("Кожа").Substring(0, Math.Min(3, Localization.T("Кожа").Length))) });
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
