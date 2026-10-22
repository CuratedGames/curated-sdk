using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Curated.Sdk.Internal;

/// <summary>
/// Minimal JSON codec for wire models so the SDK has no third-party assembly dependencies
/// (engines such as Unity often bundle conflicting copies of System.Text.Json and friends).
/// Property names map PascalCase to snake_case; nulls are omitted when writing; unknown fields
/// are ignored when reading.
/// </summary>
internal static class Json
{
    public static string Serialize(object? value)
    {
        var sb = new StringBuilder();
        Write(sb, value);
        return sb.ToString();
    }

    public static T Deserialize<T>(string json)
    {
        var tree = Parse(json);
        return (T)Convert(tree, typeof(T))!;
    }

    /// <summary>Parse into Dictionary / List / string / long / double / bool / null.</summary>
    public static object? Parse(string json)
    {
        if (json is null)
        {
            throw new FormatException("JSON text is null");
        }

        var reader = new Reader(json);
        reader.SkipWhitespace();
        var value = reader.ReadValue();
        reader.SkipWhitespace();
        if (!reader.AtEnd)
        {
            throw new FormatException("Unexpected trailing JSON content");
        }

        return value;
    }

    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_')
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string NormalizeKey(string name) => name.Replace("_", string.Empty).ToLowerInvariant();

    private static void Write(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                return;
            case string s:
                WriteString(sb, s);
                return;
            case bool b:
                sb.Append(b ? "true" : "false");
                return;
            case Guid g:
                WriteString(sb, g.ToString("D"));
                return;
            case DateTimeOffset dto:
                WriteString(sb, dto.ToString("o", CultureInfo.InvariantCulture));
                return;
            case DateTime dt:
                WriteString(sb, dt.ToString("o", CultureInfo.InvariantCulture));
                return;
            case Enum e:
                WriteString(sb, ToSnakeCase(e.ToString()));
                return;
            case float or double or decimal:
                sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case IDictionary dict:
                WriteDictionary(sb, dict);
                return;
            case IEnumerable list:
                sb.Append('[');
                var first = true;
                foreach (var item in list)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }

                    first = false;
                    Write(sb, item);
                }

                sb.Append(']');
                return;
            default:
                WriteObject(sb, value);
                return;
        }
    }

    private static void WriteDictionary(StringBuilder sb, IDictionary dict)
    {
        sb.Append('{');
        var first = true;
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Value is null)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            WriteString(sb, System.Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
            sb.Append(':');
            Write(sb, entry.Value);
        }

        sb.Append('}');
    }

    private static void WriteObject(StringBuilder sb, object value)
    {
        sb.Append('{');
        var first = true;
        foreach (var prop in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var v = prop.GetValue(value);
            if (v is null)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            WriteString(sb, ToSnakeCase(prop.Name));
            sb.Append(':');
            Write(sb, v);
        }

        sb.Append('}');
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }

    private static object? Convert(object? node, Type target)
    {
        var underlying = Nullable.GetUnderlyingType(target);
        if (node is null)
        {
            if (target.IsValueType && underlying is null)
            {
                throw new FormatException($"null is not valid for {target.Name}");
            }

            return null;
        }

        target = underlying ?? target;

        if (target == typeof(object))
        {
            return node;
        }

        if (target == typeof(string))
        {
            return node as string ?? throw new FormatException("expected a JSON string");
        }

        if (target == typeof(bool))
        {
            return node is bool b ? b : throw new FormatException("expected a JSON boolean");
        }

        if (target == typeof(Guid))
        {
            return Guid.Parse(node as string ?? throw new FormatException("expected a uuid string"));
        }

        if (target == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(
                node as string ?? throw new FormatException("expected a date string"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        if (target.IsEnum)
        {
            var text = NormalizeKey(node as string ?? throw new FormatException("expected an enum string"));
            foreach (var name in Enum.GetNames(target))
            {
                if (NormalizeKey(name) == text)
                {
                    return Enum.Parse(target, name);
                }
            }

            throw new FormatException($"unknown {target.Name} value");
        }

        if (target.IsPrimitive || target == typeof(decimal))
        {
            if (node is long or double)
            {
                return System.Convert.ChangeType(node, target, CultureInfo.InvariantCulture);
            }

            throw new FormatException("expected a JSON number");
        }

        var elementType = ListElementType(target);
        if (elementType is not null)
        {
            if (node is not List<object?> items)
            {
                throw new FormatException("expected a JSON array");
            }

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var item in items)
            {
                list.Add(Convert(item, elementType));
            }

            if (target.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        if (node is not Dictionary<string, object?> fields)
        {
            throw new FormatException($"expected a JSON object for {target.Name}");
        }

        var instance = Activator.CreateInstance(target)
            ?? throw new FormatException($"cannot create {target.Name}");
        var byKey = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in fields)
        {
            byKey[NormalizeKey(pair.Key)] = pair.Value;
        }

        foreach (var prop in target.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || !byKey.TryGetValue(NormalizeKey(prop.Name), out var raw))
            {
                continue;
            }

            prop.SetValue(instance, Convert(raw, prop.PropertyType));
        }

        return instance;
    }

    private static Type? ListElementType(Type target)
    {
        if (target.IsArray)
        {
            return target.GetElementType();
        }

        if (!target.IsGenericType)
        {
            return null;
        }

        var def = target.GetGenericTypeDefinition();
        if (def == typeof(List<>)
            || def == typeof(IList<>)
            || def == typeof(IReadOnlyList<>)
            || def == typeof(IReadOnlyCollection<>)
            || def == typeof(ICollection<>)
            || def == typeof(IEnumerable<>))
        {
            return target.GetGenericArguments()[0];
        }

        return null;
    }

    private sealed class Reader
    {
        private readonly string _s;
        private int _i;

        public Reader(string s) => _s = s;

        public bool AtEnd => _i >= _s.Length;

        public void SkipWhitespace()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
            {
                _i++;
            }
        }

        public object? ReadValue()
        {
            if (AtEnd)
            {
                throw new FormatException("Unexpected end of JSON");
            }

            var c = _s[_i];
            switch (c)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return ReadString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ReadNumber();
                    }

                    throw new FormatException($"Unexpected character '{c}' in JSON");
            }
        }

        private Dictionary<string, object?> ReadObject()
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            _i++;
            SkipWhitespace();
            if (Peek() == '}')
            {
                _i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                {
                    throw new FormatException("Expected a property name");
                }

                var key = ReadString();
                SkipWhitespace();
                Consume(':');
                SkipWhitespace();
                result[key] = ReadValue();
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _i++;
                    continue;
                }

                Consume('}');
                return result;
            }
        }

        private List<object?> ReadArray()
        {
            var result = new List<object?>();
            _i++;
            SkipWhitespace();
            if (Peek() == ']')
            {
                _i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                result.Add(ReadValue());
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _i++;
                    continue;
                }

                Consume(']');
                return result;
            }
        }

        private string ReadString()
        {
            Consume('"');
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                {
                    throw new FormatException("Unterminated JSON string");
                }

                var c = _s[_i++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (AtEnd)
                {
                    throw new FormatException("Unterminated JSON escape");
                }

                var e = _s[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_i + 4 > _s.Length)
                        {
                            throw new FormatException("Invalid JSON unicode escape");
                        }

                        sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _i += 4;
                        break;
                    default:
                        throw new FormatException("Invalid JSON escape");
                }
            }
        }

        private object ReadNumber()
        {
            var start = _i;
            var isInteger = true;
            while (_i < _s.Length)
            {
                var c = _s[_i];
                if (c == '.' || c == 'e' || c == 'E')
                {
                    isInteger = false;
                }
                else if (!(c == '-' || c == '+' || (c >= '0' && c <= '9')))
                {
                    break;
                }

                _i++;
            }

            var text = _s.Substring(start, _i - start);
            if (isInteger && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
            {
                return l;
            }

            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private char Peek() => AtEnd ? '\0' : _s[_i];

        private void Consume(char expected)
        {
            if (Peek() != expected)
            {
                throw new FormatException($"Expected '{expected}' in JSON");
            }

            _i++;
        }

        private void Expect(string literal)
        {
            if (string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Invalid JSON literal");
            }

            _i += literal.Length;
        }
    }
}
