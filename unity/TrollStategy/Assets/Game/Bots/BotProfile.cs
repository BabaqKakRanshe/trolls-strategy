using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Bots
{
    /// <summary>Where a bot's gold comes from beyond the quests.</summary>
    public enum BotEconomy
    {
        /// <summary>More raw producers, their goods sold at the market.</summary>
        RawGoods,
        /// <summary>Finished goods: copies of the workshop whose product sells best, raw producers only for a hungry workshop.</summary>
        Crafts,
        /// <summary>Arena prizes: fights for gold whenever it may, the barracks and the armory first.</summary>
        Arena
    }

    /// <summary>What comes first in a look.</summary>
    public enum BotPace
    {
        /// <summary>The quest's steps, then growth with what the quest leaves.</summary>
        QuestFirst,
        /// <summary>Growth first, saving little for the quest; the quest's steps after.</summary>
        EconomyFirst
    }

    /// <summary>Where a new building goes.</summary>
    public enum BotLayout
    {
        /// <summary>Near the colony's core, one cell between buildings.</summary>
        Compact,
        /// <summary>Near the buildings it trades with: chains stand together.</summary>
        Districts,
        /// <summary>Away from the others, two cells between buildings, over the whole island.</summary>
        Spread
    }

    /// <summary>How growth adds capacity.</summary>
    public enum BotGrowth
    {
        /// <summary>New buildings.</summary>
        Wide,
        /// <summary>Levels of full, working buildings first; new buildings once none can rise.</summary>
        Up
    }

    /// <summary>The army a bot keeps.</summary>
    public enum BotForce
    {
        /// <summary>The trolls the first battle needs, one more after each loss.</summary>
        Small,
        /// <summary>A full squad of trolls hired ahead of the battles.</summary>
        Large,
        /// <summary>Gear for the whole squad: the forge works for the armory until every fighter has a weapon.</summary>
        Gear
    }

    /// <summary>
    /// How a bot plays: how often it looks at the colony, what a click costs it, whether it invests beyond what the
    /// quests ask, whom it puts to work and when it fights. Every profile follows the same quest planner and keeps
    /// the same goods flowing. The players the reports stand for are personas drawn by <see cref="BotPopulation"/>;
    /// <see cref="Typical"/> is the reference bot the tests play.
    /// </summary>
    public sealed class BotProfile
    {
        public BotProfile(string id, string title, string description, float thinkSeconds, bool grows,
            UnitKind workerKind = UnitKind.Goblin, int maxHaulersPerRoute = 6, int maxRawProducers = 3,
            float growthGoldFactor = 2f, float reserveShare = 1f, int squadTrolls = 2, bool fightsForGold = false,
            float actionSeconds = 0f, float questReadSeconds = 0f, float roleHiring = 0f,
            BuildingKind[] upgradeHosts = null, int seed = 0, float inattention = 0f, int placementChoice = 1,
            float arenaRisk = 0f, BotEconomy economy = BotEconomy.RawGoods, BotPace pace = BotPace.QuestFirst,
            BotLayout layout = BotLayout.Compact, BotGrowth growth = BotGrowth.Wide, BotForce force = BotForce.Small,
            bool buysLand = false, float novice = 0f)
        {
            Id = id;
            Title = title;
            Description = description;
            ThinkSeconds = thinkSeconds;
            Grows = grows;
            WorkerKind = workerKind;
            MaxHaulersPerRoute = maxHaulersPerRoute;
            MaxRawProducers = maxRawProducers;
            GrowthGoldFactor = growthGoldFactor;
            ReserveShare = reserveShare;
            SquadTrolls = squadTrolls;
            FightsForGold = fightsForGold;
            ActionSeconds = actionSeconds;
            QuestReadSeconds = questReadSeconds;
            RoleHiring = roleHiring;
            UpgradeHosts = upgradeHosts ?? Array.Empty<BuildingKind>();
            Seed = seed;
            Inattention = inattention;
            PlacementChoice = placementChoice;
            ArenaRisk = arenaRisk;
            Economy = economy;
            Pace = pace;
            Layout = layout;
            Growth = growth;
            Force = force;
            BuysLand = buysLand;
            Novice = novice;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        /// <summary>Colony seconds between two looks at the colony; quests are claimed only on a look.</summary>
        public float ThinkSeconds { get; }
        /// <summary>Spends spare gold on more workers, haulers, raw producers and upgrades while a quest waits.</summary>
        public bool Grows { get; }
        /// <summary>Who works where a quest asks for any creature while <see cref="RoleHiring"/> is 0.</summary>
        public UnitKind WorkerKind { get; }
        public int MaxHaulersPerRoute { get; }
        /// <summary>Mines, fields and lumber camps growth may own in all.</summary>
        public int MaxRawProducers { get; }
        /// <summary>A new raw producer or upgrade is bought once spare gold covers it this many times.</summary>
        public float GrowthGoldFactor { get; }
        /// <summary>Share of the gold a waiting quest step needs that growth leaves untouched (1 saves it all).</summary>
        public float ReserveShare { get; }
        /// <summary>Trolls the squad needs before it fights; every lost battle asks for one more.</summary>
        public int SquadTrolls { get; }
        /// <summary>Fights whenever the mission is ready, for its gold, not only when a quest asks.</summary>
        public bool FightsForGold { get; }
        /// <summary>
        /// Player time one command costs at the HUD (finding the button, placing, selecting creatures). The colony
        /// runs on meanwhile and the next look comes that much later; 0 acts instantly.
        /// </summary>
        public float ActionSeconds { get; }
        /// <summary>Player time spent reading a quest when it begins.</summary>
        public float QuestReadSeconds { get; }
        /// <summary>
        /// Hires for the job: of the creatures the colony may hire whose work (or carrying) per gold is at least
        /// this share of the best, the one that does most per head; 0 hires <see cref="WorkerKind"/> to work and
        /// goblins to carry.
        /// </summary>
        public float RoleHiring { get; }
        /// <summary>Buildings whose upgrades growth buys, in this order; empty buys none beyond the quests.</summary>
        public IReadOnlyList<BuildingKind> UpgradeHosts { get; }
        /// <summary>The persona's number in <see cref="BotPopulation"/>; 0 for a profile made by hand.</summary>
        public int Seed { get; }
        /// <summary>
        /// Share of looks that go to the quest alone: the bot does not top up haulers and workers or grow, as a
        /// player who does not watch the whole colony every time. The bot's own dice decide each look.
        /// </summary>
        public float Inattention { get; }
        /// <summary>A new building goes on one of this many best spots, any of them; 1 always takes the nearest.</summary>
        public int PlacementChoice { get; }
        /// <summary>Share of looks in which a squad the arena window calls weaker climbs anyway.</summary>
        public float ArenaRisk { get; }

        // the strategy: what the bot builds its colony on, not only how much of it (since 2026-10-09)

        /// <summary>Where gold comes from beyond the quests: raw goods, finished goods or the arena.</summary>
        public BotEconomy Economy { get; }
        /// <summary>Whether growth or the quest's steps come first in a look (for a bot that grows).</summary>
        public BotPace Pace { get; }
        /// <summary>Where new buildings go: near the core, by chains, or over the whole island.</summary>
        public BotLayout Layout { get; }
        /// <summary>Whether growth builds new buildings or raises the levels of those it has.</summary>
        public BotGrowth Growth { get; }
        /// <summary>The army: the trolls a battle needs, a full squad ahead, or gear for every fighter.</summary>
        public BotForce Force { get; }
        /// <summary>Buys and clears land with spare gold before a building needs it.</summary>
        public bool BuysLand { get; }
        /// <summary>
        /// Share of looks with a newcomer's move: it moves a building it just placed, sells a creature it hired, takes a
        /// hauler off its route or leafs through the book. Moves a player makes and the game allows; they cost time and gold.
        /// </summary>
        public float Novice { get; }

        private static readonly BuildingKind[] Guild = { BuildingKind.HaulersGuild };

        /// <summary>
        /// The reference bot of the tests: no click costs, moderate growth, hires by craft. It checks that the chain
        /// can be played; it is not one of the players the reports stand for.
        /// </summary>
        public static readonly BotProfile Typical = new("typical", "Обычный",
            "Задания плюс умеренный рост: дозаполняет добычу и носильщиков, ставит жителей на их ремесло, копит на гильдию носильщиков; смотрит раз в 20 секунд.",
            thinkSeconds: 20f, grows: true, roleHiring: 0.9f, upgradeHosts: Guild);
    }
}
