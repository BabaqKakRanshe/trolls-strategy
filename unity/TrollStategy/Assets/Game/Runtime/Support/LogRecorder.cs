using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TrollStrategy.Support
{
    /// <summary>
    /// The last lines of the game's log, kept in memory from before the first scene. Reports carry it on every
    /// platform; in a browser it is the only log there is (no Player.log).
    /// </summary>
    public sealed class LogRecorder
    {
        public const int DefaultCapacity = 2000;
        /// <summary>Longest message kept, stack trace included.</summary>
        public const int MaxEntryChars = 8000;

        private readonly object _lock = new();
        private readonly string[] _entries;
        private int _next;
        private int _count;
        private long _dropped;
        private int _errors;

        public LogRecorder(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new string[capacity];
        }

        /// <summary>The game's own recorder, listening since startup.</summary>
        public static LogRecorder Game { get; private set; }

        /// <summary>Errors, exceptions and failed asserts seen so far.</summary>
        public int Errors
        {
            get { lock (_lock) return _errors; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Listen()
        {
            if (Game != null) UnityEngine.Application.logMessageReceivedThreaded -= Game.Add;
            Game = new LogRecorder();
            UnityEngine.Application.logMessageReceivedThreaded += Game.Add;
        }

        public void Add(string message, string stackTrace, LogType type)
        {
            bool error = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            var line = new StringBuilder();
            line.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" [").Append(type).Append("] ").Append(message);
            if (error && !string.IsNullOrEmpty(stackTrace)) line.Append('\n').Append(stackTrace.TrimEnd());
            string entry = line.Length > MaxEntryChars ? line.ToString(0, MaxEntryChars) + " …" : line.ToString();

            lock (_lock)
            {
                if (_count == _entries.Length) _dropped++;
                else _count++;
                _entries[_next] = entry;
                _next = (_next + 1) % _entries.Length;
                if (error) _errors++;
            }
        }

        /// <summary>The kept lines, oldest first, marked when older ones were dropped.</summary>
        public string Text()
        {
            var text = new StringBuilder();
            lock (_lock)
            {
                if (_dropped > 0) text.Append("[… раньше было ещё ").Append(_dropped).AppendLine(" строк …]");
                int start = (_next - _count + _entries.Length) % _entries.Length;
                for (int i = 0; i < _count; i++) text.AppendLine(_entries[(start + i) % _entries.Length]);
            }
            return text.ToString();
        }
    }
}
