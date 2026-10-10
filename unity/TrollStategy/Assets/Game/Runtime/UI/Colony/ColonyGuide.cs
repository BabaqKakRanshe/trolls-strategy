using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Visuals;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The tutorial pointer's next press in the colony, worked out from the current quest's next goal, the
    /// interaction (mode, selection, inspected building) and what the HUD shows (catalog, cards, dialogs). It keeps
    /// nothing: the same snapshot and screen always give the same step, also after a cheat or a press the pointer did
    /// not ask for. Steps of one order count together ("Шаг 2 из 3"); a step that only prepares another (open the
    /// catalog, its tab) takes that step's number. See specs/006-tutorial-guidance/data-model.md.
    /// </summary>
    public static class ColonyGuide
    {
        /// <summary>The keys of the controls lesson's steps start with this.</summary>
        public const string LessonKey = "controls";

        public static GuideStep Resolve(ColonyHudView hud, ColonyHudContext context, GameSnapshot snapshot)
        {
            if (hud == null || context == null || snapshot == null || !GameSettings.TutorialHints) return GuideStep.None;
            // the camera's flight over the island comes first: nothing covers it
            if (context.CameraBusy?.Invoke() == true) return GuideStep.None;
            var progress = snapshot.Progress;
            var quest = progress.Enabled ? progress.Quest : null;
            if (quest == null || !quest.IsTutorial || quest.IsComplete) return GuideStep.None;
            // a dialog that holds the screen, or a reward on its way, comes first
            if (hud.Reward.IsOpen || hud.BattleReward.IsOpen || hud.Menu.IsOpen || hud.Intro.IsOpen ||
                hud.Intro.AsksToRotate || hud.Wiki?.IsOpen == true) return GuideStep.None;
            // before the first press: the camera, the zoom and the keys
            if (hud.Lesson != null && !hud.Lesson.IsDone) return Lesson(hud.Lesson);
            var next = quest.NextGoal;
            if (next == null) return GuideStep.None;
            var goal = next.Goal;
            string key = quest.Id + ":" + IndexOf(quest.Goals, next);
            bool battle = goal.Kind == QuestGoalKind.WinBattles || goal.Kind == QuestGoalKind.ReachArenaLevel ||
                          goal.Kind == QuestGoalKind.WearGearInBattle;
            if (hud.Arena.IsOpen && !battle) return GuideStep.None;
            var guide = new Context(hud, context, snapshot, key);
            switch (goal.Kind)
            {
                case QuestGoalKind.OwnUnits:
                    return goal.AnyUnit ? GuideStep.None : guide.Hire(goal.Unit, key);
                case QuestGoalKind.OwnBuildings:
                    return guide.Build(goal.Building);
                case QuestGoalKind.WorkAt:
                    return guide.Work(goal);
                case QuestGoalKind.HaulRoute:
                    return guide.Haul(goal);
                default:
                    return battle ? guide.Arena(goal) : GuideStep.None;
            }
        }

        // the controls lesson: a card alone, with its keys; no veil, the island is what the player looks at
        private static GuideStep Lesson(ControlsLesson lesson)
        {
            switch (lesson.Current)
            {
                case ControlsLesson.Stage.Move:
                    return new GuideStep(GuideTarget.Card, "Осмотри остров",
                        "Двигай камеру клавишами W A S D или стрелками, или тяни карту правой кнопкой мыши.", 1, 3, false,
                        LessonKey + ":move", new[]
                        {
                            GuideKey.Of("W"), GuideKey.Of("A"), GuideKey.Of("S"), GuideKey.Of("D"), GuideKey.RightButton
                        });
                case ControlsLesson.Stage.Zoom:
                    return new GuideStep(GuideTarget.Card, "Ближе и дальше",
                        "Крути колесо мыши: остров приблизится или отдалится.", 2, 3, false, LessonKey + ":zoom",
                        new[] { GuideKey.Wheel });
                default:
                    var keys = new List<GuideKey>();
                    foreach (var (key, name) in ControlsLesson.ToolKeys) keys.Add(GuideKey.Of(key.Label, name));
                    return new GuideStep(GuideTarget.Card, "Клавиши",
                        "Кнопки справа нажимаются и с клавиатуры. Попробуй любую.", 3, 3, false, LessonKey + ":keys", keys);
            }
        }

        private static int IndexOf(IReadOnlyList<GoalSnapshot> goals, GoalSnapshot goal)
        {
            for (int i = 0; i < goals.Count; i++)
                if (goals[i] == goal) return i;
            return 0;
        }

        private sealed class Context
        {
            private readonly ColonyHudView _hud;
            private readonly ColonyHudContext _context;
            private readonly GameSnapshot _snapshot;
            private readonly InteractionController _interaction;
            private readonly string _key;

            public Context(ColonyHudView hud, ColonyHudContext context, GameSnapshot snapshot, string key)
            {
                _hud = hud;
                _context = context;
                _snapshot = snapshot;
                _interaction = context.Interaction;
                _key = key;
            }

            // --- hire: the token, then the cell by the warehouse

            public GuideStep Hire(UnitKind kind, string key)
            {
                var mode = _interaction.Mode;
                if (mode.Type == InteractionModeType.PlacingUnits && mode.UnitKind == kind)
                {
                    var cell = TutorialPlaces.HireCell(_context.Session, kind, mode.Amount);
                    if (cell == null) return Cancel(2, 2, key);
                    return Step(GuideTarget.At(cell.Value, UnitArt(kind)), "Сюда",
                        "Кликни по клетке у склада: отсюда ближе всего носить.", 2, 2, key + ":cell");
                }
                var prepare = OpenCatalog(true, 1, 2, key);
                if (prepare != null) return prepare;
                var token = _hud.Catalog.HireButton(kind);
                if (token == null) return GuideStep.None;
                return Step(GuideTarget.Of(token), _snapshot.Units.Count == 0 ? "Найми первого работника" : "Найми работника",
                    $"Нажми на жетон «{UnitName(kind)}».", 1, 2, key + ":token");
            }

            // --- build: the token, then the place by the warehouse (or "Поставить сам")

            public GuideStep Build(BuildingKind kind)
            {
                var mode = _interaction.Mode;
                var definition = _context.Catalog.GetBuilding(kind);
                if (mode.Type == InteractionModeType.PlacingBuilding && mode.BuildingKind == kind)
                {
                    var cell = TutorialPlaces.BuildingCell(_context.Session, kind);
                    if (cell == null || definition == null)
                        return Step(GuideTarget.Of(_hud.ContextBar.AutoButton), "Поставь сам",
                            "Игра найдёт свободное место.", 2, 2, _key + ":auto");
                    var place = GuideTarget.Place(cell.Value, definition.Width, definition.Height,
                        RewardArt.BuildingIcon(definition));
                    return Step(place, "Сюда", "Кликни по месту у склада или нажми «Поставить сам».", 2, 2,
                        _key + ":place");
                }
                var prepare = OpenCatalog(false, 1, 2, _key);
                if (prepare != null) return prepare;
                var token = _hud.Catalog.BuyButton(kind);
                if (token == null) return GuideStep.None;
                return Step(GuideTarget.Of(token), "Построй здание", $"Нажми на жетон «{BuildingName(kind)}».", 1, 2,
                    _key + ":token");
            }

            // --- work: a free creature, "Работа" (at the bottom or in the right click's fan), the building

            public GuideStep Work(QuestGoal goal)
            {
                var building = FirstBuilding(goal.Building, withRoom: true);
                if (building == null) return GuideStep.None;
                var mode = _interaction.Mode;
                if (mode.Type == InteractionModeType.ChoosingWorkTarget)
                    return Step(GuideTarget.Building(building.Id), "Куда на работу",
                        $"Кликни по зданию «{BuildingName(goal.Building)}».", 3, 3, _key + ":building");
                // a hire on its way (no one was free): the cell for it, never "cancel" and back to the token
                if (mode.Type == InteractionModeType.PlacingUnits) return Hire(mode.UnitKind, _key + ":hire");
                if (mode.Type != InteractionModeType.Neutral) return Cancel(1, 3, _key);
                if (SelectedFree(goal, unit => !WorksAt(unit, goal.Building)))
                    return Step(GuideTarget.Of(_hud.Fan.IsShown ? _hud.Fan.WorkButton : _hud.ContextBar.WorkButton), "Работа",
                        "Нажми «Работа» (E) внизу или кликни правой кнопкой мыши, потом по зданию.", 2, 3, _key + ":order",
                        keys: new[] { GuideKey.Of(Hotkeys.Work.Label), GuideKey.RightButton });
                var free = TutorialPlaces.NearestIdle(_snapshot, goal.CountsUnit, TutorialPlaces.CenterOf(building, _context.Catalog));
                if (free != null)
                    return Step(GuideTarget.Unit(free.Id), "Выбери работника", "Кликни по свободному существу на острове.", 1, 3,
                        _key + ":unit:" + free.Id);
                return Hire(HireKind(goal), _key + ":hire");
            }

            // --- haul: a free creature, "Перенос" (at the bottom or in the fan), where from, what, where to

            public GuideStep Haul(QuestGoal goal)
            {
                var source = FirstBuilding(goal.Building);
                if (source == null || FirstBuilding(goal.Destination) == null) return GuideStep.None;
                return HaulOrder(goal, source);
            }

            private GuideStep HaulOrder(QuestGoal goal, BuildingSnapshot source)
            {
                var mode = _interaction.Mode;
                switch (mode.Type)
                {
                    case InteractionModeType.ChoosingHaulDestination:
                    {
                        if (FindBuilding(mode.SourceId)?.Kind != goal.Building) return Cancel(3, 5, _key);
                        var target = FirstOf(_interaction.GetTargetBuildingIds(), goal.Destination);
                        if (target == null)
                            return Step(GuideTarget.Of(_hud.ContextBar.ChangeCargoButton), "Изменить груз",
                                "Этот груз туда не носят: выбери другой.", 4, 5, _key + ":recargo");
                        return Step(GuideTarget.Building(target.Id), $"2. Куда: {BuildingName(goal.Destination)}",
                            "Кликни по зданию: туда понесут груз.", 5, 5, _key + ":to");
                    }
                    case InteractionModeType.ChoosingHaulCargo:
                        if (FindBuilding(mode.SourceId)?.Kind != goal.Building) return Cancel(3, 5, _key);
                        return Step(GuideTarget.Of(_hud.HaulCargo.ConfirmButton), "Что носить",
                            "Можно оставить «Всё» и нажать «Куда носить».", 4, 5, _key + ":cargo", veil: false);
                    case InteractionModeType.ChoosingHaulSource:
                    {
                        var from = FirstOf(_interaction.GetTargetBuildingIds(), goal.Building) ?? source;
                        return Step(GuideTarget.Building(from.Id), $"1. Откуда: {BuildingName(goal.Building)}",
                            "Кликни по зданию, откуда носить.", 3, 5, _key + ":from");
                    }
                    case InteractionModeType.Neutral:
                        break;
                    case InteractionModeType.PlacingUnits:
                        // a hire on its way (no one was free): the cell for it, never "cancel" and back to the token
                        return Hire(mode.UnitKind, _key + ":hire");
                    default:
                        return Cancel(1, 5, _key);
                }
                if (SelectedFree(goal, unit => !Hauls(unit, goal)))
                    return Step(GuideTarget.Of(_hud.Fan.IsShown ? _hud.Fan.HaulButton : _hud.ContextBar.HaulButton), "Перенос",
                        "Нажми «Перенос» (H) внизу или кликни правой кнопкой мыши: дальше два здания, откуда и куда.", 2, 5,
                        _key + ":order", keys: new[] { GuideKey.Of(Hotkeys.Haul.Label), GuideKey.RightButton });
                var free = TutorialPlaces.NearestIdle(_snapshot, goal.CountsUnit, TutorialPlaces.CenterOf(source, _context.Catalog));
                if (free != null)
                    return Step(GuideTarget.Unit(free.Id), "Выбери носильщика", "Кликни по свободному существу на острове.", 1, 5,
                        _key + ":unit:" + free.Id);
                return Hire(HireKind(goal), _key + ":hire");
            }

            // --- battle: the arena tool, then its fight button; the deployment's steps are BattleGuide's

            public GuideStep Arena(QuestGoal goal)
            {
                int count = goal.Kind == QuestGoalKind.WearGearInBattle ? BattleGuide.GearSteps : BattleGuide.BattleSteps;
                if (_hud.Arena.IsOpen)
                    return Step(GuideTarget.Of(_hud.Arena.FightButton), "В бой",
                        UiFeel.IsAvailable(_hud.Arena.FightButton)
                            ? "Уровень выбран: нажми «В бой»."
                            : "Подожди: кнопка оживёт, когда арена будет готова.", 2, count, _key + ":fight", veil: false);
                if (_interaction.Mode.Type != InteractionModeType.Neutral) return Cancel(1, count, _key);
                return Step(GuideTarget.Of(_hud.TopBar.BattleButton), "Арена", "Нажми «Арена»: там уровни боёв.", 1, count,
                    _key + ":arena", keys: new[] { GuideKey.Of(Hotkeys.Arena.Label) });
            }

            // --- shared pieces

            // the catalog with the right tab, before its token can be pressed
            private GuideStep OpenCatalog(bool units, int number, int count, string key)
            {
                var catalog = _hud.Catalog;
                if (catalog.IsCovered)
                    return Step(GuideTarget.Of(_hud.TopBar.CatalogButton), "Вернись к каталогу",
                        "Кнопка снимет выбор и откроет каталог.", number, count, key + ":covered",
                        keys: new[] { GuideKey.Of(Hotkeys.Catalog.Label) });
                if (!catalog.IsOpen)
                    return Step(GuideTarget.Of(_hud.TopBar.CatalogButton), "Открой каталог",
                        "Внизу появятся здания и существа.", number, count, key + ":catalog",
                        keys: new[] { GuideKey.Of(Hotkeys.Catalog.Label) });
                if (catalog.ShowsUnits == units) return null;
                var tab = _hud.Root.Q<Button>(units ? "tab-units" : "tab-buildings");
                return units
                    ? Step(GuideTarget.Of(tab), "Вкладка «Существа»", "Здесь нанимают работников.", number, count, key + ":tab")
                    : Step(GuideTarget.Of(tab), "Вкладка «Здания»", "Здесь строят.", number, count, key + ":tab");
            }

            private GuideStep Cancel(int number, int count, string key) =>
                Step(GuideTarget.Of(_hud.ContextBar.CancelButton), "Сначала отмени", "Нажми «Отмена» (Esc).", number, count,
                    key + ":cancel", keys: new[] { GuideKey.Of(Hotkeys.Menu.Label) });

            private static GuideStep Step(GuideTarget target, string title, string text, int number, int count, string key,
                bool veil = true, GuideKey[] keys = null) =>
                target.Kind == GuideTargetKind.None
                    ? GuideStep.None
                    : new GuideStep(target, title, text, number, count, veil, key + ":" + number, keys);

            // a selected idle creature the goal counts that is not yet doing what it asks; one busy elsewhere (the
            // last order's hauler, still selected) is not taken off its route
            private bool SelectedFree(QuestGoal goal, System.Func<UnitSnapshot, bool> notYet)
            {
                var selected = _interaction.SelectedIds;
                if (selected.Count == 0) return false;
                foreach (var unit in _snapshot.Units)
                    if (selected.Contains(unit.Id) && goal.CountsUnit(unit.UnitKind) &&
                        unit.Assignment.Kind == AssignmentKind.Idle && notYet(unit)) return true;
                return false;
            }

            private bool WorksAt(UnitSnapshot unit, BuildingKind kind)
            {
                var assignment = unit.Assignment;
                return (assignment.Kind == AssignmentKind.Work || assignment.Kind == AssignmentKind.ToWork) &&
                       FindBuilding(assignment.BuildingId)?.Kind == kind;
            }

            private bool Hauls(UnitSnapshot unit, QuestGoal goal)
            {
                var assignment = unit.Assignment;
                return assignment.Kind == AssignmentKind.Haul && FindBuilding(assignment.SourceId)?.Kind == goal.Building &&
                       FindBuilding(assignment.DestinationId)?.Kind == goal.Destination;
            }

            private static UnitKind HireKind(QuestGoal goal) => goal.AnyUnit ? UnitKind.Goblin : goal.Unit;

            private BuildingSnapshot FirstBuilding(BuildingKind kind, bool withRoom = false)
            {
                BuildingSnapshot any = null;
                foreach (var building in _snapshot.Buildings)
                {
                    if (building.Kind != kind) continue;
                    if (!withRoom || building.WorkerCount < building.MaxWorkers) return building;
                    any ??= building;
                }
                return any;
            }

            private BuildingSnapshot FirstOf(IReadOnlyList<string> ids, BuildingKind kind)
            {
                foreach (var id in ids)
                {
                    var building = FindBuilding(id);
                    if (building != null && building.Kind == kind) return building;
                }
                return null;
            }

            private BuildingSnapshot FindBuilding(string id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                foreach (var building in _snapshot.Buildings)
                    if (building.Id == id) return building;
                return null;
            }

            private string UnitName(UnitKind kind) => _context.Catalog.GetUnit(kind)?.DisplayName ?? kind.ToString();

            // the picture on the pin over a creature's cell: the creature as its token shows it
            private UnityEngine.Sprite UnitArt(UnitKind kind) =>
                RewardArt.Tight(_context.Catalog.GetUnit(kind)?.PortraitSprite);

            private string BuildingName(BuildingKind kind) => _context.Catalog.GetBuilding(kind)?.DisplayName ?? kind.ToString();
        }
    }
}
