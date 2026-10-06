using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The tutorial pointer's next press on the deployment screen, for the battle steps of the tutorial: the
    /// automatic placement, a fighter on the board and its gear while the quest asks for gear worn in battle, then the
    /// start. Counted on from the colony's two steps (the arena tool, its fight button), see <see cref="ColonyGuide"/>.
    /// Worked out from the quest and the deployment; it keeps nothing.
    /// </summary>
    public static class BattleGuide
    {
        /// <summary>The steps of a battle goal: arena, fight, automatic placement, start.</summary>
        public const int BattleSteps = 4;
        /// <summary>The steps of a gear goal: arena, fight, automatic placement, a fighter, an item, start.</summary>
        public const int GearSteps = 6;

        public static GuideStep Resolve(BattleHudView hud, BattleDeployment deployment)
        {
            if (hud == null || deployment == null || !hud.IsDeploying || !GameSettings.TutorialHints) return GuideStep.None;
            var progress = deployment.Session.CurrentSnapshot.Progress;
            var quest = progress.Enabled ? progress.Quest : null;
            if (quest == null || !quest.IsTutorial || quest.IsComplete) return GuideStep.None;
            var next = quest.NextGoal;
            if (next == null) return GuideStep.None;
            var goal = next.Goal;
            bool gear = goal.Kind == QuestGoalKind.WearGearInBattle;
            if (!gear && goal.Kind != QuestGoalKind.WinBattles && goal.Kind != QuestGoalKind.ReachArenaLevel)
                return GuideStep.None;
            int count = gear ? GearSteps : BattleSteps;
            string key = quest.Id + ":battle";

            if (deployment.Placements.Count == 0)
                return Step(GuideTarget.Of(hud.Actions.AutoPlace), "Авторасстановка", "Бойцы сами встанут на синие клетки.", 3,
                    count, key + ":auto");

            if (gear && WornInSquad(deployment) < goal.Amount)
            {
                string selected = deployment.SelectedUnitId;
                var item = selected != null ? FreeItemFor(deployment, selected) : null;
                if (item != null)
                {
                    var button = hud.Gear.ItemButton(item.Id);
                    if (button != null)
                        return Step(GuideTarget.Of(button), "Снаряжение", $"Нажми на «{item.DisplayName}»: боец наденет его.", 5,
                            count, key + ":item:" + item.DefinitionId);
                }
                // a fighter with a free slot that a free item fits
                foreach (var placement in deployment.Placements)
                {
                    if (placement.UnitId == selected || FreeItemFor(deployment, placement.UnitId) == null) continue;
                    return Step(GuideTarget.Fighter(placement.Cell), "Выбери бойца",
                        "Кликни по бойцу на поле: внизу появится его снаряжение.", 4, count, key + ":fighter:" + placement.UnitId);
                }
            }
            return Step(GuideTarget.Of(hud.Actions.Start), "Начать бой", "Бой идёт сам.", count, count, key + ":start");
        }

        // items on fighters standing on the board
        private static int WornInSquad(BattleDeployment deployment)
        {
            var placed = new HashSet<string>();
            foreach (var placement in deployment.Placements) placed.Add(placement.UnitId);
            int worn = 0;
            foreach (var item in deployment.Equipment)
            {
                string owner = deployment.OwnerOf(item.Id);
                if (owner != null && placed.Contains(owner)) worn++;
            }
            return worn;
        }

        // a free item for a slot the fighter has empty
        private static EquipmentSnapshot FreeItemFor(BattleDeployment deployment, string unitId)
        {
            var used = new HashSet<EquipmentSlot>();
            foreach (var item in deployment.Equipment)
                if (deployment.OwnerOf(item.Id) == unitId) used.Add(item.Slot);
            foreach (var item in deployment.Equipment)
                if (deployment.OwnerOf(item.Id) == null && !used.Contains(item.Slot)) return item;
            return null;
        }

        private static GuideStep Step(GuideTarget target, string title, string text, int number, int count, string key) =>
            target.Kind == GuideTargetKind.None ? GuideStep.None : new GuideStep(target, title, text, number, count, true, key);
    }
}
