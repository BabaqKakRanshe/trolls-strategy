using System;
using System.Collections.Generic;
using System.Globalization;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>Why a save could not be written, listed or loaded. None means it can.</summary>
    public enum SaveError
    {
        None,
        /// <summary>The slot has no file (or no backup, when the backup was asked for).</summary>
        NotFound,
        /// <summary>A slot id must be 1–64 of a–z, 0–9, '-' and '_', starting with a letter or a digit.</summary>
        InvalidSlotId,
        /// <summary>The file system refused to read the file.</summary>
        Unreadable,
        /// <summary>The file system refused to write the file; the slot keeps what it had.</summary>
        WriteFailed,
        /// <summary>Not a save at all, broken JSON, cut short, or its body does not match its checksum.</summary>
        Corrupt,
        /// <summary>A JSON file, but not this game's save format.</summary>
        UnknownFormat,
        /// <summary>Written in a newer format than this build reads.</summary>
        NewerVersion,
        /// <summary>Older than any version this build can migrate.</summary>
        UnsupportedVersion,
        /// <summary>Made by another edition (the itch.io alpha, the Steam demo, the full game): its campaign ends elsewhere.</summary>
        OtherEdition,
        /// <summary>Names a building, creature, good, mission, item or upgrade this build's content does not have.</summary>
        UnknownContent,
        /// <summary>Two buildings, creatures or items share an id.</summary>
        DuplicateId,
        /// <summary>Points at a building, creature or item the save does not have.</summary>
        DanglingReference,
        /// <summary>Gear on a fighter that cannot wear it there (two items in one slot).</summary>
        InvalidOwnership,
        /// <summary>A number out of its range: negative gold, a level past the top, a position that is not a number.</summary>
        InvalidValue,
        /// <summary>Buildings off the grid, off cleared land, on top of each other or blocking a door.</summary>
        InvalidLayout,
        /// <summary>The land grid differs from this build's island.</summary>
        LandMismatch,
        /// <summary>The current quest is not in this build's chain, or its goals differ.</summary>
        QuestMismatch,
        /// <summary>The slot is the game's own (the autosave): the player may not write or delete it.</summary>
        Reserved
    }

    /// <summary>
    /// Why a slot cannot be opened, in the few words a slot card has. <see cref="NewerBuild"/>: a newer build wrote
    /// it (the card says which, from <see cref="SaveSlotInfo.WrittenBy"/>, and asks to update); <see cref="OtherEdition"/>:
    /// another edition's game; <see cref="Damaged"/>: unreadable or broken, only deleting is left.
    /// </summary>
    public enum SaveProblem
    {
        None,
        NewerBuild,
        OtherEdition,
        Damaged
    }

    /// <summary>The build that writes a save: shown on the slot card and checked on load.</summary>
    public sealed class SaveStamp
    {
        /// <param name="chainLength">Quests this build's games play before they end; 0 for the whole chain.</param>
        public SaveStamp(string build, string edition, int chainLength = 0)
        {
            Build = string.IsNullOrEmpty(build) ? "?" : build;
            Edition = string.IsNullOrEmpty(edition) ? "Full" : edition;
            ChainLength = Math.Max(0, chainLength);
        }

        /// <summary>The version label, "v1.0.412".</summary>
        public string Build { get; }
        /// <summary>
        /// The edition's name, "Alpha", "SteamDemo" or "Full". A save of the same edition opens as it is; one of another
        /// edition opens only when <see cref="CanCarry"/> (it is carried forward), otherwise it is refused.
        /// </summary>
        public string Edition { get; }
        /// <summary>Quests this build's games play before they end (the itch.io alpha, the Steam demo); 0 for the whole chain.</summary>
        public int ChainLength { get; }

        /// <summary>
        /// Whether this build opens a game of another edition whose chain ends after <paramref name="chainLength"/>
        /// quests (0: never ends): only when this build's chain is at least as long, so the game loses no quest it has
        /// (the alpha in the demo or the full game, the demo in the full game; never the other way).
        /// </summary>
        public bool CanCarry(int chainLength) => ChainLength == 0 || (chainLength > 0 && chainLength <= ChainLength);

        /// <summary>
        /// Whether <paramref name="build"/> ("v1.0.61") is a later version than this one ("v1.0.58"), number by
        /// number; false when either cannot be read.
        /// </summary>
        public bool IsOlderThan(string build)
        {
            var mine = Numbers(Build);
            var theirs = Numbers(build);
            if (mine == null || theirs == null) return false;
            for (int i = 0; i < Math.Max(mine.Count, theirs.Count); i++)
            {
                long a = i < mine.Count ? mine[i] : 0, b = i < theirs.Count ? theirs[i] : 0;
                if (a != b) return b > a;
            }
            return false;
        }

        private static List<long> Numbers(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            var numbers = new List<long>();
            foreach (string part in label.TrimStart('v', 'V').Split('.'))
            {
                if (!long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out long number)) return null;
                numbers.Add(number);
            }
            return numbers;
        }

        /// <summary>
        /// What keeps a save from opening, for its card: a newer format, or a rule this build's content breaks in a
        /// save a newer build wrote, is <see cref="SaveProblem.NewerBuild"/>; anything else broken is damage.
        /// </summary>
        public SaveProblem ProblemOf(SaveError error, string writtenBy)
        {
            switch (error)
            {
                case SaveError.None:
                case SaveError.Reserved:
                    return SaveProblem.None;
                case SaveError.NewerVersion:
                    return SaveProblem.NewerBuild;
                case SaveError.OtherEdition:
                    return SaveProblem.OtherEdition;
                case SaveError.UnknownContent:
                case SaveError.QuestMismatch:
                case SaveError.LandMismatch:
                case SaveError.InvalidLayout:
                    return IsOlderThan(writtenBy) ? SaveProblem.NewerBuild : SaveProblem.Damaged;
                default:
                    return SaveProblem.Damaged;
            }
        }
    }

    /// <summary>The colony at a glance, as a slot card shows it; read from the header without the state.</summary>
    public sealed class SaveSummary
    {
        public SaveSummary(bool campaign, int activeTimeMs, int questLevel, int questTotal, string questId,
            string questTitle, int gold, int creatures, int buildings, int battlesWon, int highestArenaLevel,
            int landOwned, bool finished, int chainLength = -1)
        {
            Campaign = campaign;
            ActiveTimeMs = activeTimeMs;
            QuestLevel = questLevel;
            QuestTotal = questTotal;
            QuestId = questId;
            QuestTitle = questTitle;
            Gold = gold;
            Creatures = creatures;
            Buildings = buildings;
            BattlesWon = battlesWon;
            HighestArenaLevel = highestArenaLevel;
            LandOwned = landOwned;
            Finished = finished;
            ChainLength = chainLength;
        }

        /// <summary>A quest chain game; false for the sandbox.</summary>
        public bool Campaign { get; }
        /// <summary>Colony time played: it stands still in battles and the pause menu.</summary>
        public int ActiveTimeMs { get; }
        /// <summary>The current quest's place in the chain, from 1; 0 in the sandbox.</summary>
        public int QuestLevel { get; }
        /// <summary>Quests in the chain this game plays (a short build's own length); repeatable quests follow it.</summary>
        public int QuestTotal { get; }
        /// <summary>The current quest's id; null when there is none (the sandbox, or every quest is done).</summary>
        public string QuestId { get; }
        /// <summary>The current quest's title as the content writes it (Russian, the translation key).</summary>
        public string QuestTitle { get; }
        public int Gold { get; }
        public int Creatures { get; }
        public int Buildings { get; }
        public int BattlesWon { get; }
        public int HighestArenaLevel { get; }
        /// <summary>Land blocks the colony owns, the start land included; 0 when the game has no land.</summary>
        public int LandOwned { get; }
        /// <summary>A campaign with no quest left: a short build's game played to its end, or the whole chain done.</summary>
        public bool Finished { get; }
        /// <summary>Quests the saved game plays before it ends: 0 for the whole chain, -1 when the file does not say.</summary>
        public int ChainLength { get; }
    }

    /// <summary>
    /// The part of a save every format version keeps in place: format version, the build that wrote it, and the
    /// lengths and checksums of the body and the thumbnail. A slot list reads it even from a save it cannot open.
    /// </summary>
    public readonly struct SaveEnvelope
    {
        public SaveEnvelope(int version, string build, int bodyBytes, uint bodyCrc, int thumbnailBytes, uint thumbnailCrc)
        {
            Version = version;
            Build = build;
            BodyBytes = bodyBytes;
            BodyCrc = bodyCrc;
            ThumbnailBytes = thumbnailBytes;
            ThumbnailCrc = thumbnailCrc;
        }

        public int Version { get; }
        /// <summary>The writer's version label, "v1.0.61"; null when the header has none.</summary>
        public string Build { get; }
        public int BodyBytes { get; }
        public uint BodyCrc { get; }
        /// <summary>The PNG after the body; 0 when the save has no picture.</summary>
        public int ThumbnailBytes { get; }
        public uint ThumbnailCrc { get; }
    }

    /// <summary>What the first line of a save says: format, who wrote it and when, and the colony at a glance.</summary>
    public sealed class SaveHeader
    {
        public SaveHeader(int version, DateTime savedAtUtc, string build, string edition, string colonyId, string label,
            SaveSummary summary, int bodyBytes, uint bodyCrc, int thumbnailBytes = 0, uint thumbnailCrc = 0)
        {
            Version = version;
            SavedAtUtc = savedAtUtc;
            Build = build;
            Edition = edition;
            ColonyId = colonyId;
            Label = label;
            Summary = summary;
            BodyBytes = bodyBytes;
            BodyCrc = bodyCrc;
            ThumbnailBytes = thumbnailBytes;
            ThumbnailCrc = thumbnailCrc;
        }

        /// <summary>The format version the file was written in.</summary>
        public int Version { get; }
        /// <summary>Real time of the save (UTC), taken by the store side; the game's own clock is in the summary.</summary>
        public DateTime SavedAtUtc { get; }
        /// <summary>The writer's version label, "v1.0.61".</summary>
        public string Build { get; }
        public string Edition { get; }
        /// <summary>The colony (one game from its start) the save belongs to.</summary>
        public string ColonyId { get; }
        /// <summary>The player's name for the save; null when it has none.</summary>
        public string Label { get; }
        public SaveSummary Summary { get; }
        public int BodyBytes { get; }
        public uint BodyCrc { get; }
        public int ThumbnailBytes { get; }
        public uint ThumbnailCrc { get; }
    }

    /// <summary>One save slot as a list shows it: its header and picture, or why it cannot be loaded.</summary>
    public sealed class SaveSlotInfo
    {
        public SaveSlotInfo(string slotId, bool isAutosave, SaveError status, string detail, SaveHeader header,
            bool fromBackup, bool hasBackup, DateTime fileTimeUtc, SaveProblem problem = SaveProblem.None,
            string writtenBy = null, byte[] thumbnail = null, string carriedFrom = null)
        {
            SlotId = slotId;
            IsAutosave = isAutosave;
            Status = status;
            Detail = detail;
            Header = header;
            FromBackup = fromBackup;
            HasBackup = hasBackup;
            FileTimeUtc = fileTimeUtc;
            Problem = status == SaveError.None || status == SaveError.NotFound ? SaveProblem.None
                : problem == SaveProblem.None ? SaveProblem.Damaged : problem;
            WrittenBy = writtenBy ?? header?.Build;
            Thumbnail = thumbnail;
            CarriedFrom = status == SaveError.None ? carriedFrom : null;
        }

        public string SlotId { get; }
        /// <summary>The colony's autosave; the player loads it but neither writes nor deletes it.</summary>
        public bool IsAutosave { get; }
        /// <summary>None when the header says this build can load it; the full check runs on load.</summary>
        public SaveError Status { get; }
        /// <summary>The fault in words for a log or a bug report (English, not shown to players).</summary>
        public string Detail { get; }
        /// <summary>The colony at a glance; null when the file has no readable header (a damaged slot still lists).</summary>
        public SaveHeader Header { get; }
        /// <summary>The slot's file is missing and this describes its backup (a write cut short); loading takes the backup.</summary>
        public bool FromBackup { get; }
        /// <summary>The previous save of the slot is kept beside it.</summary>
        public bool HasBackup { get; }
        /// <summary>When the file was last written; stands in for the header's time when there is no header.</summary>
        public DateTime FileTimeUtc { get; }
        /// <summary>What the card says when the slot cannot be loaded: a newer build's, another edition's, or damaged.</summary>
        public SaveProblem Problem { get; }
        /// <summary>The version label of the build that wrote it ("v1.0.61"), read even from a newer format; null when unreadable.</summary>
        public string WrittenBy { get; }
        /// <summary>The island when it was saved: a PNG of about 320×180, or null (no picture, or a damaged one).</summary>
        public byte[] Thumbnail { get; }
        /// <summary>
        /// The edition ("Alpha", "SteamDemo") that made this save when it is not this build's: the game opens carried
        /// forward into this edition's longer chain. Null for this edition's saves and for slots that cannot be loaded.
        /// </summary>
        public string CarriedFrom { get; }
        /// <summary>Nothing is saved in the slot.</summary>
        public bool IsEmpty => Status == SaveError.NotFound;
        /// <summary>The header says this build can load it. A full check of the state still runs on load.</summary>
        public bool Loadable => Status == SaveError.None;
        /// <summary>The header's time, the file's when the header is unreadable.</summary>
        public DateTime SavedAtUtc => Header?.SavedAtUtc ?? FileTimeUtc;
    }

    /// <summary>
    /// A whole game as a save holds it: the colony's state, checked against this build's content, and the session's
    /// clock between two simulation steps. <see cref="GameSession.Restore"/> plays on from it.
    /// </summary>
    public sealed class SavedGame
    {
        public SavedGame(SaveHeader header, GameState state, float stepRemainderSeconds, string carriedFrom = null,
            bool chainExtended = false, string slotId = null)
        {
            Header = header;
            State = state ?? throw new ArgumentNullException(nameof(state));
            StepRemainderSeconds = stepRemainderSeconds;
            CarriedFrom = carriedFrom;
            ChainExtended = chainExtended;
            SlotId = slotId;
        }

        /// <summary>Null for a game exported from a session and not written yet.</summary>
        public SaveHeader Header { get; }
        /// <summary>The colony as it was; the session restores from a copy, so this one stays as read.</summary>
        public GameState State { get; }
        /// <summary>Colony time the session had gathered toward its next simulation step.</summary>
        public float StepRemainderSeconds { get; }
        /// <summary>The edition that made the save when it was carried forward into this build's; null otherwise.</summary>
        public string CarriedFrom { get; }
        /// <summary>The save's short game had ended; carried into a longer chain, it opens on the quest after its end.</summary>
        public bool ChainExtended { get; }
        /// <summary>The slot it was read from; null for a game that did not come from a slot.</summary>
        public string SlotId { get; }

        /// <summary>The same game, read from <paramref name="slotId"/>.</summary>
        public SavedGame InSlot(string slotId) =>
            new(Header, State, StepRemainderSeconds, CarriedFrom, ChainExtended, slotId);
    }

    /// <summary>The answer to a save: the slot as it now lists, or why nothing was written.</summary>
    public sealed class SaveResult
    {
        public SaveResult(string slotId, bool autosave, string reason, SaveError error, string detail, SaveSlotInfo slot,
            double milliseconds)
        {
            SlotId = slotId;
            Autosave = autosave;
            Reason = reason;
            Error = error;
            Detail = detail;
            Slot = slot;
            Milliseconds = milliseconds;
        }

        public string SlotId { get; }
        public bool Autosave { get; }
        /// <summary>
        /// What started it: "player" for a slot the player chose; for an autosave "quest", "battle", "battleReward",
        /// "interval", "focus", "pause", "quit" or "leave".
        /// </summary>
        public string Reason { get; }
        public SaveError Error { get; }
        public string Detail { get; }
        /// <summary>The written slot as a list shows it, its picture included; null when the save failed.</summary>
        public SaveSlotInfo Slot { get; }
        /// <summary>Real time the picture, the export and the write took.</summary>
        public double Milliseconds { get; }
        public bool Ok => Error == SaveError.None;
    }

    /// <summary>The answer to a load or a check: the game ready to restore, or why it cannot be.</summary>
    public sealed class LoadResult
    {
        private LoadResult(SaveError error, string detail, SavedGame game, SaveHeader header, SaveProblem problem)
        {
            Error = error;
            Detail = detail;
            Game = game;
            Header = header;
            Problem = problem;
        }

        public SaveError Error { get; }
        public string Detail { get; }
        /// <summary>Null unless <see cref="Ok"/>.</summary>
        public SavedGame Game { get; }
        /// <summary>The header when it could be read, even if the rest could not.</summary>
        public SaveHeader Header { get; }
        /// <summary>What a slot card says about it (set by <see cref="SaveGames"/>; Damaged when read bare).</summary>
        public SaveProblem Problem { get; }
        /// <summary>The edition that made the save when the game was carried forward into this one; null otherwise.</summary>
        public string CarriedFrom => Game?.CarriedFrom;
        public bool Ok => Error == SaveError.None;

        public static LoadResult Success(SavedGame game) => new(SaveError.None, null, game, game.Header, SaveProblem.None);

        public static LoadResult Fail(SaveError error, string detail, SaveHeader header = null) =>
            new(error == SaveError.None ? SaveError.Corrupt : error, detail, null, header, SaveProblem.Damaged);

        /// <summary>The same answer with what the card should say.</summary>
        public LoadResult As(SaveProblem problem) => Ok ? this : new LoadResult(Error, Detail, null, Header, problem);

        public override string ToString() => Ok ? "ok" : $"{Error}: {Detail}";
    }
}
