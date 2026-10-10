using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// The saves of one running colony, for the menu and the autosave: list the slots (headers and pictures, not the
    /// state), find the latest, save into a slot, check or load one, delete one.
    ///
    /// Slots: one autosave (<see cref="AutosaveSlotId"/>), the game's own: the player loads it but neither writes nor
    /// deletes it; any number of player slots by id (the menu decides how many and how they are named). A write keeps
    /// the slot's save before as its backup. A new colony leaves every slot alone until the player has given it an
    /// order: only then may its autosave replace the one there, and an autosave this build cannot read (damaged,
    /// a newer build's, another edition's) is set aside first, never overwritten. Player slots change only by
    /// <see cref="Save"/> and <see cref="Delete"/>.
    ///
    /// Autosave: at a stable moment between commands and steps (the composer calls <see cref="AutosaveIfDue"/> once a
    /// frame, after the colony's step): after a quest reward or a battle reward is claimed and back in the colony after
    /// a battle, and every <see cref="AutosaveIntervalMs"/> of colony time with something changed; never while a battle
    /// runs or <see cref="HoldAutosave"/> says so. <see cref="AutosaveNow"/> is for moments the game may end (focus
    /// lost, the OS pausing it, quitting, the scene being left): it saves whatever runs, a battle being watched
    /// included, whose outcome the state already holds.
    ///
    /// Every save carries a picture of the island from <see cref="Thumbnail"/>, taken on the frame of the save; a
    /// missing or failed picture never fails the save. Loading checks the whole save first; only a game that passes
    /// goes to the composer's opener (a scene reload that restores it), so a refused save leaves the colony as it is.
    /// </summary>
    public sealed class SaveGames : IDisposable
    {
        public const string AutosaveSlotId = "autosave";
        /// <summary>Colony time between autosaves while something changes.</summary>
        public const int AutosaveIntervalMs = 60000;

        private readonly ISaveStore _store;
        private readonly Action<SavedGame> _open;
        private readonly Func<DateTime> _utcNow;
        private readonly int _intervalMs;
        private bool _armed;
        private bool _dirty;
        private bool _unsaved;
        private int _savedAtActiveMs;
        private string _due;
        private bool _disposed;

        /// <param name="loadedFrom">The header of the save this colony was loaded from; null for a new colony.</param>
        /// <param name="open">Opens a checked save in place of this colony; null where nothing can be opened (tests).</param>
        /// <param name="utcNow">The real clock the saves are stamped with.</param>
        public SaveGames(GameSession session, ISaveStore store, SaveStamp stamp, SaveHeader loadedFrom = null,
            Action<SavedGame> open = null, Func<DateTime> utcNow = null, int autosaveIntervalMs = AutosaveIntervalMs)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Stamp = stamp ?? throw new ArgumentNullException(nameof(stamp));
            ColonyId = loadedFrom != null && SaveSlots.IsValidId(loadedFrom.ColonyId) ? loadedFrom.ColonyId : NewColonyId();
            // a loaded colony is the player's game from the start; a new one once the player gives it an order
            _armed = loadedFrom != null;
            LastSavedAtUtc = loadedFrom?.SavedAtUtc;
            _open = open;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _intervalMs = Math.Max(1000, autosaveIntervalMs);
            _savedAtActiveMs = session.ActiveTimeMs;
            Session.OnSnapshotChanged += OnChanged;
            Session.OnCommandResolved += OnCommand;
        }

        public GameSession Session { get; }
        /// <summary>The running build: its version label for "saved by v1.0.61, you have v1.0.58", its edition.</summary>
        public SaveStamp Stamp { get; }
        /// <summary>This colony's id from its start, kept through saves and loads.</summary>
        public string ColonyId { get; }
        /// <summary>The picture each save carries: a PNG of the island taken now, or null. Set by the composer.</summary>
        public Func<byte[]> Thumbnail { get; set; }
        /// <summary>Holds timed and event autosaves while true (the composer: a battle screen is open).</summary>
        public Func<bool> HoldAutosave { get; set; }
        /// <summary>
        /// The autosave may write: a loaded colony from the start, a new one once the player's first order was
        /// accepted. Until then the autosave slot keeps the game it had.
        /// </summary>
        public bool AutosaveArmed => _armed;
        /// <summary>The colony changed since its last autosave.</summary>
        public bool Dirty => _dirty;
        /// <summary>The colony changed since it was last saved into any slot (or loaded).</summary>
        public bool HasUnsavedChanges => _unsaved;
        /// <summary>
        /// Real time this colony was last saved into any slot, or the time of the save it was loaded from; null for
        /// a new colony not saved yet. "What is not saved since 14:32 will be lost."
        /// </summary>
        public DateTime? LastSavedAtUtc { get; private set; }
        /// <summary>What the next <see cref="AutosaveIfDue"/> would save for; null when nothing is due.</summary>
        public string AutosaveDue =>
            !_armed ? null : _due ?? (_dirty && Session.ActiveTimeMs - _savedAtActiveMs >= _intervalMs ? "interval" : null);
        /// <summary>The latest write this colony made, a failed one included; null before the first.</summary>
        public SaveResult LastSave { get; private set; }
        /// <summary>Every write's result, ok or failed: a "saved" mark, a warning.</summary>
        public event Action<SaveResult> Saved;

        /// <summary>A fresh colony id: twelve hex digits.</summary>
        public static string NewColonyId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        public static bool IsAutosave(string slotId) => slotId == AutosaveSlotId;

        /// <summary>
        /// Every slot, newest first: header and picture, or why it cannot be loaded (a damaged slot lists too). Reads
        /// first lines and pictures only, never the state.
        /// </summary>
        public IReadOnlyList<SaveSlotInfo> List() => List(true);

        /// <summary>The newest slot this build can load ("Continue"); null when there is none.</summary>
        public SaveSlotInfo Latest()
        {
            foreach (var slot in List(true))
                if (slot.Loadable) return slot;
            return null;
        }

        /// <summary>
        /// One slot as a list shows it, its picture included. A slot whose save is missing beside its backup (a write
        /// cut short) shows the backup.
        /// </summary>
        public SaveSlotInfo Describe(string slotId, bool backup = false) => Describe(slotId, backup, true);

        /// <summary>A player slot id no slot has yet: "save-&lt;UTC date and time&gt;".</summary>
        public string NewSlotId()
        {
            string id = "save-" + _utcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string candidate = id;
            for (int n = 2; _store.Has(candidate) || _store.Has(candidate, true); n++) candidate = $"{id}-{n}";
            return candidate;
        }

        /// <summary>
        /// Saves the colony into a player slot, empty or not (the save there becomes the slot's backup). The autosave
        /// slot is refused (<see cref="SaveError.Reserved"/>), as is an id that is not a slot id.
        /// </summary>
        public SaveResult Save(string slotId, string label = null)
        {
            if (IsAutosave(slotId))
                return Report(new SaveResult(slotId, false, "player", SaveError.Reserved, "the autosave is the game's", null, 0));
            if (!SaveSlots.IsValidId(slotId))
                return Report(new SaveResult(slotId, false, "player", SaveError.InvalidSlotId, "not a slot id", null, 0));
            return Write(slotId, false, "player", label);
        }

        /// <summary>Saves the colony into a new player slot.</summary>
        public SaveResult SaveNew(string label = null) => Save(NewSlotId(), label);

        /// <summary>The autosave when one is due and nothing holds it; null when there was nothing to do.</summary>
        public SaveResult AutosaveIfDue()
        {
            if (_disposed || Session.ActiveBattle != null || (HoldAutosave?.Invoke() ?? false)) return null;
            string reason = AutosaveDue;
            return reason != null ? Autosave(reason) : null;
        }

        /// <summary>
        /// The autosave now, if the colony changed since the last one and the player has played it: for the moments
        /// the game may end. Runs during a battle too (the save opens in the colony after it).
        /// </summary>
        public SaveResult AutosaveNow(string reason)
        {
            if (_disposed || !_armed || !_dirty) return null;
            return Autosave(reason ?? "now");
        }

        /// <summary>Reads and checks a slot as a load would, without opening it.</summary>
        public LoadResult Check(string slotId, bool backup = false)
        {
            if (!SaveSlots.IsValidId(slotId)) return LoadResult.Fail(SaveError.InvalidSlotId, slotId);
            bool fromBackup = backup || (!_store.Has(slotId) && _store.Has(slotId, true));
            var io = _store.Read(slotId, fromBackup);
            if (!io.Ok) return LoadResult.Fail(io.Error, io.Detail).As(io.Error == SaveError.NotFound ? SaveProblem.None : SaveProblem.Damaged);
            var result = SaveCodec.Read(io.Bytes, Session.Catalog, Stamp);
            if (result.Ok) return LoadResult.Success(result.Game.InSlot(slotId));
            string writtenBy = result.Header?.Build ?? WrittenBy(io.Bytes);
            return result.As(Stamp.ProblemOf(result.Error, writtenBy));
        }

        /// <summary>
        /// Checks a slot and, if it passes, opens it in place of this colony (the composer reloads the scene with it).
        /// A refused save opens nothing; the result says why and what the card should say.
        /// </summary>
        public LoadResult Load(string slotId, bool backup = false)
        {
            var result = Check(slotId, backup);
            if (result.Ok && !_disposed) _open?.Invoke(result.Game);
            return result;
        }

        /// <summary>
        /// Removes a player slot's save and its backup. False for the autosave (the game's own), an id that is not a
        /// slot id, or a slot with nothing in it.
        /// </summary>
        public bool Delete(string slotId) => !IsAutosave(slotId) && _store.Delete(slotId);

        /// <summary>
        /// The colony at a glance as a save written now would carry it, for the "will be" card of an overwrite;
        /// writes nothing. Copies the state once.
        /// </summary>
        public SaveSummary Preview() => SaveCodec.Summarize(Session.Export().State, Session.Catalog);

        /// <summary>The picture a save written now would carry (a PNG of about 320×180); null when there is none, never throws.</summary>
        public byte[] PictureNow() => Picture();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Session.OnSnapshotChanged -= OnChanged;
            Session.OnCommandResolved -= OnCommand;
        }

        private IReadOnlyList<SaveSlotInfo> List(bool pictures)
        {
            var slots = new List<SaveSlotInfo>();
            foreach (string slotId in _store.Slots()) slots.Add(Describe(slotId, false, pictures));
            slots.Sort((a, b) =>
            {
                int byTime = b.SavedAtUtc.CompareTo(a.SavedAtUtc);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.SlotId, b.SlotId);
            });
            return slots;
        }

        private SaveSlotInfo Describe(string slotId, bool backup, bool picture)
        {
            bool isAuto = IsAutosave(slotId);
            if (!SaveSlots.IsValidId(slotId))
                return new SaveSlotInfo(slotId, isAuto, SaveError.InvalidSlotId, slotId, null, backup, false, default);
            bool hasBackup = _store.Has(slotId, true);
            bool fromBackup = backup || (!_store.Has(slotId) && hasBackup);
            var io = _store.ReadHead(slotId, fromBackup, SaveCodec.MaxHeaderBytes);
            if (!io.Ok)
                return new SaveSlotInfo(slotId, isAuto, io.Error, io.Detail, null, fromBackup, hasBackup, default,
                    io.Error == SaveError.NotFound ? SaveProblem.None : SaveProblem.Damaged);
            if (!io.LineEnded)
                return new SaveSlotInfo(slotId, isAuto, SaveError.Corrupt, "no header line", null, fromBackup, hasBackup,
                    io.ModifiedUtc, SaveProblem.Damaged);
            var (header, error, detail, envelope) = SaveCodec.ReadHeader(io.Bytes, io.Length, Stamp);
            string writtenBy = header?.Build ?? envelope?.Build;
            byte[] thumbnail = null;
            // a newer build's save still shows its picture (pale, behind a lock); a damaged one shows none
            if (picture && envelope.HasValue && envelope.Value.ThumbnailBytes > 0 &&
                (error == SaveError.None || error == SaveError.NewerVersion || error == SaveError.OtherEdition))
            {
                long offset = io.Bytes.Length + 1L + envelope.Value.BodyBytes;
                var pictureIo = _store.ReadRange(slotId, fromBackup, offset, envelope.Value.ThumbnailBytes);
                if (pictureIo.Ok && Crc32.Of(pictureIo.Bytes) == envelope.Value.ThumbnailCrc) thumbnail = pictureIo.Bytes;
            }
            return new SaveSlotInfo(slotId, isAuto, error, detail, header, fromBackup, hasBackup, io.ModifiedUtc,
                Stamp.ProblemOf(error, writtenBy), writtenBy, thumbnail, SaveCodec.CarriesFrom(header, Stamp));
        }

        private SaveResult Autosave(string reason)
        {
            // an autosave this build cannot read is kept, out of the slot, rather than replaced
            if (_store.Has(AutosaveSlotId))
            {
                var there = Describe(AutosaveSlotId, false, false);
                if (!there.Loadable && !_store.SetAside(AutosaveSlotId,
                        _utcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)))
                {
                    _due = null;
                    _savedAtActiveMs = Session.ActiveTimeMs;
                    return Report(new SaveResult(AutosaveSlotId, true, reason, SaveError.WriteFailed,
                        $"the autosave there ({there.Status}) could not be set aside", null, 0));
                }
            }
            var result = Write(AutosaveSlotId, true, reason, null);
            // a failed write waits for the next interval rather than retrying every frame
            _due = null;
            _savedAtActiveMs = Session.ActiveTimeMs;
            if (result.Ok) _dirty = false;
            return result;
        }

        private SaveResult Write(string slotId, bool autosave, string reason, string label)
        {
            var watch = Stopwatch.StartNew();
            SaveResult result;
            try
            {
                byte[] thumbnail = Picture();
                var game = Session.Export();
                DateTime savedAt = _utcNow();
                byte[] document = SaveCodec.Write(game, Session.Catalog, Stamp, savedAt, ColonyId, label, thumbnail);
                var io = _store.Write(slotId, document);
                if (!io.Ok) result = new SaveResult(slotId, autosave, reason, io.Error, io.Detail, null, Elapsed(watch));
                else
                {
                    int newline = Array.IndexOf(document, (byte)'\n');
                    var head = new byte[newline];
                    Buffer.BlockCopy(document, 0, head, 0, newline);
                    var (header, error, detail, _) = SaveCodec.ReadHeader(head, document.Length, Stamp);
                    var slot = new SaveSlotInfo(slotId, autosave, error, detail, header, false, _store.Has(slotId, true),
                        io.ModifiedUtc, SaveProblem.None, Stamp.Build, thumbnail);
                    result = new SaveResult(slotId, autosave, reason, SaveError.None, null, slot, Elapsed(watch));
                    LastSavedAtUtc = savedAt;
                    _unsaved = false;
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                // a save must never take the game down with it (a quit, a lost focus): the result carries the fault
                result = new SaveResult(slotId, autosave, reason, SaveError.WriteFailed,
                    $"{exception.GetType().Name}: {exception.Message}", null, Elapsed(watch));
            }
            return Report(result);
        }

        // the island now; a picture that cannot be taken leaves the save without one
        private byte[] Picture()
        {
            try { return Thumbnail?.Invoke(); }
            catch (Exception exception) when (!(exception is OutOfMemoryException)) { return null; }
        }

        private SaveResult Report(SaveResult result)
        {
            LastSave = result;
            Saved?.Invoke(result);
            return result;
        }

        // the writer's version label from the envelope, for a save whose header could not be read whole
        private static string WrittenBy(byte[] document)
        {
            int newline = Array.IndexOf(document, (byte)'\n');
            if (newline <= 0) return null;
            var head = new byte[newline];
            Buffer.BlockCopy(document, 0, head, 0, newline);
            return SaveCodec.ReadHeader(head, document.Length).Envelope?.Build;
        }

        private void OnChanged(GameSnapshot snapshot)
        {
            _dirty = true;
            _unsaved = true;
        }

        private void OnCommand(IGameCommand command, CommandResult result)
        {
            if (!result.Ok) return;
            _armed = true;
            switch (command)
            {
                case ClaimQuestRewardCommand:
                    _due = "quest";
                    break;
                case ClaimBattleRewardCommand:
                    _due = "battleReward";
                    break;
                case AcknowledgeBattleCommand:
                    _due = "battle";
                    break;
            }
        }

        private static double Elapsed(Stopwatch watch) => watch.Elapsed.TotalMilliseconds;
    }
}
