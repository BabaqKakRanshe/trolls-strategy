using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Editor.Tests
{
    public static class ColonyGameplayVerification
    {
        [MenuItem("TrollStrategy/Run Automated Gameplay Test")]
        public static void RunAllTests()
        {
            Debug.Log("<color=cyan>========================================</color>");
            Debug.Log("<color=cyan>[ColonyGameplayVerification] Starting Full Gameplay Test Suite...</color>");

            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/Game/Content/Definitions/GameContentCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[TEST FAILED] GameContentCatalog not found!");
                return;
            }

            int passed = 0;
            int total = 0;

            void Assert(bool condition, string testName, string errorDetails = "")
            {
                total++;
                if (condition)
                {
                    passed++;
                    Debug.Log($"<color=green>[PASS]</color> {testName}");
                }
                else
                {
                    string extra = string.IsNullOrEmpty(errorDetails) ? "" : $" -> {errorDetails}";
                    Debug.LogError($"<color=red>[FAIL]</color> {testName}{extra}");
                }
            }

            // 1. Initial State
            var session = new GameSession(catalog);
            var s0 = session.CurrentSnapshot;
            Assert(s0.Gold == 1000, "Initial Gold is 1000");
            Assert(s0.Buildings.Count == 2, "Initial Buildings count is 2 (Warehouse & Market)");
            Assert(s0.Units.Count == 0, "Initial Units count is 0");

            // 2. Build Mine
            var buildResult = session.Dispatch(new BuildMineCommand(new Cell(3, 3)));
            Assert(buildResult.Ok, "Build Mine Command succeeded at (3, 3)", buildResult.Error);
            var s1 = session.CurrentSnapshot;
            Assert(s1.Gold == 800, "Gold decreased to 800 after Mine construction (200g)");
            Assert(s1.Buildings.Count == 3, "Building count is now 3");

            // 3. Overlap validation
            var invalidBuild = session.Dispatch(new BuildMineCommand(new Cell(3, 3)));
            Assert(!invalidBuild.Ok, "Building on existing building is rejected");

            // 4. Boundary validation
            var outOfBoundsBuild = session.Dispatch(new BuildMineCommand(new Cell(13, 13)));
            Assert(!outOfBoundsBuild.Ok, "Building outside map bounds is rejected");

            // 5. Buy Goblins at free cell (7, 7)
            var buyGoblins = session.Dispatch(new BuyUnitsCommand(UnitKind.Goblin, 3, new Cell(7, 7)));
            Assert(buyGoblins.Ok, "Hired 3 Goblins at (7, 7)", buyGoblins.Error);
            var s2 = session.CurrentSnapshot;
            Assert(s2.Gold == 680, "Gold decreased to 680 (3 * 40g = 120g)");
            Assert(s2.Units.Count == 3, "Unit count is now 3");

            // 6. Buy Troll at free cell (8, 7)
            var buyTroll = session.Dispatch(new BuyUnitsCommand(UnitKind.Troll, 1, new Cell(8, 7)));
            Assert(buyTroll.Ok, "Hired 1 Troll at (8, 7)", buyTroll.Error);
            var s3 = session.CurrentSnapshot;
            Assert(s3.Gold == 510, "Gold decreased to 510 (170g)");
            Assert(s3.Units.Count == 4, "Unit count is now 4");

            // 7. Assign Work
            var workCmd = session.Dispatch(new AssignWorkCommand(new[] { "unit-1", "unit-2" }, "mine-1"));
            Assert(workCmd.Ok, "Assigned unit-1 and unit-2 to work at mine-1", workCmd.Error);

            // 8. Advance time to arrive at work
            for (int i = 0; i < 30; i++)
                session.Advance(0.25f);

            var s4 = session.CurrentSnapshot;
            var u1 = s4.Units[0];
            var u2 = s4.Units[1];
            Assert(u1.Assignment.Kind == AssignmentKind.Work, $"unit-1 arrived (state: {u1.Assignment.Kind})");
            Assert(u2.Assignment.Kind == AssignmentKind.Work, $"unit-2 arrived (state: {u2.Assignment.Kind})");

            // 9. Advance time to produce ore
            float initialOre = s4.Buildings.FirstOrDefault(b => b.Id == "mine-1")?.Ore ?? 0;
            for (int i = 0; i < 30; i++)
                session.Advance(0.25f);

            var s5 = session.CurrentSnapshot;
            var mineSnap = s5.Buildings.FirstOrDefault(b => b.Id == "mine-1");
            Assert(mineSnap != null && (mineSnap.ProductionPerSecond > 0 || mineSnap.Ore > initialOre), "Mine is actively producing ore from workers");

            // 10. Test Haul Route: Mine -> Warehouse
            session.InternalState.Buildings.Find(b => b.Id == "mine-1").Ore = 25;
            var haulCmd = session.Dispatch(new AssignHaulCommand(new[] { "unit-3" }, "mine-1", "warehouse-1"));
            Assert(haulCmd.Ok, "Assigned unit-3 to haul Mine -> Warehouse", haulCmd.Error);

            // Run hauler ticks
            for (int i = 0; i < 60; i++)
                session.Advance(0.25f);

            var s6 = session.CurrentSnapshot;
            var whSnap = s6.Buildings.FirstOrDefault(b => b.Id == "warehouse-1");
            Assert(whSnap != null && whSnap.Ore > 0, $"Warehouse received ore from hauler (Ore: {whSnap?.Ore})");

            // 11. Test Haul Route: Warehouse -> Market (Selling ore for gold!)
            int goldBeforeSelling = s6.Gold;
            var sellHaulCmd = session.Dispatch(new AssignHaulCommand(new[] { "unit-4" }, "warehouse-1", "market-1"));
            Assert(sellHaulCmd.Ok, "Assigned unit-4 (Troll) to haul Warehouse -> Market", sellHaulCmd.Error);

            // Run seller ticks
            for (int i = 0; i < 60; i++)
                session.Advance(0.25f);

            var s7 = session.CurrentSnapshot;
            Assert(s7.Gold > goldBeforeSelling, $"Gold increased by selling ore at Market ({goldBeforeSelling} -> {s7.Gold})");
            Assert(s7.SoldOre > 0, $"SoldOre metric increased ({s7.SoldOre})");

            // 12. Release Units
            var relCmd = session.Dispatch(new ReleaseUnitsCommand(new[] { "unit-1" }));
            Assert(relCmd.Ok, "Released unit-1 from job", relCmd.Error);
            var s8 = session.CurrentSnapshot;
            var relUnit = s8.Units.FirstOrDefault(u => u.Id == "unit-1");
            Assert(relUnit != null && relUnit.Assignment.Kind == AssignmentKind.Idle, "unit-1 is now Idle");

            Debug.Log("<color=cyan>========================================</color>");
            Debug.Log($"<color={(passed == total ? "green" : "red")}><b>[TEST RUN COMPLETE] Passed: {passed} / {total} tests</b></color>");
            Debug.Log("<color=cyan>========================================</color>");
        }
    }
}
