using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Settlement counters and the tools that are always at hand: pick the next idle creature, buy and clear
    /// land, show the grid and routes, go to battle, open the catalog. Each counter is a picture and a number,
    /// each tool a round button with its caption; names, details and keys are in the hints. The battle caption
    /// counts down to the mission.
    /// </summary>
    public sealed class TopBar
    {
        private const float BattleCheckSeconds = .5f;

        private readonly ColonyHudContext _context;
        private readonly BattleMissionDefinition _mission;
        private readonly CounterLabel _gold;
        private readonly CounterLabel _ore;
        private readonly CounterLabel _population;
        private readonly CounterLabel _sold;
        private readonly Label _idle;
        private readonly Label _battleCaption;
        private readonly Button _idleButton;
        private readonly Button _gridButton;
        private readonly Button _landButton;
        private readonly Button _battleButton;
        private readonly Button _catalogButton;
        private bool? _battleReady;
        private float _nextBattleCheck;
        private int _units;
        private int _idleUnits;
        private ProgressSnapshot _progress = ProgressSnapshot.Sandbox;
        private QuestFocus _focus = QuestFocus.None;
        private bool _catalogOpen;

        public TopBar(VisualElement root, ColonyHudContext context, Action toggleCatalog, HudTooltip tooltip = null)
        {
            _context = context;
            _gold = new CounterLabel(Ui.Require<Label>(root, "gold-value"), punch: true);
            _ore = new CounterLabel(Ui.Require<Label>(root, "ore-value"), punch: false);
            _population = new CounterLabel(Ui.Require<Label>(root, "population-value"), punch: true);
            _sold = new CounterLabel(Ui.Require<Label>(root, "sold-value"), punch: false);
            _idle = Ui.Require<Label>(root, "population-idle");

            // the pictures come from the content: the market's coins, the ore, the barracks and the market
            var catalog = context.Catalog;
            GoldIcon = Ui.Require<VisualElement>(root, "gold-icon");
            Ui.SetPicture(GoldIcon, RewardArt.Coin(catalog));
            Ui.SetPicture(Ui.Require<VisualElement>(root, "ore-icon"), catalog?.TryGetResource(ResourceKind.IronOre)?.Icon);
            Ui.SetPicture(Ui.Require<VisualElement>(root, "population-icon"), RewardArt.BuildingIcon(catalog, BuildingKind.Barracks));
            Ui.SetPicture(Ui.Require<VisualElement>(root, "sold-icon"), RewardArt.BuildingIcon(catalog, BuildingKind.Market));

            _idleButton = UiFeel.Bind(Ui.Require<Button>(root, "idle-button"), context.Interaction.SelectNextIdle,
                silentClick: true);
            _landButton = UiFeel.Bind(Ui.Require<Button>(root, "land-button"), context.Interaction.ToggleLandMode);
            ShowTool(_landButton, context.Session.CurrentSnapshot.Land != null);
            _gridButton = UiFeel.Bind(Ui.Require<Button>(root, "grid-button"), ToggleGuides);
            ShowTool(_gridButton, context.ToggleGuides != null);
            _mission = context.FirstMission;
            _battleButton = UiFeel.Bind(Ui.Require<Button>(root, "battle-button"), OpenBattle);
            _battleCaption = Ui.Require<Label>(root, "battle-caption");
            ShowTool(_battleButton, _mission != null && context.OpenBattle != null);
            _catalogButton = UiFeel.Bind(Ui.Require<Button>(root, "catalog-button"), toggleCatalog);

            if (tooltip != null)
            {
                tooltip.Attach(Ui.Require<VisualElement>(root, "gold"), () => "Золото",
                    () => "Казна поселения. Рынок платит за каждый доставленный товар.");
                tooltip.Attach(Ui.Require<VisualElement>(root, "ore"), () => "Руда",
                    () => "Вся руда в зданиях поселения.");
                tooltip.Attach(Ui.Require<VisualElement>(root, "population"), () => "Существа", PopulationHint);
                tooltip.Attach(Ui.Require<VisualElement>(root, "sold"), () => "Продано",
                    () => "Сколько товаров купил рынок.");
                tooltip.Attach(_idleButton, () => "Свободный", () => "Показать следующее существо без работы.", "1");
                tooltip.Attach(_landButton, () => "Земля", () => "Купить участок у острова или расчистить свой.", "L");
                tooltip.Attach(_gridButton, () => "Сетка", () => "Клетки поля и пути носильщиков.", "G");
                tooltip.Attach(_battleButton, () => "Бой", () => _battleCaption.text);
                tooltip.Attach(_catalogButton, () => "Каталог", () => "Здания и существа, лоток внизу экрана.");
            }
            RefreshBattle();
        }

        public Button BattleButton => _battleButton;
        public Button IdleButton => _idleButton;
        public Button CatalogButton => _catalogButton;
        public Button LandButton => _landButton;
        /// <summary>What the battle tool says: when the mission opens, or that it is open.</summary>
        public string BattleText => _battleCaption.text;
        public string GoldText => _gold.Value.ToString();
        /// <summary>The treasury's coin: where gold won elsewhere on the screen flies to.</summary>
        public VisualElement GoldIcon { get; }

        /// <summary>
        /// The next change of gold is on its way: the counter waits <paramref name="delay"/> seconds, then counts
        /// over <paramref name="duration"/>, as the coins land. Zero for both drops the wait.
        /// </summary>
        public void ExpectGold(float delay, float duration) => _gold.DelayNext(delay, duration);

        /// <summary>What the current quest asks for: the battle, or the catalog while it is closed.</summary>
        public void SetFocus(QuestFocus focus)
        {
            _focus = focus ?? QuestFocus.None;
            _battleButton.EnableInClassList("is-suggested", _focus.Battle);
            _catalogButton.EnableInClassList("is-suggested", _focus.UsesCatalog && !_catalogOpen);
        }

        public void Refresh(GameSnapshot snapshot)
        {
            bool missionWasOpen = _mission == null || _progress.IsMissionUnlocked(_mission.MissionId);
            _progress = snapshot.Progress;
            // a battle opened or closed by the quest chain says so at once, not at the next timer check
            if (_mission != null && missionWasOpen != _progress.IsMissionUnlocked(_mission.MissionId)) RefreshBattle();
            _gold.Set(snapshot.Gold);
            _ore.Set(snapshot.TotalOre);
            _population.Set(snapshot.Units.Count);
            _sold.Set(snapshot.SoldGoods);

            int idle = 0;
            foreach (var unit in snapshot.Units)
                if (unit.Assignment.Kind == AssignmentKind.Idle) idle++;
            _units = snapshot.Units.Count;
            _idleUnits = idle;
            Ui.SetText(_idle, idle > 0 ? $"{idle} без дела" : string.Empty);
            UiFeel.SetAvailable(_idleButton, idle > 0);
            _gridButton.EnableInClassList("is-on", _context.GuidesVisible?.Invoke() ?? false);
            ShowTool(_landButton, snapshot.Land != null);
            _landButton.EnableInClassList("is-on",
                _context.Interaction.Mode.Type == InteractionModeType.ManagingLand);
        }

        public void SetCatalogOpen(bool open)
        {
            _catalogOpen = open;
            _catalogButton.EnableInClassList("is-on", open);
            _catalogButton.EnableInClassList("is-suggested", _focus.UsesCatalog && !open);
        }

        public void Tick()
        {
            if (Time.unscaledTime < _nextBattleCheck) return;
            _nextBattleCheck = Time.unscaledTime + BattleCheckSeconds;
            RefreshBattle();
        }

        public void RefreshBattle()
        {
            if (_mission == null) return;
            var session = _context.Session;
            bool ready = session.CanEnterMission(_mission.MissionId).Ok;
            if (ready && _battleReady == false)
            {
                // the moment the mission opens, the button says so instead of waiting to be noticed
                UiMotion.Punch(_battleButton, .22f, .5f);
                UiMotion.Punch(_battleCaption, .18f, .5f);
                GameAudio.Play(Sfx.Equip, .7f);
            }
            _battleReady = ready;
            UiFeel.SetAvailable(_battleButton, ready);
            if (!_progress.IsMissionUnlocked(_mission.MissionId))
            {
                // the quest chain opens the battle; say at which level
                int level = _progress.MissionUnlockLevel(_mission.MissionId);
                Ui.SetText(_battleCaption, level > 0 ? $"Бой с {level} уровня" : "Бой закрыт");
                return;
            }
            int wait = session.MissionWaitMs(_mission.MissionId);
            Ui.SetText(_battleCaption, ready ? "В бой" : wait > 0 ? "Бой через " + Duration(wait) : "Бой закрыт");
        }

        /// <summary>Seconds under a minute, minutes and seconds above.</summary>
        public static string Duration(int milliseconds)
        {
            int seconds = Mathf.CeilToInt(milliseconds / 1000f);
            return seconds < 60 ? seconds + " с" : $"{seconds / 60}:{seconds % 60:00}";
        }

        private string PopulationHint()
        {
            if (_units == 0) return "Пока никого. Нанять можно в каталоге.";
            return _idleUnits > 0 ? $"Всего {_units}, без дела {_idleUnits}." : $"Всего {_units}, все при деле.";
        }

        // A tool comes and goes with its caption.
        private static void ShowTool(Button button, bool visible) => Ui.Show(button.parent, visible);

        private void ToggleGuides()
        {
            _context.ToggleGuides?.Invoke();
            _gridButton.EnableInClassList("is-on", _context.GuidesVisible?.Invoke() ?? false);
        }

        private void OpenBattle()
        {
            if (_mission != null) _context.OpenBattle?.Invoke(_mission);
        }
    }
}
