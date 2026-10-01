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
            float growthGoldFactor = 2f, float reserveShare = 1f, int squadTrolls = 2, bool fightsForGold = false)
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
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        /// <summary>Colony seconds between two looks at the colony; quests are claimed only on a look.</summary>
        public float ThinkSeconds { get; }
        /// <summary>Spends spare gold on more workers, haulers and raw producers while a quest waits.</summary>
        public bool Grows { get; }
        /// <summary>Who works where a quest asks for any creature, and whom growth hires as workers.</summary>
        public UnitKind WorkerKind { get; }
        public int MaxHaulersPerRoute { get; }
        /// <summary>Mines, fields and lumber camps growth may own in all.</summary>
        public int MaxRawProducers { get; }
        /// <summary>A new raw producer is built once spare gold covers its set-up this many times.</summary>
        public float GrowthGoldFactor { get; }
        /// <summary>Share of the gold a waiting quest step needs that growth leaves untouched (1 saves it all).</summary>
        public float ReserveShare { get; }
        /// <summary>Trolls the squad needs before it fights; every lost battle asks for one more.</summary>
        public int SquadTrolls { get; }
        /// <summary>Fights whenever the mission is ready, for its gold, not only when a quest asks.</summary>
        public bool FightsForGold { get; }

        public static readonly BotProfile Passive = new("passive", "Пассивный",
            "Делает только то, что просят задания, и заглядывает в колонию раз в минуту.",
            thinkSeconds: 60f, grows: false);

        public static readonly BotProfile Typical = new("typical", "Обычный",
            "Задания плюс умеренный рост: дозаполняет добычу и носильщиков, смотрит раз в 20 секунд.",
            thinkSeconds: 20f, grows: true);

        public static readonly BotProfile Active = new("active", "Активный",
            "Смотрит каждые 5 секунд и вкладывает свободное золото в добычу, даже когда копит на задание.",
            thinkSeconds: 5f, grows: true, maxHaulersPerRoute: 10, maxRawProducers: 5, growthGoldFactor: 1.2f,
            reserveShare: 0.5f);

        public static readonly BotProfile Warlord = new("warlord", "Воитель",
            "Ставит на работу троллей и ходит в бой при каждой возможности ради золота.",
            thinkSeconds: 10f, grows: true, workerKind: UnitKind.Troll, squadTrolls: 3, fightsForGold: true);

        public static IReadOnlyList<BotProfile> All { get; } = new[] { Passive, Typical, Active, Warlord };
    }
}
