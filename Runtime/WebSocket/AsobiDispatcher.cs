using System;

namespace Asobi
{
    public class AsobiDispatcher
    {
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
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
        public event Action<string> OnHeartbeat;
        public event Action<string> OnMatchFinished;
        public event Action<string> OnMatchmakerExpired;
        public event Action<string> OnMatchmakerFailed;
        public event Action<string> OnWorldFinished;
        public event Action<string> OnWorldList;
        public event Action<string> OnWorldPhaseChanged;

        protected void RaiseDisconnected(string reason) => OnDisconnected?.Invoke(reason);

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
}
