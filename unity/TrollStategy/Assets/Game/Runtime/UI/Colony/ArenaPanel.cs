using System;
using System.Collections.Generic;
using System.Globalization;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The arena as a poster, opened by the battle tool. On the left a rail of level discs, the whole ladder: the
    /// levels the colony has opened, the next one with a lock and the road beyond it faded; a tick on a won level,
    /// a star on a milestone, the folk a first win opens for hire and the rest time under a resting one. Any level
    /// can be looked at, only an open one fights. The chosen level fills the poster from the session's
    /// <see cref="ArenaOfferSnapshot"/>: the squad's places and how the colony's best squad compares, the enemies
    /// with their strength and gear, what a win brings (gold, trophies, the folk a first win opens for hire), the
    /// arena's prize fund, the stake and what a defeat costs, and the button into its battle. Which level is
    /// chosen is the panel's own; outcomes stay with the session. Reads the session, never the state.
    /// </summary>
    public sealed class ArenaPanel
    {
        private const float RefreshSeconds = .5f;
        private const int EnemyKinds = 4;
        private const int PathDots = 3;
        private const int GearShown = 3;
        private const int TrophiesShown = 3;

        private sealed class Level
        {
            public VisualElement Root;
            public VisualElement Path;
            public Button Disc;
            public VisualElement Badge;
            public VisualElement BadgeGlyph;
            public VisualElement Star;
            public VisualElement Folk;
            public bool HasFolk;
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
        private readonly Label _biome;
        private readonly Label _odds;
        private readonly Label _statHealth;
        private readonly Label _statDamage;
        private readonly Label _statArmor;
        private readonly VisualElement _enemyGear;
        private readonly VisualElement _trophies;
        private readonly VisualElement _trophyArt;
        private readonly Label _trophyCount;
        private readonly VisualElement _fundDots;
        private readonly Label _fundNext;
        private readonly Label _stakeValue;
        private readonly List<Level> _levels = new();
        private ArenaOfferSnapshot _offer;
        private string _trophiesShown;
        private BattleMissionDefinition _chosen;
        private BattleMissionDefinition _nextClosed;
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
            _biome = Ui.Require<Label>(root, "arena-biome");
            _odds = Ui.Require<Label>(root, "arena-odds");
            _statHealth = Ui.Require<Label>(root, "arena-stat-health");
            _statDamage = Ui.Require<Label>(root, "arena-stat-damage");
            _statArmor = Ui.Require<Label>(root, "arena-stat-armor");
            _enemyGear = Ui.Require<VisualElement>(root, "arena-enemy-gear");
            _trophies = Ui.Require<VisualElement>(root, "arena-trophies");
            _trophyArt = Ui.Require<VisualElement>(root, "arena-trophy-art");
            _trophyCount = Ui.Require<Label>(root, "arena-trophy-count");
            _fundDots = Ui.Require<VisualElement>(root, "arena-fund-dots");
            _fundNext = Ui.Require<Label>(root, "arena-fund-next");
            _stakeValue = Ui.Require<Label>(root, "arena-stake-value");
            Ui.SetPicture(Ui.Require<VisualElement>(root, "arena-stake-coin"), RewardArt.Coin(context.Catalog));
            UiFeel.Bind(Ui.Require<Button>(root, "arena-close"), Close, Sfx.UiBack);
            // a click on the veil around the sheet closes it, like Esc
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == _overlay) Close();
            });
            _tooltip?.Attach(_squad, () => "Ваш отряд", () => _chosen == null
                ? null
                : $"В бой идут до {_context.Session.SquadLimit(_chosen)} бойцов. Кого взять, решите перед боем.");
            _tooltip?.Attach(_odds, () => "Оценка отряда", OddsHint);
            _tooltip?.Attach(Ui.Require<VisualElement>(root, "arena-strength"), () => "Сила врагов", () => _offer == null
                ? null
                : $"Враги сильнее обычного: здоровье ×{Multiplier(_offer.EnemyHealthPercent)}, урон +{_offer.EnemyDamageBonus}, броня +{_offer.EnemyArmorBonus}");
            _tooltip?.Attach(_trophies, () => "Трофеи", TrophiesHint);
            _tooltip?.Attach(Ui.Require<VisualElement>(root, "arena-fund"), () => "Призовой фонд", () => _offer == null
                ? null
                : $"Повторные победы на любом уровне платят из призового фонда арены. Одна выплата приходит каждые {TopBar.Duration(_offer.Fund.PeriodMs)}.");
            _tooltip?.Attach(Ui.Require<VisualElement>(root, "arena-stake"), () => "Ставка", () =>
                "Победа возвращает ставку, поражение и ничья её сжигают.");
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

        /// <summary>The offer the poster shows.</summary>
        public ArenaOfferSnapshot Offer => _offer;

        /// <summary>The odds pill's words, as the poster shows them.</summary>
        public string Odds => _odds.text;

        /// <summary>The enemies' health, damage and armour, as the poster shows them.</summary>
        public string EnemyStrength => $"{_statHealth.text} {_statDamage.text} {_statArmor.text}";

        /// <summary>The items the enemies wear, as tokens on the poster.</summary>
        public int EnemyGearShown => _enemyGear.childCount;

        /// <summary>Trophy pictures on the poster; 0 while it shows none.</summary>
        public int TrophiesShownCount => Ui.IsShown(_trophies) ? _trophyArt.childCount : 0;

        /// <summary>The prize fund's dots and how many of them are full.</summary>
        public (int Full, int All) FundDots
        {
            get
            {
                int full = 0;
                foreach (var dot in _fundDots.Children())
                    if (dot.ClassListContains("arena-fund__dot--full")) full++;
                return (full, _fundDots.childCount);
            }
        }

        public string Stake => _stakeValue.text;

        /// <summary>The line by the battle button: what stands in the way, or what a defeat costs.</summary>
        public string State => _state.text;

        public string Eyebrow => _eyebrow.text;

        public string Biome => _biome.text;

        /// <summary>Whether the level's disc on the rail carries the milestone star.</summary>
        public bool HasStar(BattleMissionDefinition mission)
        {
            foreach (var level in _levels)
                if (Ui.IsShown(level.Root) && level.Mission == mission) return Ui.IsShown(level.Star);
            return false;
        }

        /// <summary>Whether the level's disc carries the lock: the next level the colony can open.</summary>
        public bool HasLock(BattleMissionDefinition mission) =>
            LevelOf(mission) is Level level && Ui.IsShown(level.Badge) && level.Badge.ClassListContains("arena-rail__badge--closed");

        /// <summary>Whether the level's disc is faded: a level beyond the next one.</summary>
        public bool IsFaded(BattleMissionDefinition mission) => LevelOf(mission)?.Root.ClassListContains("is-far") == true;

        /// <summary>Whether the level's disc shows the folk its first win opens for hire.</summary>
        public bool ShowsFolk(BattleMissionDefinition mission) => LevelOf(mission) is Level level && Ui.IsShown(level.Folk);

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
            // the whole ladder: the open levels, the next one with its lock and the road beyond it
            var shown = new List<BattleMissionDefinition>(session.ArenaLadder());
            _nextClosed = shown.Find(mission => !session.IsMissionUnlocked(mission.MissionId));
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
                var folk = level.Mission.UnlockUnit.HasValue ? _context.Catalog.TryGetUnit(level.Mission.UnlockUnit.Value) : null;
                Ui.SetPicture(level.Folk, RewardArt.Tight(folk != null ? folk.PortraitSprite : null));
                level.HasFolk = folk != null && folk.PortraitSprite != null;
            }
        }

        private void RenderLevel(Level level)
        {
            var session = _context.Session;
            string id = level.Mission.MissionId;
            bool open = session.IsMissionUnlocked(id);
            bool won = open && session.MissionWins(id) > 0;
            int wait = open ? session.MissionWaitMs(id) : 0;

            bool next = level.Mission == _nextClosed;
            level.Disc.EnableInClassList("is-on", level.Mission == _chosen);
            level.Root.EnableInClassList("is-closed", !open);
            level.Root.EnableInClassList("is-far", !open && !next);
            // a tick on a won level, a lock on the next closed one; the road beyond it only fades
            Ui.Show(level.Badge, won || next);
            level.Badge.EnableInClassList("arena-rail__badge--won", won);
            level.Badge.EnableInClassList("arena-rail__badge--closed", next);
            Ui.Show(level.Star, level.Mission.Milestone);
            // the folk a first win opens for hire, until that win
            Ui.Show(level.Folk, level.HasFolk && !won);
            level.BadgeGlyph.EnableInClassList("glyph--check", won);
            level.BadgeGlyph.EnableInClassList("glyph--lock", next);
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
            var offer = _offer = session.ArenaOffer(mission);
            int min = offer.GoldMin, max = offer.GoldMax;

            if (_posterOf != mission)
            {
                _posterOf = mission;
                Ui.SetText(_name, StripLevel(mission.DisplayName));
                Ui.SetText(_biome, BiomeName(mission.Biome));
                FillEnemies(mission);
                FillEnemyGear(offer);
                Ui.SetText(_statHealth, "×" + Multiplier(offer.EnemyHealthPercent));
                Ui.SetText(_statDamage, "+" + offer.EnemyDamageBonus);
                Ui.SetText(_statArmor, "+" + offer.EnemyArmorBonus);
            }
            var ladder = session.ArenaLadder();
            Ui.SetText(_eyebrow, EyebrowOf(offer, wins, ladder.Count > 0 && ladder[ladder.Count - 1] == mission));
            int limit = session.SquadLimit(mission);
            if (limit != _squadShown) FillSquad(limit);
            ShowOdds(offer);

            Ui.SetText(_gold, max > min ? $"{min}–{max}" : min.ToString());
            Ui.SetText(_goldNote, offer.FirstWin ? "золота за первую победу"
                : offer.PaysFromFund ? "золота за победу" : "фонд пуст: только ставка назад");
            ShowTrophies(offer);
            ShowFund(offer.Fund);
            Ui.SetText(_stakeValue, offer.Stake.ToString());

            // a first win over a folk opens it for hire
            var unit = offer.UnlockUnit.HasValue ? _context.Catalog.TryGetUnit(offer.UnlockUnit.Value) : null;
            Ui.Show(_hire, unit != null);
            if (unit != null)
            {
                Ui.SetText(_hireName, unit.DisplayName);
                Ui.SetPicture(_hireArt, RewardArt.Tight(unit.PortraitSprite));
            }

            // what stands between the colony and this battle, or what a defeat would cost
            string glyph = null;
            string state;
            if (!open)
            {
                state = enter.Error;
                glyph = "glyph--lock";
            }
            else if (enter.Ok)
                state = offer.ClosesOnDefeat
                    ? $"Поражение: ставка сгорит, отдых {TopBar.Duration(offer.DefeatRestMs)}, уровень закроется до победы на {offer.ReopenLevel}-м"
                    : $"Поражение: ставка сгорит, отдых {TopBar.Duration(offer.DefeatRestMs)}";
            else if (wait > 0)
            {
                state = $"Отдых {TopBar.Duration(wait)}";
                glyph = "glyph--rest";
            }
            else state = enter.Error;
            Ui.SetText(_state, state);
            Ui.Show(_statePicture, false);
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

        // "Уровень 7", "Уровень 7. Побед: 3", on a milestone "Веха. Уровень 10", at the won top of the ladder its end
        private static string EyebrowOf(ArenaOfferSnapshot offer, int wins, bool top)
        {
            if (top && offer.LadderComplete) return "Лестница пройдена. Повторы платят из фонда.";
            if (offer.Milestone)
                return wins > 0 ? $"Веха. Уровень {offer.Level}. Побед: {wins}" : $"Веха. Уровень {offer.Level}";
            return wins > 0 ? $"Уровень {offer.Level}. Побед: {wins}" : $"Уровень {offer.Level}";
        }

        private void ShowOdds(ArenaOfferSnapshot offer)
        {
            Ui.SetText(_odds, offer.Odds == OddsGrade.Stronger ? "Отряд сильнее"
                : offer.Odds == OddsGrade.Even ? "На равных" : "Отряд слабее");
            _odds.EnableInClassList("arena-odds--stronger", offer.Odds == OddsGrade.Stronger);
            _odds.EnableInClassList("arena-odds--even", offer.Odds == OddsGrade.Even);
            _odds.EnableInClassList("arena-odds--weaker", offer.Odds == OddsGrade.Weaker);
        }

        private string OddsHint()
        {
            if (_offer == null) return null;
            if (_offer.SquadCount == 0) return "В колонии нет бойцов: наймите существ в бараках.";
            string basis = $"Оценка по лучшим {_offer.SquadCount} бойцам колонии и её снаряжению. Это подсказка, исход решает бой.";
            return _offer.Odds == OddsGrade.Weaker
                ? basis + "\n" + "Сделайте отряд сильнее: улучшения бараков, снаряжение, больше бойцов."
                : basis;
        }

        private string TrophiesHint()
        {
            if (_offer == null || _offer.Trophies.Count == 0) return null;
            var names = new List<string>();
            foreach (var trophy in _offer.Trophies) names.Add($"{trophy.Name} ×{trophy.Amount}");
            return string.Join("\n", names);
        }

        // up to three items the enemies wear, as their pictures
        private void FillEnemyGear(ArenaOfferSnapshot offer)
        {
            _enemyGear.Clear();
            for (int i = 0; i < offer.EnemyGear.Count && i < GearShown; i++)
            {
                var item = offer.EnemyGear[i];
                var token = Ui.Box("arena-enemy-gear__item");
                Ui.SetPicture(token, item.Icon);
                _enemyGear.Add(token);
                if (_tooltip != null)
                {
                    string title = item.DisplayName;
                    _tooltip.Attach(token, () => title, () => "Враги носят это снаряжение, и оно действует в бою.");
                }
            }
        }

        private void ShowTrophies(ArenaOfferSnapshot offer)
        {
            Ui.Show(_trophies, offer.Trophies.Count > 0);
            if (offer.Trophies.Count == 0) return;
            var key = new List<string>();
            int count = 0;
            foreach (var trophy in offer.Trophies)
            {
                key.Add($"{trophy.Resource}:{trophy.Amount}");
                count += trophy.Amount;
            }
            string signature = string.Join(",", key);
            if (signature == _trophiesShown) return;
            _trophiesShown = signature;
            _trophyArt.Clear();
            for (int i = 0; i < offer.Trophies.Count && i < TrophiesShown; i++)
            {
                var picture = Ui.Box("arena-trophies__item");
                picture.pickingMode = PickingMode.Ignore;
                Ui.SetPicture(picture, _context.Catalog.TryGetResource(offer.Trophies[i].Resource)?.Icon);
                _trophyArt.Add(picture);
            }
            Ui.SetText(_trophyCount, "×" + count);
        }

        private void ShowFund(ArenaFundSnapshot fund)
        {
            while (_fundDots.childCount < fund.Cap)
            {
                var dot = Ui.Box("arena-fund__dot");
                dot.pickingMode = PickingMode.Ignore;
                _fundDots.Add(dot);
            }
            while (_fundDots.childCount > fund.Cap) _fundDots.RemoveAt(_fundDots.childCount - 1);
            for (int i = 0; i < _fundDots.childCount; i++)
                _fundDots[i].EnableInClassList("arena-fund__dot--full", i < fund.Payouts);
            Ui.SetText(_fundNext, fund.NextInMs < 0 ? "фонд полон" : $"через {TopBar.Duration(fund.NextInMs)}");
        }

        // 250 % reads "2.5": the enemies' health over their kind's
        private static string Multiplier(int percent) =>
            (percent / 100.0).ToString("0.#", CultureInfo.InvariantCulture);

        private static string BiomeName(ArenaBiome biome)
        {
            switch (biome)
            {
                case ArenaBiome.Forest: return "Лес";
                case ArenaBiome.Swamp: return "Болото";
                case ArenaBiome.Graveyard: return "Кладбище";
                case ArenaBiome.MountainPass: return "Горная застава";
                case ArenaBiome.Snow: return "Снега";
                default: return "Луг";
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
            // a milestone's star, on the disc's other shoulder
            level.Star = Ui.Box("glyph glyph--star arena-rail__star");
            level.Star.pickingMode = PickingMode.Ignore;
            Ui.Show(level.Star, false);
            level.Disc.Add(level.Star);
            level.Folk = Ui.Box("arena-rail__folk");
            level.Folk.pickingMode = PickingMode.Ignore;
            Ui.Show(level.Folk, false);
            level.Disc.Add(level.Folk);
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

        private Level LevelOf(BattleMissionDefinition mission)
        {
            foreach (var level in _levels)
                if (Ui.IsShown(level.Root) && level.Mission == mission) return level;
            return null;
        }

        // The level's state in words for its disc's hint.
        private string StateOf(BattleMissionDefinition mission)
        {
            var session = _context.Session;
            var enter = session.CanEnterMission(mission.MissionId);
            int wins = session.MissionWins(mission.MissionId);
            int wait = session.MissionWaitMs(mission.MissionId);
            if (!session.IsMissionUnlocked(mission.MissionId))
                return mission == _nextClosed ? enter.Error : $"Откроется после победы на {mission.Level - 1}-м уровне";
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
