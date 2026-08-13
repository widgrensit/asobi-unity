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
    }
}
