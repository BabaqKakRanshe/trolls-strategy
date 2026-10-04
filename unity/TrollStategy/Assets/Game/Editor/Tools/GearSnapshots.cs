using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Editor.Setup;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.WorldUi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Editor.Tools
{
    /// <summary>
    /// Pictures of the squad's gear tokens without Play Mode: the first mission's board on its arena, framed as the
    /// battle camera frames it, a placed squad wearing gear, rendered by the camera. Shots: the deployment, the
    /// deployment with a helmet slot, a blow. Batch entry: -executeMethod TrollStrategy.Editor.Tools.GearSnapshots.RunBatch
    /// -gearSnapshots &lt;folder&gt; (with graphics, no -quit). Writes PNGs and exits.
    /// </summary>
    [InitializeOnLoad]
    public static class GearSnapshots
    {
        static GearSnapshots()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gearSnapshots") >= 0 && UnityEditorInternal.InternalEditorUtility.inBatchMode)
                EditorApplication.delayCall += RunBatch;
        }

        private const string CatalogPath = "Assets/Game/Content/Definitions/GameContentCatalog.asset";
        private static bool s_started;
        private static string s_folder;
        private static Camera s_camera;
        private static BattleBoardView s_board;
        private static readonly Queue<(string Name, Action Setup)> Pending = new();
        private static (string Name, Action Setup)? s_current;
        private static double s_shotAt;
        private static double s_last;
        private static int s_exit;

        public static void RunBatch()
        {
            if (s_started) return;
            s_started = true;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-gearSnapshots");
            s_folder = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Builds/GearSnapshots");
            try
            {
                Prepare();
                s_last = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Prepare()
        {
            Directory.CreateDirectory(s_folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WorldPanel.Configure(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiSetup.WorldPanelSettingsPath));
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            var mission = catalog.Missions.Where(m => m != null).OrderBy(m => m.Level).First();
            var board = mission.CreateBoard();

            s_camera = new GameObject("BattleCamera", typeof(Camera)).GetComponent<Camera>();
            s_camera.clearFlags = CameraClearFlags.SolidColor;
            s_camera.backgroundColor = new Color32(53, 67, 82, 255);
            s_camera.targetTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var light = new GameObject("BattleSun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            s_board = new GameObject("BattleWorld", typeof(BattleBoardView)).GetComponent<BattleBoardView>();
            s_board.Init(board, mission, s_camera, Vector3.zero, catalog);
            if (s_board.Arena != null) new BattleArenaLighting().Apply(s_board.Arena, light);
            Frame(s_camera, s_board);

            // the squad: a troll and three goblins on the left cells
            var kinds = new[] { UnitKind.Troll, UnitKind.Goblin, UnitKind.Goblin, UnitKind.Goblin };
            var cells = mission.PlayerDeployment.Take(kinds.Length).ToList();
            var placements = new List<BattlePlacement>();
            var kindOf = new Dictionary<string, UnitKind>(StringComparer.Ordinal);
            for (int i = 0; i < cells.Count; i++)
            {
                string id = $"unit-{i}";
                placements.Add(new BattlePlacement(id, cells[i]));
                kindOf[id] = kinds[i];
            }
            s_board.ShowPlayerPlacements(placements, kindOf);
            s_board.ShowSelection("unit-0");

            WornItem Item(string id)
            {
                var definition = catalog.Equipment.First(e => e != null && e.ItemId == id);
                return new WornItem(definition.Slot, definition.Icon, definition.Enchanted);
            }
            var gear = new Dictionary<string, List<WornItem>>(StringComparer.Ordinal)
            {
                ["unit-0"] = new() { Item("iron-sword"), Item("patched-armor") },
                ["unit-1"] = new() { Item("rusty-sword") },
                ["unit-3"] = new() { Item("enchanted-sword"), Item("wooden-shield") },
            };
            var slots = catalog.Equipment.Where(e => e != null).Select(e => e.Slot).Distinct().OrderBy(s => s).ToList();
            var withHelmet = new List<EquipmentSlot> { EquipmentSlot.Weapon, EquipmentSlot.Armor, EquipmentSlot.Helmet };

            Pending.Enqueue(("gear-deploy", () => s_board.ShowGear(gear, slots, true)));
            Pending.Enqueue(("gear-deploy-helmet-slot", () => s_board.ShowGear(gear, withHelmet, true)));
            Pending.Enqueue(("gear-battle-blow", () =>
            {
                s_board.ShowGear(gear, slots, false);
                s_board.ShowSelection(null);
                var striker = UnityEngine.Object.FindObjectsByType<BattleFighterView>(FindObjectsSortMode.None)
                    .First(f => f.Id == "unit-3");
                striker.BeginAttack(striker.Ground + Vector3.right, true);
                // the shot lands while the token is at its biggest
                s_shotAt = EditorApplication.timeSinceStartup + .3;
            }));
        }

        // the arena's own perspective shot at 16:9, as BattleSceneController.FrameCamera does
        private static void Frame(Camera camera, BattleBoardView board)
        {
            var arena = board.Arena;
            if (arena == null)
            {
                camera.transform.position = board.BoardCenter + new Vector3(0f, 9f, -9f);
                camera.transform.LookAt(board.BoardCenter);
                return;
            }
            camera.fieldOfView = arena.FieldOfView;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.backgroundColor = arena.Background;
            float aspect = 16f / 9f;
            float halfWidth = Mathf.Tan(arena.FieldOfView * .5f * Mathf.Deg2Rad) * aspect;
            float fit = board.BoardSize.x / (2f * arena.BoardWidthShare * halfWidth);
            float distance = Mathf.Max(arena.Distance, fit);
            float pitch = arena.Pitch * Mathf.Deg2Rad;
            var focus = board.BoardCenter + new Vector3(0f, 0f, arena.FocusOffset);
            camera.transform.position = focus + new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * distance;
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
        }

        // edit mode runs no Update: drive the board, the fighters and the world panels by hand
        private static void Step(float dt)
        {
            Invoke(s_board, "Update");
            foreach (var fighter in UnityEngine.Object.FindObjectsByType<BattleFighterView>(FindObjectsSortMode.None))
            {
                fighter.Advance(dt);
                Invoke(fighter, "LateUpdate");
            }
            foreach (var panel in UnityEngine.Object.FindObjectsByType<WorldPanel>(FindObjectsSortMode.None))
                Invoke(panel, "LateUpdate");
        }

        private static void Invoke(object target, string method) =>
            target?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(target, null);

        private static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                float dt = Mathf.Clamp((float)(now - s_last), 0f, .05f);
                s_last = now;
                Step(dt);
                EditorApplication.QueuePlayerLoopUpdate();
                if (s_current == null)
                {
                    if (Pending.Count == 0)
                    {
                        EditorApplication.update -= Tick;
                        Debug.Log("[GearSnapshots] done");
                        EditorApplication.Exit(s_exit);
                        return;
                    }
                    s_shotAt = now + 1.5;
                    s_current = Pending.Dequeue();
                    s_current.Value.Setup();
                    return;
                }
                if (now < s_shotAt) return;
                s_camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = s_camera.targetTexture;
                var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(s_folder, s_current.Value.Name + ".png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("[GearSnapshots] " + s_current.Value.Name);
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
    }
}
