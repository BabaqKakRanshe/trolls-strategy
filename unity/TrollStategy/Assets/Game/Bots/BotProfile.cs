using System;
using System.Collections.Generic;
using TrollStrategy.Content;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// How a bot plays: how often it looks at the colony, whether it invests beyond what the quests ask, whom it
    /// puts to work and when it fights. A profile stands for a kind of player, not for a skill level: every
    /// profile follows the same quest planner and keeps the same goods flowing.
    /// </summary>
    public sealed class BotProfile
    {
        public BotProfile(string id, string title, string description, float thinkSeconds, bool grows,
            UnitKind workerKind = UnitKind.Goblin, int maxHaulersPerRoute = 6, int maxRawProducers = 3,
            float growthGoldFactor = 2f, float reserveShare = 1f, int squadTrolls = 2, bool fightsForGold = false,
            float actionSeconds = 0f, float questReadSeconds = 0f, float roleHiring = 0f,
            BuildingKind[] upgradeHosts = null)
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

        private static readonly BuildingKind[] Guild = { BuildingKind.HaulersGuild };

        public static readonly BotProfile Human = new("human", "Живой игрок",
            "Как обычный, но с ценой интерфейса: команда стоит 8 секунд, чтение задания 30, носильщиков на маршруте не больше трёх.",
            thinkSeconds: 15f, grows: true, maxHaulersPerRoute: 3, actionSeconds: 8f, questReadSeconds: 30f,
            roleHiring: 0.9f, upgradeHosts: Guild);

        public static readonly BotProfile Passive = new("passive", "Пассивный",
            "Делает только то, что просят задания, нанимает гоблинов и заглядывает в колонию раз в минуту.",
            thinkSeconds: 60f, grows: false);

        public static readonly BotProfile Typical = new("typical", "Обычный",
            "Задания плюс умеренный рост: дозаполняет добычу и носильщиков, ставит жителей на их ремесло, копит на гильдию носильщиков; смотрит раз в 20 секунд.",
            thinkSeconds: 20f, grows: true, roleHiring: 0.9f, upgradeHosts: Guild);

        public static readonly BotProfile Active = new("active", "Активный",
            "Смотрит каждые 5 секунд и вкладывает свободное золото в добычу, мастеров и гильдию носильщиков, даже когда копит на задание.",
            thinkSeconds: 5f, grows: true, maxHaulersPerRoute: 10, maxRawProducers: 5, growthGoldFactor: 1.2f,
            reserveShare: 0.5f, roleHiring: 0.9f, upgradeHosts: Guild);

        public static readonly BotProfile Warlord = new("warlord", "Воитель",
            "Нанимает самых сильных, а не самых выгодных, ходит на арену при каждой возможности ради золота и вкладывается в казарму.",
            thinkSeconds: 10f, grows: true, workerKind: UnitKind.Troll, squadTrolls: 3, fightsForGold: true,
            roleHiring: 0.65f, upgradeHosts: new[] { BuildingKind.Barracks, BuildingKind.HaulersGuild });

        public static IReadOnlyList<BotProfile> All { get; } = new[] { Human, Passive, Typical, Active, Warlord };
    }
}
