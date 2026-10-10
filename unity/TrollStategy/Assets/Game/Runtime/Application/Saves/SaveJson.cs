using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrollStrategy.Application
{
    /// <summary>A save file that cannot be read as written: broken JSON, a missing field, a value of the wrong type.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message)
        {
        }
    }

    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// One value of a save file. Numbers keep their text, so an int reads back exactly and a float goes through
    /// <see cref="float.Parse(string, IFormatProvider)"/> once, the inverse of the "R" format it was written in.
    /// </summary>
    public abstract class JsonNode
    {
        public abstract JsonKind Kind { get; }

        public static readonly JsonNode Null = new JsonNull();

        /// <summary>The value as compact JSON text.</summary>
        public override string ToString()
        {
            var writer = new JsonWriter();
            writer.Node(this);
            return writer.ToString();
        }
    }

    public sealed class JsonNull : JsonNode
    {
        public override JsonKind Kind => JsonKind.Null;
    }

    public sealed class JsonBool : JsonNode
    {
        public JsonBool(bool value) => Value = value;
        public bool Value { get; }
        public override JsonKind Kind => JsonKind.Bool;
    }

    public sealed class JsonNumber : JsonNode
    {
        public JsonNumber(string text) => Text = text;
        public string Text { get; }
        public override JsonKind Kind => JsonKind.Number;
    }

    public sealed class JsonString : JsonNode
    {
        public JsonString(string value) => Value = value ?? string.Empty;
        public string Value { get; }
        public override JsonKind Kind => JsonKind.String;
    }

    public sealed class JsonArray : JsonNode
    {
        public List<JsonNode> Items { get; } = new();
        public int Count => Items.Count;
        public JsonNode this[int index] => Items[index];
        public override JsonKind Kind => JsonKind.Array;
        public JsonArray Add(JsonNode item)
        {
            Items.Add(item ?? Null);
            return this;
        }
    }

    /// <summary>Members in the order they were written or read; a name appears once.</summary>
    public sealed class JsonObject : JsonNode
    {
        private readonly List<KeyValuePair<string, JsonNode>> _members = new();
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        public override JsonKind Kind => JsonKind.Object;
        public IReadOnlyList<KeyValuePair<string, JsonNode>> Members => _members;
        public int Count => _members.Count;

        public bool Has(string name) => _index.ContainsKey(name);

        /// <summary>The member's value; null when the object has no such member.</summary>
        public JsonNode this[string name]
        {
            get => _index.TryGetValue(name, out int i) ? _members[i].Value : null;
            set => Set(name, value);
        }

        public JsonObject Set(string name, JsonNode value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            value ??= Null;
            if (_index.TryGetValue(name, out int i)) _members[i] = new KeyValuePair<string, JsonNode>(name, value);
            else
            {
                _index.Add(name, _members.Count);
                _members.Add(new KeyValuePair<string, JsonNode>(name, value));
            }
            return this;
        }

        public bool Remove(string name)
        {
            if (!_index.TryGetValue(name, out int at)) return false;
            _members.RemoveAt(at);
            _index.Clear();
            for (int i = 0; i < _members.Count; i++) _index.Add(_members[i].Key, i);
            return true;
        }

        internal bool TryAdd(string name, JsonNode value)
        {
            if (_index.ContainsKey(name)) return false;
            Set(name, value);
            return true;
        }
    }

    /// <summary>Compact JSON text: no whitespace, members in the order they are written, numbers in the invariant culture.</summary>
    public sealed class JsonWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly StringBuilder _text;
        // per open container: whether a value was already written into it (a comma goes before the next)
        private readonly Stack<bool> _filled = new();
        private bool _afterName;

        public JsonWriter(int capacity = 256) => _text = new StringBuilder(capacity);

        public override string ToString() => _text.ToString();

        public JsonWriter BeginObject()
        {
            Separate();
            _text.Append('{');
            _filled.Push(false);
            return this;
        }

        public JsonWriter EndObject()
        {
            _filled.Pop();
            _text.Append('}');
            return this;
        }

        public JsonWriter BeginArray()
        {
            Separate();
            _text.Append('[');
            _filled.Push(false);
            return this;
        }

        public JsonWriter EndArray()
        {
            _filled.Pop();
            _text.Append(']');
            return this;
        }

        public JsonWriter Name(string name)
        {
            Separate();
            AppendString(name);
            _text.Append(':');
            _afterName = true;
            return this;
        }

        public JsonWriter Value(string value)
        {
            Separate();
            if (value == null) _text.Append("null");
            else AppendString(value);
            return this;
        }

        public JsonWriter Value(int value)
        {
            Separate();
            _text.Append(value.ToString(Invariant));
            return this;
        }

        public JsonWriter Value(long value)
        {
            Separate();
            _text.Append(value.ToString(Invariant));
            return this;
        }

        public JsonWriter Value(uint value)
        {
            Separate();
            _text.Append(value.ToString(Invariant));
            return this;
        }

        public JsonWriter Value(bool value)
        {
            Separate();
            _text.Append(value ? "true" : "false");
            return this;
        }

        /// <summary>
        /// A float as the shortest text that parses back to the same bits ("R"); NaN and the infinities, which JSON
        /// numbers cannot hold, as the strings "NaN", "Infinity" and "-Infinity".
        /// </summary>
        public JsonWriter Value(float value)
        {
            Separate();
            if (float.IsNaN(value) || float.IsInfinity(value)) AppendString(value.ToString(Invariant));
            else _text.Append(value.ToString("R", Invariant));
            return this;
        }

        public JsonWriter Null()
        {
            Separate();
            _text.Append("null");
            return this;
        }

        public JsonWriter Node(JsonNode node)
        {
            switch (node)
            {
                case null:
                case JsonNull:
                    return Null();
                case JsonBool b:
                    return Value(b.Value);
                case JsonNumber n:
                    Separate();
                    _text.Append(n.Text);
                    return this;
                case JsonString s:
                    return Value(s.Value);
                case JsonArray a:
                    BeginArray();
                    foreach (var item in a.Items) Node(item);
                    return EndArray();
                case JsonObject o:
                    BeginObject();
                    foreach (var member in o.Members) Name(member.Key).Node(member.Value);
                    return EndObject();
                default:
                    throw new ArgumentException($"Unknown JSON node {node.GetType().Name}");
            }
        }

        private void Separate()
        {
            if (_afterName)
            {
                _afterName = false;
                return;
            }
            if (_filled.Count == 0) return;
            if (_filled.Peek()) _text.Append(',');
            else
            {
                _filled.Pop();
                _filled.Push(true);
            }
        }

        private void AppendString(string value)
        {
            _text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case '\n': _text.Append("\\n"); break;
                    case '\r': _text.Append("\\r"); break;
                    case '\t': _text.Append("\\t"); break;
                    case '\b': _text.Append("\\b"); break;
                    case '\f': _text.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                            _text.Append("\\u").Append(((int)c).ToString("x4", Invariant));
                        else _text.Append(c);
                        break;
                }
            }
            _text.Append('"');
        }
    }

    /// <summary>
    /// Reads JSON text into <see cref="JsonNode"/>s. Strict: one value, nothing after it but whitespace, no comments,
    /// no trailing commas, no repeated member names, at most <see cref="MaxDepth"/> levels. Every fault is a
    /// <see cref="SaveFormatException"/> naming the position.
    /// </summary>
    public sealed class JsonParser
    {
        public const int MaxDepth = 64;

        private readonly string _text;
        private int _at;
        private int _depth;

        private JsonParser(string text) => _text = text ?? string.Empty;

        public static JsonNode Parse(string text)
        {
            var parser = new JsonParser(text);
            parser.SkipSpace();
            var value = parser.ReadValue();
            parser.SkipSpace();
            if (parser._at != parser._text.Length) throw parser.Fault("text after the end of the value");
            return value;
        }

        private JsonNode ReadValue()
        {
            if (_at >= _text.Length) throw Fault("the text ends where a value should be");
            char c = _text[_at];
            switch (c)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return new JsonString(ReadString());
                case 't': Expect("true"); return new JsonBool(true);
                case 'f': Expect("false"); return new JsonBool(false);
                case 'n': Expect("null"); return JsonNode.Null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                    throw Fault($"unexpected '{c}'");
            }
        }

        private JsonObject ReadObject()
        {
            Enter();
            _at++;
            var result = new JsonObject();
            SkipSpace();
            if (Peek() == '}')
            {
                _at++;
                _depth--;
                return result;
            }
            while (true)
            {
                SkipSpace();
                if (Peek() != '"') throw Fault("a member name should be here");
                int nameAt = _at;
                string name = ReadString();
                SkipSpace();
                if (Peek() != ':') throw Fault("':' should follow a member name");
                _at++;
                SkipSpace();
                var value = ReadValue();
                if (!result.TryAdd(name, value)) throw Fault($"member \"{name}\" appears twice", nameAt);
                SkipSpace();
                char next = Peek();
                _at++;
                if (next == ',') continue;
                if (next == '}') break;
                throw Fault("',' or '}' should follow a member", _at - 1);
            }
            _depth--;
            return result;
        }

        private JsonArray ReadArray()
        {
            Enter();
            _at++;
            var result = new JsonArray();
            SkipSpace();
            if (Peek() == ']')
            {
                _at++;
                _depth--;
                return result;
            }
            while (true)
            {
                SkipSpace();
                result.Add(ReadValue());
                SkipSpace();
                char next = Peek();
                _at++;
                if (next == ',') continue;
                if (next == ']') break;
                throw Fault("',' or ']' should follow an item", _at - 1);
            }
            _depth--;
            return result;
        }

        private string ReadString()
        {
            _at++; // opening quote
            var value = new StringBuilder();
            while (true)
            {
                if (_at >= _text.Length) throw Fault("the text ends inside a string");
                char c = _text[_at++];
                if (c == '"') return value.ToString();
                if (c < 0x20) throw Fault("a control character inside a string", _at - 1);
                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }
                if (_at >= _text.Length) throw Fault("the text ends inside a string");
                char e = _text[_at++];
                switch (e)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (_at + 4 > _text.Length ||
                            !int.TryParse(_text.Substring(_at, 4), NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture, out int code))
                            throw Fault("a broken \\u escape", _at - 2);
                        value.Append((char)code);
                        _at += 4;
                        break;
                    default:
                        throw Fault($"unknown escape \\{e}", _at - 2);
                }
            }
        }

        private JsonNumber ReadNumber()
        {
            int start = _at;
            if (Peek() == '-') _at++;
            if (!Digit(Peek())) throw Fault("a number needs digits", start);
            if (Peek() == '0') _at++;
            else while (Digit(Peek())) _at++;
            if (Peek() == '.')
            {
                _at++;
                if (!Digit(Peek())) throw Fault("a number needs digits after '.'", start);
                while (Digit(Peek())) _at++;
            }
            if (Peek() == 'e' || Peek() == 'E')
            {
                _at++;
                if (Peek() == '+' || Peek() == '-') _at++;
                if (!Digit(Peek())) throw Fault("a number needs digits in its exponent", start);
                while (Digit(Peek())) _at++;
            }
            return new JsonNumber(_text.Substring(start, _at - start));
        }

        private static bool Digit(char c) => c >= '0' && c <= '9';

        private char Peek() => _at < _text.Length ? _text[_at] : '\0';

        private void Expect(string word)
        {
            if (string.CompareOrdinal(_text, _at, word, 0, word.Length) != 0) throw Fault($"expected {word}");
            _at += word.Length;
        }

        private void Enter()
        {
            if (++_depth > MaxDepth) throw Fault($"nested deeper than {MaxDepth} levels");
        }

        private void SkipSpace()
        {
            while (_at < _text.Length)
            {
                char c = _text[_at];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r') return;
                _at++;
            }
        }

        private SaveFormatException Fault(string what, int at = -1) =>
            new($"JSON: {what} at character {(at >= 0 ? at : _at)}");
    }
}
