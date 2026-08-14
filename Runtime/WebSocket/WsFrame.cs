using System;

namespace Asobi
{
    /// <summary>
    /// Outbound envelope text, with no <c>UnityEngine</c> dependency so the
    /// wire shape can be unit-tested outside a Unity player (as
    /// <see cref="JsonScan"/> is to <see cref="JsonHelper"/>).
    /// </summary>
    internal static class WsFrame
    {
        /// <summary>
        /// A fire-and-forget envelope. <c>seq</c> is a top-level sibling of
        /// <c>payload</c>, never nested inside it, and is omitted entirely
        /// when the caller did not stamp one.
        /// </summary>
        internal static string FireAndForget(string type, string payloadJson, long? seq = null)
        {
            var seqPart = seq.HasValue ? $"\"seq\":{seq.Value}," : "";
            return $"{{\"type\":\"{type}\",{seqPart}\"payload\":{payloadJson}}}";
        }

        /// <summary>
        /// A correlated request envelope. <c>cid</c> is what the reply is
        /// matched on, so like <c>seq</c> it is a top-level sibling of
        /// <c>payload</c>, never nested inside it.
        /// </summary>
        internal static string Request(string type, string payloadJson, string cid)
        {
            return $"{{\"type\":\"{type}\",\"payload\":{payloadJson},\"cid\":\"{cid}\"}}";
        }

        /// <summary>
        /// The payload for a <c>world.input</c> frame: the caller's JSON
        /// verbatim, because the server takes the payload as the input map
        /// itself. An input that was not supplied becomes an empty map.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The input is not a JSON object. Non-JSON text splices onto the wire
        /// as a malformed frame, and an array or a bare value reaches the
        /// server as a badmap; either way <c>world.input</c> carries no
        /// <c>cid</c>, so there is nothing to correlate a rejection to and the
        /// failure would be silent. Checked by first character rather than
        /// parsed: this runs once per input frame.
        /// </exception>
        internal static string WorldInputPayload(string inputJson)
        {
            if (IsBlank(inputJson)) return "{}";
            if (!IsSingleJsonObject(inputJson))
                throw new ArgumentException(
                    "world.input takes a single JSON object: the payload is the input map itself, so an array, a bare value, trailing text or unbalanced braces cannot be sent.",
                    nameof(inputJson));

            return inputJson;
        }

        // JSON's own whitespace set, not char.IsWhiteSpace: U+00A0 and friends are
        // not legal between tokens, and letting one through puts a byte the server's
        // decoder rejects right before the payload.
        static bool IsJsonSpace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';

        static bool IsBlank(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (var c in s)
                if (!IsJsonSpace(c))
                    return false;
            return true;
        }

        // The payload is spliced into the frame unparsed, so "starts with {" is not
        // enough: `{},"seq":9` passes that and injects a top-level frame field the
        // client never stamped, which world.ack would then acknowledge. Walk the
        // whole string and require exactly one balanced object with nothing after it.
        static bool IsSingleJsonObject(string s)
        {
            var i = 0;
            while (i < s.Length && IsJsonSpace(s[i])) i++;
            if (i >= s.Length || s[i] != '{') return false;

            var depth = 0;
            var inString = false;
            var escaped = false;

            for (; i < s.Length; i++)
            {
                var c = s[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) break;
                    if (depth < 0) return false;
                }
            }

            if (depth != 0 || inString) return false;

            for (i++; i < s.Length; i++)
                if (!IsJsonSpace(s[i]))
                    return false;

            return true;
        }
    }
}
