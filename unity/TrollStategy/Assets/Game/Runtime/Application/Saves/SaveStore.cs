using System;
using System.Collections.Generic;
using System.IO;

namespace TrollStrategy.Application
{
    /// <summary>What a store read or wrote: the bytes, the file's length and time, or why it could not.</summary>
    public readonly struct SaveIo
    {
        public SaveIo(byte[] bytes, long length, DateTime modifiedUtc, bool lineEnded)
        {
            Bytes = bytes;
            Length = length;
            ModifiedUtc = modifiedUtc;
            LineEnded = lineEnded;
            Error = SaveError.None;
            Detail = null;
        }

        public SaveIo(SaveError error, string detail)
        {
            Bytes = null;
            Length = 0;
            ModifiedUtc = default;
            LineEnded = false;
            Error = error;
            Detail = detail;
        }

        public byte[] Bytes { get; }
        /// <summary>The whole file's length in bytes.</summary>
        public long Length { get; }
        public DateTime ModifiedUtc { get; }
        /// <summary>For a first line: a newline ended it (a file without one has no body).</summary>
        public bool LineEnded { get; }
        public SaveError Error { get; }
        public string Detail { get; }
        public bool Ok => Error == SaveError.None;
    }

    /// <summary>
    /// Where saves live, by slot id. A slot holds its latest save and, once written twice, the save before as its
    /// backup. The store keeps bytes and knows no format; <see cref="SaveGames"/> reads them.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>Every slot with a save or a backup, in no order.</summary>
        IReadOnlyList<string> Slots();
        bool Has(string slotId, bool backup = false);
        /// <summary>The file's first line without its newline (at most <paramref name="maxBytes"/>) and its length.</summary>
        SaveIo ReadHead(string slotId, bool backup, int maxBytes);
        SaveIo Read(string slotId, bool backup = false);
        /// <summary><paramref name="count"/> bytes from <paramref name="offset"/> of the file (a save's picture).</summary>
        SaveIo ReadRange(string slotId, bool backup, long offset, int count);
        /// <summary>Writes the whole document or nothing; the slot's save before becomes its backup.</summary>
        SaveIo Write(string slotId, byte[] document);
        /// <summary>Removes the slot's save and its backup; false when there was nothing or it could not.</summary>
        bool Delete(string slotId);
        /// <summary>
        /// Moves the slot's save out of the slots, where nothing overwrites it (a file this build cannot read, kept
        /// for a newer build, the player or support); false when it could not.
        /// </summary>
        bool SetAside(string slotId, string tag);
    }

    public static class SaveSlots
    {
        /// <summary>1–64 of a–z, 0–9, '-' and '_', starting with a letter or a digit: a file name on every platform.</summary>
        public static bool IsValidId(string slotId)
        {
            if (string.IsNullOrEmpty(slotId) || slotId.Length > 64) return false;
            for (int i = 0; i < slotId.Length; i++)
            {
                char c = slotId[i];
                bool plain = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!plain && (i == 0 || (c != '-' && c != '_'))) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Saves as files in one folder (Application.persistentDataPath/Saves in the game): "&lt;slot&gt;.save" and its
    /// backup "&lt;slot&gt;.save.bak". A write goes to "&lt;slot&gt;.save.tmp" and is flushed to the disk, then the old save
    /// becomes the backup and the new one takes its name, so a crash at any moment leaves either the old save, or the
    /// backup alone (which a list then shows), never half a file under the slot's name. Saves set aside go to the
    /// "Aside" folder beside the slots. On WebGL the folder is IndexedDB's: the page template's
    /// autoSyncPersistentDataPath persists every closed file.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        public const string Extension = ".save";
        public const string BackupExtension = ".save.bak";
        public const string TempExtension = ".save.tmp";
        public const string AsideFolder = "Aside";

        public FileSaveStore(string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("A save folder is needed", nameof(folder));
            Folder = folder;
        }

        public string Folder { get; }

        public IReadOnlyList<string> Slots()
        {
            var slots = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                if (!Directory.Exists(Folder)) return Array.Empty<string>();
                foreach (string path in Directory.GetFiles(Folder))
                {
                    string name = Path.GetFileName(path);
                    string slot = name.EndsWith(BackupExtension, StringComparison.Ordinal)
                        ? name.Substring(0, name.Length - BackupExtension.Length)
                        : name.EndsWith(Extension, StringComparison.Ordinal)
                            ? name.Substring(0, name.Length - Extension.Length)
                            : null;
                    if (SaveSlots.IsValidId(slot)) slots.Add(slot);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
            return new List<string>(slots);
        }

        public bool Has(string slotId, bool backup = false) =>
            SaveSlots.IsValidId(slotId) && File.Exists(PathOf(slotId, backup));

        public SaveIo ReadHead(string slotId, bool backup, int maxBytes)
        {
            if (!SaveSlots.IsValidId(slotId)) return new SaveIo(SaveError.InvalidSlotId, slotId);
            string path = PathOf(slotId, backup);
            try
            {
                if (!File.Exists(path)) return new SaveIo(SaveError.NotFound, path);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[Math.Max(1, (int)Math.Min(maxBytes + 1L, stream.Length))];
                int read = 0, chunk;
                while (read < buffer.Length && (chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
                {
                    int newline = Array.IndexOf(buffer, (byte)'\n', read, chunk);
                    read += chunk;
                    if (newline >= 0)
                    {
                        var line = new byte[newline];
                        Buffer.BlockCopy(buffer, 0, line, 0, newline);
                        return new SaveIo(line, stream.Length, File.GetLastWriteTimeUtc(path), true);
                    }
                }
                var head = new byte[read];
                Buffer.BlockCopy(buffer, 0, head, 0, read);
                return new SaveIo(head, stream.Length, File.GetLastWriteTimeUtc(path), false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new SaveIo(SaveError.Unreadable, exception.Message);
            }
        }

        public SaveIo Read(string slotId, bool backup = false)
        {
            if (!SaveSlots.IsValidId(slotId)) return new SaveIo(SaveError.InvalidSlotId, slotId);
            string path = PathOf(slotId, backup);
            try
            {
                if (!File.Exists(path)) return new SaveIo(SaveError.NotFound, path);
                byte[] bytes = File.ReadAllBytes(path);
                return new SaveIo(bytes, bytes.Length, File.GetLastWriteTimeUtc(path), true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new SaveIo(SaveError.Unreadable, exception.Message);
            }
        }

        public SaveIo ReadRange(string slotId, bool backup, long offset, int count)
        {
            if (!SaveSlots.IsValidId(slotId)) return new SaveIo(SaveError.InvalidSlotId, slotId);
            string path = PathOf(slotId, backup);
            try
            {
                if (!File.Exists(path)) return new SaveIo(SaveError.NotFound, path);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (offset < 0 || count < 0 || offset + count > stream.Length)
                    return new SaveIo(SaveError.Corrupt, $"{offset}+{count} past {stream.Length} bytes");
                stream.Seek(offset, SeekOrigin.Begin);
                var bytes = new byte[count];
                int read = 0, chunk;
                while (read < count && (chunk = stream.Read(bytes, read, count - read)) > 0) read += chunk;
                return read == count
                    ? new SaveIo(bytes, stream.Length, File.GetLastWriteTimeUtc(path), true)
                    : new SaveIo(SaveError.Corrupt, $"{read} of {count} bytes");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new SaveIo(SaveError.Unreadable, exception.Message);
            }
        }

        public bool SetAside(string slotId, string tag)
        {
            if (!SaveSlots.IsValidId(slotId)) return false;
            string path = PathOf(slotId, false);
            try
            {
                if (!File.Exists(path)) return false;
                string folder = Path.Combine(Folder, AsideFolder);
                Directory.CreateDirectory(folder);
                string name = string.IsNullOrEmpty(tag) ? slotId : $"{slotId}-{tag}";
                string target = Path.Combine(folder, name + Extension);
                for (int n = 2; File.Exists(target); n++) target = Path.Combine(folder, $"{name}-{n}{Extension}");
                File.Move(path, target);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public SaveIo Write(string slotId, byte[] document)
        {
            if (!SaveSlots.IsValidId(slotId)) return new SaveIo(SaveError.InvalidSlotId, slotId);
            if (document == null) throw new ArgumentNullException(nameof(document));
            string path = PathOf(slotId, false), backup = PathOf(slotId, true), temp = Path.Combine(Folder, slotId + TempExtension);
            try
            {
                Directory.CreateDirectory(Folder);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(document, 0, document.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                {
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.Move(temp, path);
                return new SaveIo(document, document.Length, File.GetLastWriteTimeUtc(path), true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is NotSupportedException)
            {
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch (Exception cleanup) when (cleanup is IOException || cleanup is UnauthorizedAccessException)
                {
                    // the next write replaces it
                }
                return new SaveIo(SaveError.WriteFailed, exception.Message);
            }
        }

        public bool Delete(string slotId)
        {
            if (!SaveSlots.IsValidId(slotId)) return false;
            bool any = false;
            try
            {
                foreach (string path in new[] { PathOf(slotId, false), PathOf(slotId, true), Path.Combine(Folder, slotId + TempExtension) })
                {
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    any = true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
            return any;
        }

        private string PathOf(string slotId, bool backup) =>
            Path.Combine(Folder, slotId + (backup ? BackupExtension : Extension));
    }
}
