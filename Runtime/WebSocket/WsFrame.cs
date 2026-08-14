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
            if (string.IsNullOrWhiteSpace(inputJson)) return "{}";

            var i = 0;
            while (char.IsWhiteSpace(inputJson[i])) i++;
            if (inputJson[i] != '{')
                throw new ArgumentException(
                    "world.input takes a JSON object: the payload is the input map itself, so an array, a bare value or non-JSON text cannot be sent.",
                    nameof(inputJson));

            return inputJson;
        }
    }
}
