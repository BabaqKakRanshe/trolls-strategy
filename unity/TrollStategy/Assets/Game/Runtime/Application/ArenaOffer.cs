using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>What a finished battle cost and whether a reward waits in the colony, for the verdict.</summary>
    public sealed class BattleCost
    {
        public BattleCost(int burnedStake, int closedLevel, int reopenLevel, int restMs, bool rewardWaits)
        {
            BurnedStake = burnedStake;
            ClosedLevel = closedLevel;
            ReopenLevel = reopenLevel;
            RestMs = restMs;
            RewardWaits = rewardWaits;
        }

        public int BurnedStake { get; }
        /// <summary>The level the defeat closed again; 0 when none.</summary>
        public int ClosedLevel { get; }
        /// <summary>The level whose win opens the closed one again.</summary>
        public int ReopenLevel { get; }
        public int RestMs { get; }
        /// <summary>Gold or trophies wait to be taken in the colony.</summary>
        public bool RewardWaits { get; }

        /// <summary>The cost of a battle the session ran; null without one.</summary>
        public static BattleCost Of(BattleRunState run, GameContentCatalog catalog, bool rewardWaits)
        {
            if (run == null) return null;
            var mission = catalog?.TryGetMission(run.MissionId);
            int level = mission != null ? mission.Level : 0;
            return new BattleCost(run.BurnedStake, run.ClosedMissionId != null ? level : 0, Math.Max(0, level - 1),
                run.RestMs, rewardWaits);
        }
    }

    /// <summary>The arena's prize fund as the arena window shows it.</summary>
    public sealed class ArenaFundSnapshot
    {
        public ArenaFundSnapshot(int payouts, int cap, int nextInMs, int periodMs)
        {
            Payouts = payouts;
            Cap = cap;
            NextInMs = nextInMs;
            PeriodMs = periodMs;
        }

        public int Payouts { get; }
        public int Cap { get; }
        /// <summary>Active time until the next payout; −1 while the fund is full.</summary>
        public int NextInMs { get; }
        public int PeriodMs { get; }
    }

    /// <summary>
    /// Everything the colony can know about an arena level before the battle: how strong its enemies are, how the
    /// colony's best squad compares, what a win brings, what the battle stakes and what a defeat costs. The arena
    /// window, the barracks' card and the bots read the same offer.
    /// </summary>
    public sealed class ArenaOfferSnapshot
    {
        public BattleMissionDefinition Mission { get; internal set; }
        public int Level { get; internal set; }
        public bool Milestone { get; internal set; }
        public BattleFormation Formation { get; internal set; }
        public ArenaBiome Biome { get; internal set; }
        /// <summary>The enemies' health against their kind's, in percent.</summary>
        public int EnemyHealthPercent { get; internal set; }
        /// <summary>The most an enemy adds to its kind's damage: the level's bonus and its gear (the champion aside).</summary>
        public int EnemyDamageBonus { get; internal set; }
        public int EnemyArmorBonus { get; internal set; }
        /// <summary>The items the enemies wear, each once, in slot order.</summary>
        public IReadOnlyList<EquipmentDefinition> EnemyGear { get; internal set; }
        /// <summary>The milestone champion's kind, or null.</summary>
        public UnitKind? Champion { get; internal set; }
        public OddsGrade Odds { get; internal set; }
        public double OddsRatio { get; internal set; }
        /// <summary>Fighters the estimate took: the colony's strongest, as many as may go.</summary>
        public int SquadCount { get; internal set; }
        public bool FirstWin { get; internal set; }
        /// <summary>A repeat win here takes a payout from the prize fund (and the fund has one).</summary>
        public bool PaysFromFund { get; internal set; }
        /// <summary>The gold a win pays now; 0–0 for a repeat while the fund is empty.</summary>
        public int GoldMin { get; internal set; }
        public int GoldMax { get; internal set; }
        /// <summary>The trophies a win brings now; none for an unpaid repeat.</summary>
        public IReadOnlyList<ResourceStack> Trophies { get; internal set; }
        public UnitKind? UnlockUnit { get; internal set; }
        public int Stake { get; internal set; }
        /// <summary>Gold still missing for the stake; 0 when the treasury holds it.</summary>
        public int GoldShort { get; internal set; }
        public ArenaFundSnapshot Fund { get; internal set; }
        public int DefeatRestMs { get; internal set; }
        /// <summary>A defeat here closes the level until a new win on the one below.</summary>
        public bool ClosesOnDefeat { get; internal set; }
        public int ReopenLevel { get; internal set; }
        /// <summary>The colony has won the ladder's top level.</summary>
        public bool LadderComplete { get; internal set; }
    }

    public static class ArenaOffers
    {
        public static ArenaFundSnapshot Fund(GameState state, GameContentCatalog catalog)
        {
            var economy = catalog.Economy;
            int cap = economy.ArenaFundCap, period = economy.ArenaFundPeriodMs;
            return new ArenaFundSnapshot(ArenaFund.Available(state, cap, period), cap,
                ArenaFund.NextInMs(state, cap, period), period);
        }

        public static ArenaOfferSnapshot Create(GameState state, BattleMissionDefinition mission, GameContentCatalog catalog)
        {
            if (state == null || mission == null || catalog == null) return null;
            var economy = catalog.Economy;
            var offer = new ArenaOfferSnapshot
            {
                Mission = mission,
                Level = mission.Level,
                Milestone = mission.Milestone,
                Formation = mission.Formation,
                Biome = mission.Biome,
                EnemyHealthPercent = mission.EnemyHealthPercent
            };

            // the enemies' strength and gear, as the battle will take them
            var gear = new List<EquipmentDefinition>();
            int damage = 0, armor = 0;
            foreach (var enemy in mission.Enemies)
            {
                int enemyDamage = mission.EnemyDamageBonus, enemyArmor = mission.EnemyArmorBonus;
                foreach (string id in enemy.GearIds)
                {
                    var item = Equipment(catalog, id);
                    if (item == null) continue;
                    enemyDamage += item.DamageBonus;
                    enemyArmor += item.ArmorBonus;
                    if (!gear.Contains(item)) gear.Add(item);
                }
                // the strength line speaks of the level's enemies, as its health does; the champion is its own
                if (enemy.Champion && mission.ChampionHealthPercent > 0) offer.Champion = enemy.Kind;
                else
                {
                    damage = Math.Max(damage, enemyDamage);
                    armor = Math.Max(armor, enemyArmor);
                }
            }
            gear.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.ItemId, b.ItemId));
            offer.EnemyDamageBonus = damage;
            offer.EnemyArmorBonus = armor;
            offer.EnemyGear = gear;

            // the colony's best squad against them
            var squad = BestSquad(state, mission, catalog);
            offer.SquadCount = squad.Count;
            List<BattleFighterInput> enemies;
            try { enemies = BattleApplication.EnemyFighters(mission, catalog); }
            catch (ArgumentOutOfRangeException) { enemies = new List<BattleFighterInput>(); }
            var (ratio, grade) = BattleOdds.Compare(squad, enemies, economy.ArenaOddsMargin, economy.ArmorScale);
            offer.OddsRatio = ratio;
            offer.Odds = grade;

            // what a win brings now: a first win its full reward, a repeat only while the fund has a payout
            offer.Fund = Fund(state, catalog);
            offer.FirstWin = state.WinsOf(mission.MissionId) == 0;
            offer.PaysFromFund = !offer.FirstWin && offer.Fund.Payouts > 0;
            bool pays = offer.FirstWin || offer.PaysFromFund;
            var (min, max) = BattleApplication.WinGold(state, mission, catalog);
            offer.GoldMin = pays ? min : 0;
            offer.GoldMax = pays ? max : 0;
            var trophies = new List<ResourceStack>();
            if (pays)
                foreach (var trophy in mission.WinGoods)
                    if (trophy.Amount > 0)
                        trophies.Add(new ResourceStack(trophy.Resource,
                            catalog.TryGetResource(trophy.Resource)?.DisplayName ?? trophy.Resource.ToString(), trophy.Amount));
            offer.Trophies = trophies;
            offer.UnlockUnit = offer.FirstWin ? mission.UnlockUnit : null;

            // what the battle stakes
            offer.Stake = BattleApplication.Stake(state, mission, catalog);
            offer.GoldShort = Math.Max(0, offer.Stake - state.Gold);
            offer.DefeatRestMs = (int)Math.Ceiling(BattleApplication.CooldownSeconds(state, mission, catalog) *
                                                   economy.ArenaDefeatRestMultiplier * 1000f);
            offer.ClosesOnDefeat = BattleApplication.ClosesOnDefeat(state, mission, catalog);
            offer.ReopenLevel = mission.Level - 1;

            int top = 0;
            foreach (var candidate in catalog.Missions)
                if (candidate != null) top = Math.Max(top, candidate.Level);
            offer.LadderComplete = top > 0 && state.HighestMissionLevel >= top;
            return offer;
        }

        /// <summary>
        /// The squad the colony could field here at its strongest: its toughest creatures (health times damage), as
        /// many as may go, each item of the colony on the strongest fighter whose slot it fits, best items first.
        /// </summary>
        internal static List<BattleFighterInput> BestSquad(GameState state, BattleMissionDefinition mission,
            GameContentCatalog catalog)
        {
            int limit = BattleApplication.SquadLimit(state, mission, catalog);
            var units = new List<(UnitState Unit, int Strength)>();
            foreach (var unit in state.Units)
            {
                var definition = catalog.TryGetUnit(unit.Kind);
                if (definition != null) units.Add((unit, definition.CombatHealth * definition.CombatDamage));
            }
            units.Sort((a, b) => a.Strength != b.Strength ? b.Strength.CompareTo(a.Strength)
                : string.CompareOrdinal(a.Unit.Id, b.Unit.Id));
            if (units.Count > limit) units.RemoveRange(limit, units.Count - limit);

            var items = new List<(EquipmentState Item, EquipmentDefinition Definition)>();
            foreach (var item in state.Equipment)
            {
                var definition = Equipment(catalog, item.DefinitionId);
                if (definition != null) items.Add((item, definition));
            }
            items.Sort((a, b) =>
            {
                int worth = (b.Definition.DamageBonus + b.Definition.ArmorBonus)
                    .CompareTo(a.Definition.DamageBonus + a.Definition.ArmorBonus);
                return worth != 0 ? worth : string.CompareOrdinal(a.Item.Id, b.Item.Id);
            });
            var worn = new List<EquipmentState>();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (item, definition) in items)
                foreach (var (unit, _) in units)
                    if (taken.Add($"{unit.Id}:{definition.Slot}"))
                    {
                        worn.Add(new EquipmentState { Id = item.Id, DefinitionId = item.DefinitionId, OwnerUnitId = unit.Id });
                        break;
                    }

            int healthPercent = UpgradeRules.Total(state, catalog, UpgradeEffect.FighterHealthPercent);
            int damageBonus = UpgradeRules.Total(state, catalog, UpgradeEffect.FighterDamage);
            var squad = new List<BattleFighterInput>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                var cell = i < mission.PlayerDeployment.Count ? mission.PlayerDeployment[i] : default;
                squad.Add(BattleApplication.FighterInput(units[i].Unit.Id, units[i].Unit.Kind, true, cell, catalog, worn,
                    100 + healthPercent, damageBonus, 0));
            }
            return squad;
        }

        private static EquipmentDefinition Equipment(GameContentCatalog catalog, string id)
        {
            foreach (var item in catalog.Equipment)
                if (item != null && item.ItemId == id) return item;
            return null;
        }
    }
}
