namespace Asobi
{
    /// <summary>
    /// Reads asobi's shared error object out of a failed response body.
    /// </summary>
    /// <remarks>
    /// Every asobi failure is
    /// <c>{"error": {"code": ..., "message": ..., "details": {...}}}</c>.
    /// <c>JsonUtility</c> cannot deserialize an object into a string field and
    /// drops object-typed members silently, so reading the body into a model
    /// with a single <c>string error</c> left the client with no code and no
    /// message - every failure collapsed to "HTTP 4xx". This scans the raw text
    /// instead, the way <see cref="JsonHelper"/> already does for other
    /// polymorphic fields.
    ///
    /// The flat legacy form, <c>{"error": "some_string"}</c>, is still accepted:
    /// a few routes kept it, and an older deployment may send one.
    ///
    /// No <c>UnityEngine</c> dependency, so it is unit-tested outside a player.
    /// </remarks>
    public static class AsobiErrorParser
    {
        public static AsobiError Parse(string responseText)
        {
            var raw = JsonScan.ExtractField(responseText, "error");
            if (raw == null)
                return null;

            if (raw.Length > 0 && raw[0] == '{')
            {
                return new AsobiError
                {
                    code = JsonScan.Unquote(JsonScan.ExtractField(raw, "code")) ?? "",
                    error = JsonScan.Unquote(JsonScan.ExtractField(raw, "message")) ?? "",
                };
            }

            return new AsobiError { code = "", error = JsonScan.Unquote(raw) ?? "" };
        }
    }
}
