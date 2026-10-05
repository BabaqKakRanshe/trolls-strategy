using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using TrollStrategy.Application;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// The colony as lines of text: the session's clock and counters, every top-level value of the snapshot, and one
    /// line per building, creature, item, upgrade… with everything it shows. Two sessions are the same game at a
    /// moment when their lines match. Revision counters are left out: they count commits and clock calls, and a
    /// scene calls the clock every frame.
    /// </summary>
    public static class SnapshotDigest
    {
        private const int MaxDepth = 8;
        private const int ClipChars = 160;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Dictionary<Type, MemberInfo[]> Members = new();

        public static List<string> Lines(GameSession session)
        {
            var lines = new List<string>
            {
                $"ActiveTimeMs: {session.ActiveTimeMs}",
                $"SalesGold: {session.SalesGold}",
                $"BattlesWon: {session.BattlesWon}",
                $"HighestMissionLevel: {session.HighestMissionLevel}",
                $"ActiveBattle: {Text(session.ActiveBattle, 0)}"
            };
            var snapshot = session.CurrentSnapshot;
            foreach (var member in MembersOf(snapshot.GetType()))
            {
                object value = Value(member, snapshot, out string error);
                if (error != null) lines.Add($"{member.Name}: {error}");
                else if (value is IEnumerable items and not string)
                {
                    int count = 0;
                    foreach (object item in items) lines.Add($"{member.Name}[{count++}]: {Text(item, 1)}");
                    lines.Add($"{member.Name}: {count}");
                }
                else lines.Add($"{member.Name}: {Text(value, 0)}");
            }
            return lines;
        }

        /// <summary>The first line where two digests part, as both sides read it; null when they read the same.</summary>
        public static string FirstDifference(IReadOnlyList<string> a, IReadOnlyList<string> b, string nameA,
            string nameB)
        {
            for (int i = 0; i < Math.Max(a.Count, b.Count); i++)
            {
                string x = i < a.Count ? a[i] : "(строки нет)", y = i < b.Count ? b[i] : "(строки нет)";
                if (x == y) continue;
                int at = 0;
                while (at < x.Length && at < y.Length && x[at] == y[at]) at++;
                return $"{nameA}: {Clip(x, at)}\n{nameB}: {Clip(y, at)}";
            }
            return null;
        }

        // The line's name and the text around the first differing character.
        private static string Clip(string line, int at)
        {
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            string name = colon > 0 ? line.Substring(0, colon + 2) : string.Empty;
            int start = Math.Max(name.Length, at - ClipChars / 2);
            int end = Math.Min(line.Length, at + ClipChars / 2);
            return name + (start > name.Length ? "…" : string.Empty) + line.Substring(start, end - start) +
                   (end < line.Length ? "…" : string.Empty);
        }

        private static string Text(object value, int depth)
        {
            switch (value)
            {
                case null: return "null";
                case string words: return "\"" + words.Replace("\n", "\\n") + "\"";
                case bool flag: return flag ? "true" : "false";
                case float number: return number.ToString("R", Invariant);
                case double number: return number.ToString("R", Invariant);
                case Enum named: return named.ToString();
                case UnityEngine.Object asset: return asset != null ? asset.name : "null";
            }
            var type = value.GetType();
            if (type.IsPrimitive || value is decimal) return Convert.ToString(value, Invariant);
            if (depth >= MaxDepth) return "…";
            var text = new StringBuilder();
            if (value is IEnumerable items)
            {
                text.Append('[');
                bool first = true;
                foreach (object item in items)
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append(Text(item, depth + 1));
                }
                return text.Append(']').ToString();
            }
            text.Append('{');
            foreach (var member in MembersOf(type))
            {
                object inner = Value(member, value, out string error);
                text.Append(member.Name).Append('=').Append(error ?? Text(inner, depth + 1)).Append(';');
            }
            return text.Append('}').ToString();
        }

        private static object Value(MemberInfo member, object owner, out string error)
        {
            error = null;
            try
            {
                return member is PropertyInfo property ? property.GetValue(owner) : ((FieldInfo)member).GetValue(owner);
            }
            catch (TargetInvocationException exception)
            {
                error = "!" + exception.InnerException?.GetType().Name;
                return null;
            }
        }

        // Public properties, and for the game's own types their fields too (snapshots keep some state private
        // behind methods); static members, indexers, backing fields and revision counters are left out.
        private static MemberInfo[] MembersOf(Type type)
        {
            if (Members.TryGetValue(type, out var members)) return members;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            bool own = type.Namespace != null && type.Namespace.StartsWith("TrollStrategy", StringComparison.Ordinal);
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead).Cast<MemberInfo>();
            var fields = type.GetFields(instance)
                .Where(f => (f.IsPublic || own) && !f.Name.StartsWith("<", StringComparison.Ordinal))
                .Cast<MemberInfo>();
            members = properties.Concat(fields)
                .Where(m => m.Name != "Revision" && m.Name != "_revision")
                .OrderBy(m => m.MetadataToken).ToArray();
            Members[type] = members;
            return members;
        }
    }
}
