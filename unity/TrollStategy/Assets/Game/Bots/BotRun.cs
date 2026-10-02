using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    public enum BotOutcome
    {
        /// <summary>Every quest of the authored chain was claimed.</summary>
        Completed,
        /// <summary>No goal of the current quest moved for the stall limit.</summary>
        Stalled,
        /// <summary>The colony time limit ran out while the quests still moved.</summary>
        TimeLimit
    }

    /// <summary>Why a bot was not doing anything for its quest during a look at the colony.</summary>
    public enum BotWaitKind
    {
        /// <summary>Everything the quest needs is in place; goods are being made, carried or sold.</summary>
        Flow,
        /// <summary>The next step costs more gold than the colony has.</summary>
        Gold,
        /// <summary>A clock holds the step back: the mission opening or recovering, land being cleared.</summary>
        Time
    }

    /// <summary>One claimed quest: when it began and ended in colony time and what held it up.</summary>
    public sealed class QuestRecord
    {
        public int Level;
        public string Id;
        public string Title;
        public int StartMs;
        public int DoneMs;
        public int FlowWaitMs;
        public int GoldWaitMs;
        public int TimeWaitMs;
        /// <summary>Player time spent on commands and reading the quest (profiles with action costs).</summary>
        public int BusyMs;
        public int GoldAfterClaim;
        public int Population;
        public int Buildings;
        public int DurationMs => DoneMs - StartMs;
    }

    public sealed class BattleRecord
    {
        public int AtMs;
        public int QuestLevel;
        /// <summary>The level of the arena ladder the battle was fought on.</summary>
        public int ArenaLevel;
        public string Squad;
        public BattleOutcome Outcome;
        public int Fallen;
        public int Gold;
    }

    /// <summary>The colony once a minute, for the timeline.</summary>
    public sealed class BotSample
    {
        public int AtMs;
        public int QuestLevel;
        public int Gold;
        public int SoldGoods;
        public int Population;
        public int Buildings;
        public int LandBlocks;
        public int ArenaLevel;
    }

    /// <summary>A creature the colony could hire from this moment on (folk join after their arena level is won).</summary>
    public sealed class UnlockRecord
    {
        public string Name;
        public int AtMs;
        public int QuestLevel;
    }

    /// <summary>Everything one bot run left behind: the outcome, the quest timeline, battles and refusals.</summary>
    public sealed class BotRun
    {
        public BotRun(BotProfile profile, int chainLength)
        {
            Profile = profile;
            ChainLength = chainLength;
        }

        public BotProfile Profile { get; }
        public int ChainLength { get; }
        public BotOutcome Outcome { get; set; }
        /// <summary>For a stall or a time limit: the quest and what the bot last waited for.</summary>
        public string StopReason { get; set; }
        public int EndMs { get; set; }
        public int FinalGold { get; set; }
        public int FinalPopulation { get; set; }
        public int FinalBuildings { get; set; }
        public int Hired { get; set; }
        public int LandBought { get; set; }
        /// <summary>The highest arena level won by the end.</summary>
        public int ArenaLevel { get; set; }
        public int BattleGold => Battles.Sum(b => b.Gold);
        /// <summary>The colony at the end by creature name.</summary>
        public SortedDictionary<string, int> Units { get; } = new(System.StringComparer.Ordinal);
        /// <summary>Levels of the colony upgrades bought by the end, by name.</summary>
        public SortedDictionary<string, int> Upgrades { get; } = new(System.StringComparer.Ordinal);
        public List<UnlockRecord> Unlocks { get; } = new();
        public int Decisions { get; set; }
        public List<QuestRecord> Quests { get; } = new();
        public List<BattleRecord> Battles { get; } = new();
        public List<BotSample> Samples { get; } = new();
        /// <summary>Refused commands by the session's message; a healthy bot sees few.</summary>
        public SortedDictionary<string, int> Refusals { get; } = new(System.StringComparer.Ordinal);
        public int CommandsAccepted { get; set; }

        public void Refused(string error)
        {
            string key = string.IsNullOrEmpty(error) ? "?" : error;
            Refusals.TryGetValue(key, out int count);
            Refusals[key] = count + 1;
        }
    }
}
