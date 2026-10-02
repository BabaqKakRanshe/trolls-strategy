using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The arena as a poster, opened by the battle tool. On the left a rail of level discs: every level the
    /// colony has opened and the next closed one, a tick on a won level, a lock on the closed one and the rest
    /// time under a resting one. The chosen level fills the poster: the squad's places against the enemy, the
    /// gold a win brings, the folk a first win opens for hire and the button into its battle. Which level is
    /// chosen is the panel's own; outcomes stay with the session. Reads the session, never the state.
    /// </summary>
    public sealed class ArenaPanel
    {
        private const float RefreshSeconds = .5f;
        private const int EnemyKinds = 4;
        private const int PathDots = 3;

        private sealed class Level
        {
            public VisualElement Root;
            public VisualElement Path;
            public Button Disc;
            public VisualElement Badge;
            public VisualElement BadgeGlyph;
            public VisualElement Rest;
            public Label RestTime;
            public BattleMissionDefinition Mission;
        }

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _overlay;
        private readonly ScrollView _rail;
        private readonly Label _eyebrow;
        private readonly Label _name;
        private readonly VisualElement _squad;
        private readonly VisualElement _enemies;
        private readonly Label _gold;
        private readonly Label _goldNote;
        private readonly VisualElement _hire;
        private readonly VisualElement _hireArt;
        private readonly Label _hireName;
        private readonly VisualElement _statePicture;
        private readonly VisualElement _stateGlyph;
        private readonly Label _state;
        private readonly Button _fight;
        private readonly List<Level> _levels = new();
        private BattleMissionDefinition _chosen;
        private BattleMissionDefinition _posterOf;
        private int _squadShown = -1;
        private float _sinceRefresh;
        private string _shownSignature;

        public ArenaPanel(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            _overlay = Ui.Require<VisualElement>(root, "arena-overlay");
            _rail = Ui.Require<ScrollView>(root, "arena-levels");
            _rail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _rail.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _eyebrow = Ui.Require<Label>(root, "arena-level");
            _name = Ui.Require<Label>(root, "arena-name");
            _squad = Ui.Require<VisualElement>(root, "arena-squad");
            _enemies = Ui.Require<VisualElement>(root, "arena-enemies");
            _gold = Ui.Require<Label>(root, "arena-gold");
            _goldNote = Ui.Require<Label>(root, "arena-gold-note");
            _hire = Ui.Require<VisualElement>(root, "arena-hire");
            _hireArt = Ui.Require<VisualElement>(root, "arena-hire-art");
            _hireName = Ui.Require<Label>(root, "arena-hire-name");
            _statePicture = Ui.Require<VisualElement>(root, "arena-state-picture");
            _stateGlyph = Ui.Require<VisualElement>(root, "arena-state-glyph");
            _state = Ui.Require<Label>(root, "arena-state");
            Ui.SetPicture(Ui.Require<VisualElement>(root, "arena-coin"), RewardArt.Coin(context.Catalog));
            _fight = UiFeel.Bind(Ui.Require<Button>(root, "arena-fight"), Fight);
            UiFeel.Bind(Ui.Require<Button>(root, "arena-close"), Close, Sfx.UiBack);
            // a click on the veil around the sheet closes it, like Esc
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == _overlay) Close();
            });
            _tooltip?.Attach(_squad, () => "Ваш отряд", () => _chosen == null
                ? null
                : $"В бой идут до {_context.Session.SquadLimit(_chosen)} бойцов. Кого взять, решите перед боем.");
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);

        /// <summary>The level discs on the rail, lowest level first.</summary>
        public IReadOnlyList<Button> LevelButtons
        {
            get
            {
                var buttons = new List<Button>();
                foreach (var level in _levels)
                    if (Ui.IsShown(level.Root)) buttons.Add(level.Disc);
                return buttons;
            }
        }

        /// <summary>The level the poster shows.</summary>
        public BattleMissionDefinition Chosen => _chosen;

        public Button FightButton => _fight;

        /// <summary>The places the poster shows for the squad.</summary>
        public int SquadPlaces => _squad.childCount;

        /// <summary>The kinds of enemy the poster shows.</summary>
        public int EnemyKindsShown => _enemies.childCount;

        public string Gold => _gold.text;

        /// <summary>The folk a first win opens for hire, as the poster names it; null when it names none.</summary>
        public string Hire => Ui.IsShown(_hire) ? _hireName.text : null;

        public void Open()
        {
            if (_context.OpenBattle == null) return;
            Ui.Show(_overlay, true);
            UiMotion.PopIn(_overlay, .2f);
            _chosen = _context.Session.SuggestedMission();
            Refresh();
            ScrollToChosen();
        }

        public void Close() => Ui.Show(_overlay, false);

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen) return;
            _sinceRefresh += unscaledDeltaTime;
            if (_sinceRefresh < RefreshSeconds) return;
            _sinceRefresh = 0f;
            Refresh();
        }

        public void Refresh()
        {
            var session = _context.Session;
            // every open level and the first closed one after them
            var shown = new List<BattleMissionDefinition>();
            foreach (var mission in session.ArenaLadder())
            {
                shown.Add(mission);
                if (!session.IsMissionUnlocked(mission.MissionId)) break;
            }
            string signature = string.Join(",", shown.ConvertAll(m => m.MissionId));
            if (signature != _shownSignature) Rebuild(shown, signature);
            if (_chosen == null || !shown.Contains(_chosen)) _chosen = session.SuggestedMission();

            foreach (var level in _levels)
                if (Ui.IsShown(level.Root)) RenderLevel(level);
            if (_chosen != null) RenderPoster(_chosen);
        }

        private void Choose(BattleMissionDefinition mission)
        {
            if (mission == null) return;
            _chosen = mission;
            Refresh();
        }

        private void Rebuild(List<BattleMissionDefinition> shown, string signature)
        {
            _shownSignature = signature;
            while (_levels.Count < shown.Count) _levels.Add(CreateLevel());
            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                bool visible = i < shown.Count;
                Ui.Show(level.Root, visible);
                if (!visible) continue;
                level.Mission = shown[i];
                Ui.Show(level.Path, i > 0);
                Ui.SetCaption(level.Disc, level.Mission.Level.ToString());
            }
        }

        private void RenderLevel(Level level)
        {
            var session = _context.Session;
            string id = level.Mission.MissionId;
            bool open = session.IsMissionUnlocked(id);
            bool won = open && session.MissionWins(id) > 0;
            int wait = open ? session.MissionWaitMs(id) : 0;

            level.Disc.EnableInClassList("is-on", level.Mission == _chosen);
            level.Root.EnableInClassList("is-closed", !open);
            // a tick on a won level, a lock on the closed one
            Ui.Show(level.Badge, won || !open);
            level.Badge.EnableInClassList("arena-rail__badge--won", won);
            level.Badge.EnableInClassList("arena-rail__badge--closed", !open);
            level.BadgeGlyph.EnableInClassList("glyph--check", won);
            level.BadgeGlyph.EnableInClassList("glyph--lock", !open);
            Ui.Show(level.Rest, wait > 0);
            if (wait > 0) Ui.SetText(level.RestTime, TopBar.Duration(wait));
        }

        private void RenderPoster(BattleMissionDefinition mission)
        {
            var session = _context.Session;
            string id = mission.MissionId;
            bool open = session.IsMissionUnlocked(id);
            var enter = session.CanEnterMission(id);
            int wins = session.MissionWins(id);
            int wait = session.MissionWaitMs(id);
            var (min, max) = session.WinGold(mission);

            if (_posterOf != mission)
            {
                _posterOf = mission;
                Ui.SetText(_name, StripLevel(mission.DisplayName));
                FillEnemies(mission);
            }
            Ui.SetText(_eyebrow, wins > 0 ? $"Уровень {mission.Level}. Побед: {wins}" : $"Уровень {mission.Level}");
            int limit = session.SquadLimit(mission);
            if (limit != _squadShown) FillSquad(limit);

            Ui.SetText(_gold, max > min ? $"{min}–{max}" : min.ToString());
            Ui.SetText(_goldNote, wins == 0 ? "золота за первую победу" : "золота за победу");

            // a first win over a folk opens it for hire
            var unit = mission.UnlockUnit.HasValue && wins == 0 ? _context.Catalog.TryGetUnit(mission.UnlockUnit.Value) : null;
            Ui.Show(_hire, unit != null);
            if (unit != null)
            {
                Ui.SetText(_hireName, unit.DisplayName);
                Ui.SetPicture(_hireArt, RewardArt.Tight(unit.PortraitSprite));
            }

            // what stands between the colony and this battle, or a word on growing stronger
            string glyph = null;
            bool advice = false;
            string state;
            if (!open)
            {
                state = enter.Error;
                glyph = "glyph--lock";
            }
            else if (enter.Ok)
            {
                state = "Улучшения бараков делают отряд сильнее";
                advice = true;
            }
            else if (wait > 0)
            {
                state = $"Отдых {TopBar.Duration(wait)}";
                glyph = "glyph--rest";
            }
            else state = enter.Error;
            Ui.SetText(_state, state);
            if (advice) Ui.SetPicture(_statePicture, RewardArt.BuildingIcon(_context.Catalog, BuildingKind.Barracks));
            else Ui.Show(_statePicture, false);
            Ui.Show(_stateGlyph, glyph != null);
            _stateGlyph.EnableInClassList("glyph--lock", glyph == "glyph--lock");
            _stateGlyph.EnableInClassList("glyph--rest", glyph == "glyph--rest");
            UiFeel.SetAvailable(_fight, enter.Ok);
        }

        // The places the squad may fill: a ring for each fighter the colony may send to this level.
        private void FillSquad(int limit)
        {
            _squadShown = limit;
            _squad.Clear();
            for (int i = 0; i < limit; i++)
            {
                var place = Ui.Box("arena-slot");
                place.pickingMode = PickingMode.Ignore;
                _squad.Add(place);
            }
        }

        // Up to four kinds of the level's enemies, each on a disc with its count.
        private void FillEnemies(BattleMissionDefinition mission)
        {
            _enemies.Clear();
            var counts = new Dictionary<UnitKind, int>();
            var order = new List<UnitKind>();
            foreach (var enemy in mission.Enemies)
            {
                if (!counts.ContainsKey(enemy.Kind))
                {
                    counts[enemy.Kind] = 0;
                    order.Add(enemy.Kind);
                }
                counts[enemy.Kind]++;
            }
            for (int i = 0; i < order.Count && i < EnemyKinds; i++)
            {
                var kind = order[i];
                var definition = _context.Catalog.TryGetUnit(kind);
                var foe = Ui.Box("arena-foe");
                var disc = Ui.Box("arena-foe__disc");
                var art = Ui.Box("arena-foe__art");
                disc.pickingMode = PickingMode.Ignore;
                art.pickingMode = PickingMode.Ignore;
                Ui.SetPicture(art, RewardArt.Tight(definition != null ? definition.PortraitSprite : null));
                disc.Add(art);
                foe.Add(disc);
                var count = Ui.Text("×" + counts[kind], "arena-foe__count t-black");
                count.pickingMode = PickingMode.Ignore;
                foe.Add(count);
                _enemies.Add(foe);
                if (_tooltip != null && definition != null)
                {
                    string title = definition.DisplayName, body = definition.Description;
                    _tooltip.Attach(foe, () => title, () => body);
                }
            }
        }

        private Level CreateLevel()
        {
            var level = new Level { Root = Ui.Box("arena-rail__level") };
            level.Root.pickingMode = PickingMode.Ignore;
            // the dots of the path from the level below
            level.Path = Ui.Box("arena-rail__path");
            level.Path.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < PathDots; i++)
            {
                var dot = Ui.Box("arena-rail__dot");
                dot.pickingMode = PickingMode.Ignore;
                level.Path.Add(dot);
            }
            level.Disc = Ui.CaptionButton(string.Empty, null, "btn btn-disc arena-rail__disc");
            level.Disc.Q<Label>(className: "btn__caption")?.AddToClassList("t-black");
            level.Badge = Ui.Box("arena-rail__badge");
            level.Badge.pickingMode = PickingMode.Ignore;
            level.BadgeGlyph = Ui.Box("glyph");
            level.BadgeGlyph.pickingMode = PickingMode.Ignore;
            level.Badge.Add(level.BadgeGlyph);
            level.Disc.Add(level.Badge);
            level.Rest = Ui.Box("arena-rail__rest");
            level.Rest.pickingMode = PickingMode.Ignore;
            var clock = Ui.Box("glyph glyph--rest");
            clock.pickingMode = PickingMode.Ignore;
            level.RestTime = Ui.Text(string.Empty, "arena-rail__rest-time t-bold");
            level.RestTime.pickingMode = PickingMode.Ignore;
            level.Rest.Add(clock);
            level.Rest.Add(level.RestTime);
            level.Root.Add(level.Path);
            level.Root.Add(level.Disc);
            level.Root.Add(level.Rest);
            UiFeel.Bind(level.Disc, () => Choose(level.Mission));
            _tooltip?.Attach(level.Disc, () => level.Mission != null ? StripLevel(level.Mission.DisplayName) : null,
                () => level.Mission != null ? StateOf(level.Mission) : null);
            _rail.Add(level.Root);
            return level;
        }

        // The level's state in words for its disc's hint.
        private string StateOf(BattleMissionDefinition mission)
        {
            var session = _context.Session;
            var enter = session.CanEnterMission(mission.MissionId);
            int wins = session.MissionWins(mission.MissionId);
            int wait = session.MissionWaitMs(mission.MissionId);
            if (!session.IsMissionUnlocked(mission.MissionId)) return enter.Error;
            if (enter.Ok) return wins > 0 ? $"Побед: {wins}. Можно в бой" : "Можно в бой";
            return wait > 0 ? $"Отдых {TopBar.Duration(wait)}" : enter.Error;
        }

        private void Fight()
        {
            var mission = _chosen;
            if (mission == null || !_context.Session.CanEnterMission(mission.MissionId).Ok) return;
            Close();
            _context.OpenBattle?.Invoke(mission);
        }

        private void ScrollToChosen()
        {
            foreach (var level in _levels)
                if (Ui.IsShown(level.Root) && level.Mission == _chosen)
                {
                    var target = level.Root;
                    _rail.schedule.Execute(() => _rail.ScrollTo(target));
                    return;
                }
        }

        // "12. Варги" reads "Варги" under its level's number.
        private static string StripLevel(string name)
        {
            int dot = name.IndexOf(". ", StringComparison.Ordinal);
            return dot > 0 && dot < 4 ? name.Substring(dot + 2) : name;
        }
    }
}
