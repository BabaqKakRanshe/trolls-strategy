using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Bots
{
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
            float arenaRisk = 0f)
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
