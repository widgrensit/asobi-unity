using System;

namespace Asobi
{
    /// <summary>
    /// Raw JSON text scanning, with no <c>UnityEngine</c> dependency.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="JsonHelper"/> so the parts that are pure string
    /// work can be compiled - and unit-tested - outside a Unity player.
    /// <see cref="JsonHelper"/> delegates here and keeps its own
    /// <c>JsonUtility</c>-bound helpers.
    /// </remarks>
    public static class JsonScan
    {
        /// <summary>
        /// The raw, unparsed JSON text of a top-level field, or null when the
        /// field is absent. Object and array values come back bracketed and can
        /// be handed to a deserializer; a string value comes back WITH its
        /// quotes; numbers, booleans and null come back as literal text.
        /// </summary>
        public static string ExtractField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var searchKey = $"\"{fieldName}\":";
            int keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIdx < 0)
                return null;

            int valueStart = keyIdx + searchKey.Length;
            while (valueStart < json.Length && json[valueStart] == ' ')
                valueStart++;

            if (valueStart >= json.Length)
                return null;

            char startChar = json[valueStart];

            if (startChar == '{' || startChar == '[')
                return ExtractBracketedValue(json, valueStart, startChar);

            if (startChar == '"')
                return ExtractStringValue(json, valueStart);

            int endIdx = valueStart;
            while (endIdx < json.Length && json[endIdx] != ',' && json[endIdx] != '}' && json[endIdx] != ']')
                endIdx++;
            return json.Substring(valueStart, endIdx - valueStart).Trim();
        }

        /// <summary>A quoted JSON string value with its quotes and escapes resolved.</summary>
        public static string Unquote(string raw)
        {
            if (raw == null || raw.Length < 2 || raw[0] != '"' || raw[raw.Length - 1] != '"')
                return raw;
            return raw.Substring(1, raw.Length - 2)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        static string ExtractBracketedValue(string json, int start, char openChar)
        {
            char closeChar = openChar == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                    continue;
                if (c == openChar)
                    depth++;
                else if (c == closeChar)
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        static string ExtractStringValue(string json, int start)
        {
            bool escaped = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                    return json.Substring(start, i - start + 1);
            }
            return null;
        }
    }
}
