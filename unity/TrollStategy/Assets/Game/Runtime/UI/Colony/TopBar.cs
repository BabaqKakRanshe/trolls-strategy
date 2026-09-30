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
    /// Settlement counters and the actions that are always at hand: pick the next idle creature, buy and clear
    /// land, show the grid and routes, go to battle, open the catalog. The battle button counts down to the mission.
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
        private readonly Button _idleButton;
        private readonly Button _gridButton;
        private readonly Button _landButton;
        private readonly Button _battleButton;
        private readonly Button _catalogButton;
        private bool? _battleReady;
        private float _nextBattleCheck;
        private ProgressSnapshot _progress = ProgressSnapshot.Sandbox;
        private QuestFocus _focus = QuestFocus.None;
        private bool _catalogOpen;

        public TopBar(VisualElement root, ColonyHudContext context, Action toggleCatalog)
        {
            _context = context;
            _gold = new CounterLabel(Ui.Require<Label>(root, "gold-value"), punch: true);
            _ore = new CounterLabel(Ui.Require<Label>(root, "ore-value"), punch: false);
            _population = new CounterLabel(Ui.Require<Label>(root, "population-value"), punch: true);
            _sold = new CounterLabel(Ui.Require<Label>(root, "sold-value"), punch: false);
            _idle = Ui.Require<Label>(root, "population-idle");

            _idleButton = UiFeel.Bind(Ui.Require<Button>(root, "idle-button"), context.Interaction.SelectNextIdle,
                silentClick: true);
            _landButton = UiFeel.Bind(Ui.Require<Button>(root, "land-button"), context.Interaction.ToggleLandMode);
            Ui.Show(_landButton, context.Session.CurrentSnapshot.Land != null);
            _gridButton = UiFeel.Bind(Ui.Require<Button>(root, "grid-button"), ToggleGuides);
            Ui.Show(_gridButton, context.ToggleGuides != null);
            _mission = context.FirstMission;
            _battleButton = UiFeel.Bind(Ui.Require<Button>(root, "battle-button"), OpenBattle);
            Ui.Show(_battleButton, _mission != null && context.OpenBattle != null);
            _catalogButton = UiFeel.Bind(Ui.Require<Button>(root, "catalog-button"), toggleCatalog);
            RefreshBattle();
        }

        public Button BattleButton => _battleButton;
        public Button IdleButton => _idleButton;
        public Button CatalogButton => _catalogButton;
        public Button LandButton => _landButton;
        public string GoldText => _gold.Value.ToString();

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
            Ui.SetText(_idle, snapshot.Units.Count == 0 ? "никого нет" : idle > 0 ? $"свободно: {idle}" : "все при деле");
            UiFeel.SetAvailable(_idleButton, idle > 0);
            _gridButton.EnableInClassList("is-on", _context.GuidesVisible?.Invoke() ?? false);
            Ui.Show(_landButton, snapshot.Land != null);
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
                UiMotion.Flash(_battleButton, Color.white, .8f);
                GameAudio.Play(Sfx.Equip, .7f);
            }
            _battleReady = ready;
            UiFeel.SetAvailable(_battleButton, ready);
            if (!_progress.IsMissionUnlocked(_mission.MissionId))
            {
                // the quest chain opens the battle; say at which level
                int level = _progress.MissionUnlockLevel(_mission.MissionId);
                Ui.SetText(_battleButton, level > 0 ? $"БОЙ ПОСЛЕ УР. {level}" : "БОЙ ЗАКРЫТ");
                return;
            }
            int wait = session.MissionWaitMs(_mission.MissionId);
            Ui.SetText(_battleButton, ready ? "В БОЙ" : wait > 0 ? "БОЙ ЧЕРЕЗ " + Duration(wait) : "БОЙ ЗАКРЫТ");
        }

        /// <summary>Seconds under a minute, minutes and seconds above.</summary>
        public static string Duration(int milliseconds)
        {
            int seconds = Mathf.CeilToInt(milliseconds / 1000f);
            return seconds < 60 ? seconds + " С" : $"{seconds / 60}:{seconds % 60:00}";
        }

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
