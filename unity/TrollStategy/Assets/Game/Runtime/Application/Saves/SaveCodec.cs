using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Application
{
    /// <summary>
    /// The save file format. A save is a UTF-8 header line, a UTF-8 body line and, last, a picture of the island.
    /// The header is a JSON object: the format's name and version, when and by which build and edition it was
    /// written, the colony it belongs to, the colony at a glance (<see cref="SaveSummary"/>), and the length in bytes
    /// and CRC-32 of the body and of the picture. A slot list reads only this line (and the picture, by its offset)
    /// and checks the file's length against it, so a file cut short shows as corrupt without reading the state. The
    /// body is the whole <see cref="GameState"/> and the session's step clock; the picture a PNG of about 320×180.
    /// One file holds all three, so a save never shows another save's picture.
    ///
    /// Content goes by name, never by enum number: building, creature and good kinds by their enum names, missions,
    /// items, upgrades and quests by their ids. Reading checks everything against this build's content and the
    /// colony's own rules and refuses (<see cref="SaveError"/>, with the place in the file) rather than repairing:
    /// unknown names, dangling or duplicate ids, gear in a taken slot, a broken layout, a land grid or a quest this
    /// build does not have, another edition's game. A newer version is refused; an older one goes through
    /// <see cref="Migrations"/> first.
    ///
    /// Not saved: a battle being watched (its outcome is already in the state when it starts, so a save made then
    /// opens in the colony after it), the session's caches and revision, selection and other screen state.
    /// </summary>
    public static class SaveCodec
    {
        public const string Format = "troll-strategy-save";
        /// <summary>The version this build writes.</summary>
        public const int Version = 1;
        /// <summary>The oldest version this build still reads.</summary>
        public const int OldestVersion = 1;
        /// <summary>A header line longer than this is not a header.</summary>
        public const int MaxHeaderBytes = 64 * 1024;

        private static readonly UTF8Encoding Utf8 = new(false, true);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>
        /// Upgrades from each older version to the next: the step at key v turns a version-v header and body into
        /// version v + 1 in place, or returns why it cannot. The body is null when a slot list reads the header alone.
        /// The envelope (format, version, body length and checksum) never moves. Version 1 is the first format, so
        /// there is no step yet; a change to the format raises <see cref="Version"/> and adds the step from the
        /// version before.
        /// </summary>
        private static readonly Dictionary<int, Func<JsonObject, JsonObject, string>> Migrations = new();

        /// <summary>
        /// The game as a save file. <paramref name="savedAtUtc"/> is the real time of the save (the store's clock);
        /// <paramref name="label"/> the player's name for it, or null.
        /// </summary>
        public static byte[] Write(SavedGame game, GameContentCatalog catalog, SaveStamp stamp, DateTime savedAtUtc,
            string colonyId, string label = null, byte[] thumbnail = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (stamp == null) throw new ArgumentNullException(nameof(stamp));
            string body = WriteBody(game.State, game.StepRemainderSeconds, catalog);
            var header = new JsonObject()
                .Set("format", new JsonString(Format))
                .Set("version", Number(Version))
                .Set("savedAt", new JsonString(savedAtUtc.ToUniversalTime().ToString("o", Invariant)))
                .Set("build", new JsonString(stamp.Build))
                .Set("edition", new JsonString(stamp.Edition))
                .Set("colony", Text(colonyId))
                .Set("label", Text(label))
                .Set("summary", WriteSummary(Summarize(game.State, catalog)));
            return Pack(header, body, thumbnail);
        }

        /// <summary>
        /// The body line for a state and step clock. The same game always gives the same text: lists keep their order
        /// (the simulation's order), dictionaries and sets go sorted. Two sessions are the same game when their bodies
        /// match.
        /// </summary>
        public static string WriteBody(GameState state, float stepRemainderSeconds, GameContentCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var writer = new JsonWriter(16 * 1024 + state.Units.Count * 512);
            writer.BeginObject();
            writer.Name("state");
            SaveBody.WriteState(writer, state, catalog);
            writer.Name("session").BeginObject().Name("stepRemainder").Value(stepRemainderSeconds).EndObject();
            writer.EndObject();
            return writer.ToString();
        }

        /// <summary>The colony at a glance, for the header.</summary>
        public static SaveSummary Summarize(GameState state, GameContentCatalog catalog)
        {
            var progress = state.Progress;
            var quest = progress != null ? Progression.CurrentQuest(state, catalog) : null;
            int owned = 0;
            if (state.Land != null)
                for (int y = 0; y < state.Land.BlocksPerSide; y++)
                for (int x = 0; x < state.Land.BlocksPerSide; x++)
                    if (state.Land.IsOwned(x, y)) owned++;
            return new SaveSummary(progress != null, state.ActiveTimeMs, progress != null ? progress.QuestIndex + 1 : 0,
                QuestTotal(state, catalog), quest?.Id, quest?.Title, state.Gold, state.Units.Count,
                state.Buildings.Count, state.BattlesWon, state.HighestMissionLevel, owned,
                progress != null && quest == null, progress?.ChainLength ?? 0);
        }

        // quests in the chain this game plays: a short build's own length, else the whole authored chain
        private static int QuestTotal(GameState state, GameContentCatalog catalog) =>
            state.Progress == null ? 0
            : state.Progress.ChainLength > 0 ? state.Progress.ChainLength
            : catalog.Progression != null ? catalog.Progression.Quests.Count : 0;

        /// <summary>
        /// Puts a header, a body and a picture (or none) together, writing their lengths and CRC-32s into the header.
        /// </summary>
        public static byte[] Pack(JsonObject header, string body, byte[] thumbnail = null)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            byte[] bodyBytes = Utf8.GetBytes(body ?? string.Empty);
            header.Set("body", Part(bodyBytes));
            header.Set("thumbnail", thumbnail != null && thumbnail.Length > 0 ? Part(thumbnail) : JsonNode.Null);
            byte[] head = Utf8.GetBytes(header.ToString());
            int pictureBytes = thumbnail?.Length ?? 0;
            var document = new byte[head.Length + 1 + bodyBytes.Length + pictureBytes];
            Buffer.BlockCopy(head, 0, document, 0, head.Length);
            document[head.Length] = (byte)'\n';
            Buffer.BlockCopy(bodyBytes, 0, document, head.Length + 1, bodyBytes.Length);
            if (pictureBytes > 0) Buffer.BlockCopy(thumbnail, 0, document, head.Length + 1 + bodyBytes.Length, pictureBytes);
            return document;
        }

        private static JsonObject Part(byte[] bytes) => new JsonObject()
            .Set("bytes", Number(bytes.Length))
            .Set("crc32", new JsonNumber(Crc32.Of(bytes).ToString(Invariant)));

        /// <summary>The picture of a whole save file; null when it has none or the picture is damaged.</summary>
        public static byte[] Thumbnail(byte[] document)
        {
            int newline = document != null ? Array.IndexOf(document, (byte)'\n') : -1;
            if (newline <= 0) return null;
            try
            {
                var envelope = ReadEnvelope(ParseObject(document, 0, newline, "header"));
                long at = newline + 1L + envelope.Info.BodyBytes;
                return Picture(document, at, envelope.Info);
            }
            catch (SaveFormatException) { return null; }
            catch (SaveRejection) { return null; }
        }

        /// <summary>The picture at <paramref name="offset"/> of <paramref name="bytes"/> when it matches its checksum.</summary>
        public static byte[] Picture(byte[] bytes, long offset, SaveEnvelope envelope)
        {
            if (bytes == null || envelope.ThumbnailBytes <= 0 || offset < 0 ||
                offset + envelope.ThumbnailBytes > bytes.Length) return null;
            var picture = new byte[envelope.ThumbnailBytes];
            Buffer.BlockCopy(bytes, (int)offset, picture, 0, picture.Length);
            return Crc32.Of(picture) == envelope.ThumbnailCrc ? picture : null;
        }

        /// <summary>The header and the body as JSON, unchecked: for tools and tests that take a save apart.</summary>
        public static (JsonObject Header, JsonObject Body) Open(byte[] document)
        {
            int newline = document != null ? Array.IndexOf(document, (byte)'\n') : -1;
            if (newline < 0) throw new SaveFormatException("no header line");
            var header = ParseObject(document, 0, newline, "header");
            var envelope = ReadEnvelope(header);
            int bodyBytes = envelope.Error == SaveError.UnknownFormat
                ? document.Length - newline - 1
                : Math.Min(envelope.Info.BodyBytes, document.Length - newline - 1);
            return (header, ParseObject(document, newline + 1, bodyBytes, "body"));
        }

        /// <summary>
        /// Reads a slot's first line (without its newline). <paramref name="fileLength"/> is the whole file's, checked
        /// against the lengths the header gives its body and picture; <paramref name="current"/>, when given, is the
        /// running build, whose edition the save must share. The header comes back whenever it parsed, even with a
        /// fault; the envelope whenever it did (a newer format's too), null when the line is no save header at all.
        /// </summary>
        public static (SaveHeader Header, SaveError Error, string Detail, SaveEnvelope? Envelope) ReadHeader(
            byte[] headLine, long fileLength, SaveStamp current = null)
        {
            if (headLine == null || headLine.Length == 0) return (null, SaveError.Corrupt, "empty file", null);
            if (headLine.Length > MaxHeaderBytes) return (null, SaveError.Corrupt, "no header line", null);
            SaveHeader header = null;
            SaveEnvelope? info = null;
            try
            {
                var json = ParseObject(headLine, 0, headLine.Length, "header");
                var envelope = ReadEnvelope(json);
                if (envelope.Error == SaveError.UnknownFormat) return (null, envelope.Error, envelope.Detail, null);
                info = envelope.Info;
                if (envelope.Error != SaveError.None) return (TryHeader(json), envelope.Error, envelope.Detail, info);
                if (envelope.Info.Version < Version)
                {
                    string refused = Migrate(json, null, envelope.Info.Version);
                    if (refused != null) return (null, SaveError.UnsupportedVersion, refused, info);
                }
                header = ReadHeaderFields(json);
                long expected = headLine.Length + 1L + envelope.Info.BodyBytes + envelope.Info.ThumbnailBytes;
                if (fileLength != expected)
                    return (header, SaveError.Corrupt,
                        fileLength < expected
                            ? $"cut short: {fileLength} of {expected} bytes"
                            : $"{fileLength - expected} bytes too long", info);
                var edition = CheckEdition(header, current);
                return (header, edition.Error, edition.Detail, info);
            }
            catch (SaveFormatException exception)
            {
                return (header, SaveError.Corrupt, exception.Message, info);
            }
            catch (SaveRejection rejection)
            {
                return (header, rejection.Error, rejection.Message, info);
            }
        }

        /// <summary>
        /// Reads a whole save: header, checksum, migrations, the state against <paramref name="catalog"/> and the
        /// colony's rules. Never throws for a bad file; the result says what is wrong and where.
        /// </summary>
        public static LoadResult Read(byte[] document, GameContentCatalog catalog, SaveStamp current = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (document == null || document.Length == 0) return LoadResult.Fail(SaveError.Corrupt, "empty file");
            int newline = Array.IndexOf(document, (byte)'\n');
            if (newline <= 0 || newline > MaxHeaderBytes) return LoadResult.Fail(SaveError.Corrupt, "no header line");

            SaveHeader header = null;
            try
            {
                var headerJson = ParseObject(document, 0, newline, "header");
                var envelope = ReadEnvelope(headerJson);
                if (envelope.Error != SaveError.None)
                    return LoadResult.Fail(envelope.Error, envelope.Detail, TryHeader(headerJson));

                var info = envelope.Info;
                long expected = newline + 1L + info.BodyBytes + info.ThumbnailBytes;
                if (document.Length != expected)
                    return LoadResult.Fail(SaveError.Corrupt,
                        document.Length < expected
                            ? $"cut short: {document.Length} of {expected} bytes"
                            : $"{document.Length - expected} bytes too long", TryHeader(headerJson));
                int bodyLength = info.BodyBytes;
                if (Crc32.Of(document, newline + 1, bodyLength) != info.BodyCrc)
                    return LoadResult.Fail(SaveError.Corrupt, "the body does not match its checksum",
                        TryHeader(headerJson));

                var body = ParseObject(document, newline + 1, bodyLength, "body");
                if (info.Version < Version)
                {
                    string refused = Migrate(headerJson, body, info.Version);
                    if (refused != null) return LoadResult.Fail(SaveError.UnsupportedVersion, refused);
                }
                header = ReadHeaderFields(headerJson);
                var edition = CheckEdition(header, current);
                if (edition.Error != SaveError.None) return LoadResult.Fail(edition.Error, edition.Detail, header);

                var state = SaveBody.ReadState(SaveBody.Obj(body, "state", "body"), catalog);
                var session = SaveBody.Obj(body, "session", "body");
                float remainder = SaveBody.Float(session, "stepRemainder", "session");
                if (float.IsNaN(remainder) || float.IsInfinity(remainder) || remainder < 0f ||
                    remainder >= Math.Max(catalog.Economy.EconomyStepSeconds, 0f) + 1e-3f)
                    throw new SaveRejection(SaveError.InvalidValue, $"session.stepRemainder: {remainder}");
                // the upgrade step for another edition's game, decided from the state itself
                string carriedFrom = null;
                bool extended = false;
                if (current != null && !string.Equals(header.Edition, current.Edition, StringComparison.Ordinal))
                {
                    if (!CarryForward(state, current, catalog, out extended))
                        throw new SaveRejection(SaveError.OtherEdition,
                            $"made by the {header.Edition} edition (chain {state.Progress?.ChainLength}); this is {current.Edition} (chain {current.ChainLength})");
                    carriedFrom = header.Edition;
                }
                SaveCheck.Run(state, catalog);
                return LoadResult.Success(new SavedGame(header, state, remainder, carriedFrom, extended));
            }
            catch (SaveRejection rejection)
            {
                return LoadResult.Fail(rejection.Error, rejection.Message, header);
            }
            catch (SaveFormatException exception)
            {
                return LoadResult.Fail(SaveError.Corrupt, exception.Message, header);
            }
        }

        // The envelope every version keeps: the format's name and version, the writer's build, and the lengths and
        // checksums of the body and the picture.
        private static (SaveEnvelope Info, SaveError Error, string Detail) ReadEnvelope(JsonObject json)
        {
            if (!(json["format"] is JsonString format) || format.Value != Format)
                return (default, SaveError.UnknownFormat, "not a Troll Strategy save");
            int version = SaveBody.Int(json, "version", "header");
            string build = json["build"] is JsonString writer ? writer.Value : null;
            var body = SaveBody.Obj(json, "body", "header");
            int bytes = SaveBody.Int(body, "bytes", "header.body", 0);
            uint crc = SaveBody.UInt(body, "crc32", "header.body");
            int pictureBytes = 0;
            uint pictureCrc = 0;
            if (json["thumbnail"] is JsonObject picture)
            {
                pictureBytes = SaveBody.Int(picture, "bytes", "header.thumbnail", 0);
                pictureCrc = SaveBody.UInt(picture, "crc32", "header.thumbnail");
            }
            var info = new SaveEnvelope(version, build, bytes, crc, pictureBytes, pictureCrc);
            if (version > Version)
                return (info, SaveError.NewerVersion, $"format version {version}; this build reads up to {Version}");
            if (version < OldestVersion)
                return (info, SaveError.UnsupportedVersion,
                    $"format version {version}; this build reads from {OldestVersion}");
            return (info, SaveError.None, null);
        }

        // what a header of another version still tells, for the slot card of a save this build cannot load
        private static SaveHeader TryHeader(JsonObject json)
        {
            try { return ReadHeaderFields(json); }
            catch (SaveFormatException) { return null; }
            catch (SaveRejection) { return null; }
        }

        private static SaveHeader ReadHeaderFields(JsonObject json)
        {
            var summaryJson = SaveBody.Obj(json, "summary", "header");
            var bodyJson = SaveBody.Obj(json, "body", "header");
            string savedAtText = SaveBody.Str(json, "savedAt", "header");
            if (!DateTime.TryParse(savedAtText, Invariant, DateTimeStyles.RoundtripKind, out var savedAt))
                throw new SaveFormatException($"header.savedAt: not a time: {savedAtText}");
            var summary = new SaveSummary(
                SaveBody.Bool(summaryJson, "campaign", "summary"),
                SaveBody.Int(summaryJson, "activeMs", "summary"),
                SaveBody.Int(summaryJson, "questLevel", "summary"),
                SaveBody.Int(summaryJson, "questTotal", "summary"),
                SaveBody.Str(summaryJson, "questId", "summary", true),
                SaveBody.Str(summaryJson, "questTitle", "summary", true),
                SaveBody.Int(summaryJson, "gold", "summary"),
                SaveBody.Int(summaryJson, "creatures", "summary"),
                SaveBody.Int(summaryJson, "buildings", "summary"),
                SaveBody.Int(summaryJson, "battlesWon", "summary"),
                SaveBody.Int(summaryJson, "arenaLevel", "summary"),
                SaveBody.Int(summaryJson, "land", "summary"),
                SaveBody.Bool(summaryJson, "finished", "summary"),
                // written since the editions carry forward; -1 in files from before
                summaryJson.Has("chainLength") ? SaveBody.Int(summaryJson, "chainLength", "summary", 0) : -1);
            return new SaveHeader(SaveBody.Int(json, "version", "header"), savedAt.ToUniversalTime(),
                SaveBody.Str(json, "build", "header"), SaveBody.Str(json, "edition", "header"),
                SaveBody.Str(json, "colony", "header", true), SaveBody.Str(json, "label", "header", true), summary,
                SaveBody.Int(bodyJson, "bytes", "header.body", 0), SaveBody.UInt(bodyJson, "crc32", "header.body"),
                json["thumbnail"] is JsonObject picture ? SaveBody.Int(picture, "bytes", "header.thumbnail", 0) : 0,
                json["thumbnail"] is JsonObject crc ? SaveBody.UInt(crc, "crc32", "header.thumbnail") : 0);
        }

        // A save opens in its own edition as it is, in an edition with a chain at least as long carried forward, and
        // in a shorter one not at all.
        private static (SaveError Error, string Detail) CheckEdition(SaveHeader header, SaveStamp current) =>
            current == null || string.Equals(header.Edition, current.Edition, StringComparison.Ordinal) ||
            CarriesFrom(header, current) != null
                ? (SaveError.None, null)
                : (SaveError.OtherEdition,
                    $"made by the {header.Edition} edition (chain {header.Summary.ChainLength}); this is {current.Edition} (chain {current.ChainLength})");

        /// <summary>
        /// The edition a save comes from when <paramref name="current"/> opens it by carrying it forward (another,
        /// shorter edition: <see cref="SaveStamp.CanCarry"/>); null for its own edition's saves and for those it
        /// cannot open. Read from the header alone; the load decides again from the state.
        /// </summary>
        public static string CarriesFrom(SaveHeader header, SaveStamp current)
        {
            if (header == null || current == null || string.Equals(header.Edition, current.Edition, StringComparison.Ordinal))
                return null;
            var summary = header.Summary;
            // a sandbox game has no chain to end, and the whole chain takes any shorter one
            if (!summary.Campaign || current.ChainLength == 0) return header.Edition;
            return summary.ChainLength >= 0 && current.CanCarry(summary.ChainLength) ? header.Edition : null;
        }

        /// <summary>
        /// Carries a game made by a shorter edition into <paramref name="current"/>'s (the alpha into the demo or the
        /// full game, the demo into the full game): its chain becomes this build's and the arena opens; a game whose
        /// short chain had ended begins the quest after it, as a claim begins the next quest. False, changing nothing,
        /// when this build's chain is shorter: the game would lose quests it has.
        /// </summary>
        public static bool CarryForward(GameState state, SaveStamp current, GameContentCatalog catalog, out bool extended)
        {
            extended = false;
            var progress = state?.Progress;
            if (progress == null) return true;
            if (current == null || !current.CanCarry(progress.ChainLength)) return false;
            bool wasOver = Progression.IsOver(state);
            progress.ChainLength = current.ChainLength;
            // a chain that ends where this build's ends too stays ended, its arena cap kept
            if (Progression.IsOver(state)) return true;
            progress.ArenaCap = 0;
            if (!wasOver) return true;
            Progression.Update(state, catalog);
            extended = true;
            return true;
        }

        private static string Migrate(JsonObject header, JsonObject body, int from)
        {
            for (int version = from; version < Version; version++)
            {
                if (!Migrations.TryGetValue(version, out var step))
                    return $"no migration from format version {version}";
                string refused = step(header, body);
                if (refused != null) return $"migration from version {version}: {refused}";
            }
            return null;
        }

        private static JsonObject WriteSummary(SaveSummary summary) => new JsonObject()
            .Set("campaign", new JsonBool(summary.Campaign))
            .Set("activeMs", Number(summary.ActiveTimeMs))
            .Set("questLevel", Number(summary.QuestLevel))
            .Set("questTotal", Number(summary.QuestTotal))
            .Set("questId", Text(summary.QuestId))
            .Set("questTitle", Text(summary.QuestTitle))
            .Set("gold", Number(summary.Gold))
            .Set("creatures", Number(summary.Creatures))
            .Set("buildings", Number(summary.Buildings))
            .Set("battlesWon", Number(summary.BattlesWon))
            .Set("arenaLevel", Number(summary.HighestArenaLevel))
            .Set("land", Number(summary.LandOwned))
            .Set("finished", new JsonBool(summary.Finished))
            .Set("chainLength", Number(summary.ChainLength));

        private static JsonObject ParseObject(byte[] bytes, int offset, int count, string what)
        {
            string text;
            try { text = Utf8.GetString(bytes, offset, count); }
            catch (ArgumentException) { throw new SaveFormatException($"{what}: not UTF-8 text"); }
            JsonNode node;
            try { node = JsonParser.Parse(text); }
            catch (SaveFormatException exception) { throw new SaveFormatException($"{what}: {exception.Message}"); }
            return node as JsonObject ?? throw new SaveFormatException($"{what}: not a JSON object");
        }

        private static JsonNode Number(int value) => new JsonNumber(value.ToString(Invariant));
        private static JsonNode Text(string value) => value == null ? JsonNode.Null : new JsonString(value);
    }

    /// <summary>A save refused for a reason the reader names; caught inside <see cref="SaveCodec"/>, never thrown out.</summary>
    internal sealed class SaveRejection : Exception
    {
        public SaveRejection(SaveError error, string detail) : base(detail) => Error = error;
        public SaveError Error { get; }
    }

    /// <summary>CRC-32 (IEEE 802.3), the checksum of a save's body.</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Of(byte[] bytes) => Of(bytes, 0, bytes.Length);

        public static uint Of(byte[] bytes, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++) crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
