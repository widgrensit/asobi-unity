using System;

namespace Asobi
{
    public class AsobiDispatcher
    {
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<int, int> OnReconnecting;
        public event Action OnReconnectFailed;
        public event Action<string> OnMatchState;
        public event Action<string, string> OnMatchEvent;
        public event Action<string> OnChatMessage;
        public event Action<string> OnNotification;
        public event Action<string> OnMatchmakerMatched;
        public event Action<string> OnVoteStart;
        public event Action<string> OnVoteTally;
        public event Action<string> OnVoteResult;
        public event Action<string> OnVoteVetoed;
        public event Action<string> OnWorldTick;
        public event Action<string> OnWorldTerrain;
        public event Action<string> OnWorldJoined;
        public event Action<string> OnWorldLeft;
        public event Action<string, string> OnWorldEvent;
        public event Action<string> OnDmMessage;
        public event Action<string> OnDmSent;
        public event Action<string> OnPresenceUpdated;
        public event Action<string> OnMatchJoined;
        public event Action<string> OnMatchLeft;
        public event Action<string> OnChatJoined;
        public event Action<string> OnChatLeft;
        public event Action<string> OnMatchmakerQueued;
        public event Action<string> OnMatchmakerRemoved;
        public event Action<string> OnVoteCastOk;
        public event Action<string> OnVoteVetoOk;
        public event Action<string> OnError;
        public event Action<string> OnGameError;
        public event Action<string> OnGameMessage;
        public event Action<string> OnHeartbeat;
        public event Action<string> OnMatchFinished;
        public event Action<string> OnMatchmakerExpired;
        public event Action<string> OnMatchmakerFailed;
        public event Action<string> OnWorldFinished;
        public event Action<string> OnWorldList;
        public event Action<string> OnWorldPhaseChanged;

        protected void RaiseDisconnected(string reason) => OnDisconnected?.Invoke(reason);

        // Fired before each retry, so games can show "Reconnecting (attempt/max)...".
        protected void RaiseReconnecting(int attempt, int maxAttempts) => OnReconnecting?.Invoke(attempt, maxAttempts);

        // Fired once after the final retry fails; no further attempts will be made.
        protected void RaiseReconnectFailed() => OnReconnectFailed?.Invoke();

        protected internal virtual void OnPendingResponse(string cid, string type, string raw) { }

        internal void HandleMessage(string raw)
        {
            var env = ProtocolEnvelope.Parse(raw);
            if (env.Type == null) return;

            if (!string.IsNullOrEmpty(env.Cid))
                OnPendingResponse(env.Cid, env.Type, raw);

            switch (env.Type)
            {
                case "session.connected":
                    OnConnected?.Invoke();
                    break;
                case "match.state":
                    OnMatchState?.Invoke(raw);
                    break;
                case "chat.message":
                    OnChatMessage?.Invoke(raw);
                    break;
                case "notification.new":
                    OnNotification?.Invoke(raw);
                    break;
                // TODO deprecate: server only emits "match.matched". The
                // "matchmaker.matched" alias is kept defensively against
                // historical drift; remove in a future major version.
                case "matchmaker.matched":
                case "match.matched":
                    OnMatchmakerMatched?.Invoke(raw);
                    break;
                case "match.finished":
                    OnMatchFinished?.Invoke(raw);
                    break;
                case "match.matchmaker_expired":
                    OnMatchmakerExpired?.Invoke(raw);
                    break;
                case "match.matchmaker_failed":
                    OnMatchmakerFailed?.Invoke(raw);
                    break;
                case "match.vote_start":
                    OnVoteStart?.Invoke(raw);
                    break;
                case "match.vote_tally":
                    OnVoteTally?.Invoke(raw);
                    break;
                case "match.vote_result":
                    OnVoteResult?.Invoke(raw);
                    break;
                case "match.vote_vetoed":
                    OnVoteVetoed?.Invoke(raw);
                    break;
                case "world.tick":
                    OnWorldTick?.Invoke(raw);
                    break;
                case "world.terrain":
                    OnWorldTerrain?.Invoke(raw);
                    break;
                case "world.list":
                    OnWorldList?.Invoke(raw);
                    break;
                case "world.joined":
                    OnWorldJoined?.Invoke(raw);
                    break;
                case "world.left":
                    OnWorldLeft?.Invoke(raw);
                    break;
                case "world.phase_changed":
                    OnWorldPhaseChanged?.Invoke(raw);
                    break;
                case "world.finished":
                    OnWorldFinished?.Invoke(raw);
                    break;
                case "match.joined":
                    OnMatchJoined?.Invoke(raw);
                    break;
                case "match.left":
                    OnMatchLeft?.Invoke(raw);
                    break;
                case "chat.joined":
                    OnChatJoined?.Invoke(raw);
                    break;
                case "chat.left":
                    OnChatLeft?.Invoke(raw);
                    break;
                case "matchmaker.queued":
                    OnMatchmakerQueued?.Invoke(raw);
                    break;
                case "matchmaker.removed":
                    OnMatchmakerRemoved?.Invoke(raw);
                    break;
                case "vote.cast_ok":
                    OnVoteCastOk?.Invoke(raw);
                    break;
                case "vote.veto_ok":
                    OnVoteVetoOk?.Invoke(raw);
                    break;
                case "dm.message":
                    OnDmMessage?.Invoke(raw);
                    break;
                case "dm.sent":
                    OnDmSent?.Invoke(raw);
                    break;
                case "presence.updated":
                    OnPresenceUpdated?.Invoke(raw);
                    break;
                case "session.heartbeat":
                    OnHeartbeat?.Invoke(raw);
                    break;
                case "error":
                    OnError?.Invoke(raw);
                    break;
                // module.* are the server's current names for the same two
                // events. Without them a game silently drops dev-console
                // output from a server on the newer naming.
                case "game.error":
                case "module.error":
                    OnGameError?.Invoke(raw);
                    break;
                case "game.message":
                case "module.message":
                    OnGameMessage?.Invoke(raw);
                    break;
                default:
                    if (env.Type.StartsWith("match."))
                    {
                        var eventName = env.Type.Substring(6);
                        OnMatchEvent?.Invoke(eventName, raw);
                    }
                    else if (env.Type.StartsWith("world."))
                    {
                        var eventName = env.Type.Substring(6);
                        OnWorldEvent?.Invoke(eventName, raw);
                    }
                    break;
            }
        }
    }

    internal readonly struct ProtocolEnvelope
    {
        public readonly string Type;
        public readonly string Cid;

        ProtocolEnvelope(string type, string cid) { Type = type; Cid = cid; }

        public static ProtocolEnvelope Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return default;
            return new ProtocolEnvelope(ReadStringField(raw, "type"), ReadStringField(raw, "cid"));
        }

        static string ReadStringField(string json, string field)
        {
            var key = "\"" + field + "\"";
            int i = 0;
            while (true)
            {
                int k = json.IndexOf(key, i, StringComparison.Ordinal);
                if (k < 0) return null;
                int after = k + key.Length;
                while (after < json.Length && (json[after] == ' ' || json[after] == '\t' || json[after] == '\n' || json[after] == '\r'))
                    after++;
                if (after >= json.Length || json[after] != ':')
                {
                    i = k + key.Length;
                    continue;
                }
                if (!IsKeyPosition(json, k))
                {
                    i = k + key.Length;
                    continue;
                }
                after++;
                while (after < json.Length && (json[after] == ' ' || json[after] == '\t' || json[after] == '\n' || json[after] == '\r'))
                    after++;
                if (after >= json.Length || json[after] != '"') return null;
                int start = after + 1;
                var sb = new System.Text.StringBuilder();
                for (int p = start; p < json.Length; p++)
                {
                    char c = json[p];
                    if (c == '\\' && p + 1 < json.Length)
                    {
                        char n = json[p + 1];
                        switch (n)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            default: sb.Append(n); break;
                        }
                        p++;
                        continue;
                    }
                    if (c == '"') return sb.ToString();
                    sb.Append(c);
                }
                return null;
            }
        }

        static bool IsKeyPosition(string json, int quoteIdx)
        {
            for (int j = quoteIdx - 1; j >= 0; j--)
            {
                char c = json[j];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') continue;
                return c == '{' || c == ',';
            }
            return true;
        }
    }

    // The decoded form of an rpc.ok / rpc.error frame.
    //
    // Lives here rather than in AsobiRealtime so it can be tested: this file is
    // Unity-free and linked into the plain .NET test project, AsobiRealtime is
    // not. AsobiRealtime does nothing with a reply except turn one of these
    // into a completed or faulted Task.
    internal readonly struct RpcReply
    {
        public readonly bool IsError;
        public readonly string ResultJson;
        public readonly string Code;
        public readonly string Message;
        public readonly string DetailsJson;

        RpcReply(bool isError, string resultJson, string code, string message, string detailsJson)
        {
            IsError = isError;
            ResultJson = resultJson;
            Code = code;
            Message = message;
            DetailsJson = detailsJson;
        }

        public static RpcReply Parse(string type, string raw)
        {
            if (type == "rpc.error")
            {
                // An empty error object still gets a code, or a server defect
                // and a domain outcome look identical to a caller branching on
                // Code. Message falls back to the code rather than to null, so
                // Exception.Message is never empty.
                var code = JsonSlice.Unquote(JsonSlice.Read(raw, "payload", "error", "code")) ?? "internal";
                var message = JsonSlice.Unquote(JsonSlice.Read(raw, "payload", "error", "message")) ?? code;
                return new RpcReply(true, null, code, message,
                    JsonSlice.Read(raw, "payload", "error", "details"));
            }
            // The result, not the envelope: a caller deserializing into its own
            // type should not have to know the frame shape.
            return new RpcReply(false, JsonSlice.Read(raw, "payload", "result") ?? "{}", null, null, null);
        }
    }

    // RPC results and error details are arbitrary game-defined JSON, so they
    // cannot be read with ReadStringField and are not worth a parser: hand the
    // caller the raw substring and let it deserialize into whatever type it
    // expects. Brace/bracket depth aware, and skips over strings so a brace
    // inside a message ("}") does not truncate the value.
    internal static class JsonSlice
    {
        // Returns the raw JSON value of `path` (a chain of nested object keys),
        // or null if any step is absent. Read(raw, "payload", "result") on
        // {"payload":{"result":{"reward":100}}} gives {"reward":100}.
        public static string Read(string json, params string[] path)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var span = json;
            foreach (var key in path)
            {
                span = ValueOf(span, key);
                if (span == null) return null;
            }
            return span;
        }

        static string ValueOf(string json, string field)
        {
            var key = "\"" + field + "\"";
            int i = 0;
            while (true)
            {
                int k = json.IndexOf(key, i, StringComparison.Ordinal);
                if (k < 0) return null;
                int p = k + key.Length;
                p = SkipWs(json, p);
                if (p >= json.Length || json[p] != ':') { i = k + key.Length; continue; }
                p = SkipWs(json, p + 1);
                if (p >= json.Length) return null;
                int end = EndOfValue(json, p);
                return end < 0 ? null : json.Substring(p, end - p);
            }
        }

        // Decodes a raw JSON string literal (quotes and escapes) to its text.
        // Returns null for anything that is not a string, so a caller can tell
        // an absent field from one holding a number or an object.
        public static string Unquote(string raw)
        {
            if (raw == null || raw.Length < 2 || raw[0] != '"') return null;
            var sb = new System.Text.StringBuilder();
            for (int p = 1; p < raw.Length; p++)
            {
                char c = raw[p];
                if (c == '"') return sb.ToString();
                if (c == '\\' && p + 1 < raw.Length)
                {
                    char n = raw[++p];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (p + 4 < raw.Length &&
                                int.TryParse(raw.Substring(p + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out var cp))
                            {
                                sb.Append((char)cp);
                                p += 4;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static int SkipWs(string s, int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n' || s[p] == '\r')) p++;
            return p;
        }

        static int EndOfValue(string s, int start)
        {
            char open = s[start];
            if (open == '"') return EndOfString(s, start);
            if (open != '{' && open != '[')
            {
                int p = start;
                while (p < s.Length && s[p] != ',' && s[p] != '}' && s[p] != ']') p++;
                return p;
            }
            char close = open == '{' ? '}' : ']';
            int depth = 0;
            for (int p = start; p < s.Length; p++)
            {
                char c = s[p];
                if (c == '"') { p = EndOfString(s, p) - 1; continue; }
                if (c == open) depth++;
                else if (c == close && --depth == 0) return p + 1;
            }
            return -1;
        }

        static int EndOfString(string s, int quote)
        {
            for (int p = quote + 1; p < s.Length; p++)
            {
                if (s[p] == '\\') { p++; continue; }
                if (s[p] == '"') return p + 1;
            }
            return s.Length;
        }
    }
}
