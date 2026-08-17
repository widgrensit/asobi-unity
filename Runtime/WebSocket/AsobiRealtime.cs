using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Asobi
{
    public class AsobiRealtime : AsobiDispatcher, IDisposable
    {
        readonly AsobiClient _client;
        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        int _cidCounter;
        readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

        int _reconnectAttempts;
        bool _disconnectRequested;
        CancellationTokenSource _reconnectCts;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        // Opt-out: set to false to disable automatic reconnection after an
        // unexpected close. Does not affect a game-initiated DisconnectAsync().
        public bool AutoReconnect { get; set; } = true;

        internal AsobiRealtime(AsobiClient client) => _client = client;

        // Test-only: construct without a client/WebSocket so dispatch logic
        // can be exercised in isolation.
        internal AsobiRealtime() { }

        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            _disconnectRequested = false;
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            await _ws.ConnectAsync(new Uri(_client.Config.WsUrl), _cts.Token);
            _ = ReceiveLoop();

            var payload = JsonUtility.ToJson(new WsConnectPayload { token = _client.AccessToken });
            await SendAsync("session.connect", payload);

            _reconnectAttempts = 0;
        }

        public Task<string> SendHeartbeatAsync()
        {
            return SendAsync("session.heartbeat", "{}");
        }

        /// <summary>
        /// Call a server extension's RPC method.
        /// </summary>
        /// <param name="method">Namespaced method, e.g. "quests.claim".</param>
        /// <param name="paramsJson">
        /// The params object as JSON. Always an object, so an extension can add
        /// a field without breaking a shipped game.
        /// </param>
        /// <returns>
        /// The result object as raw JSON - deserialize into whatever type the
        /// extension documents.
        /// </returns>
        /// <exception cref="AsobiRpcException">
        /// The method rejected the call. Branch on <see cref="AsobiRpcException.Code"/>,
        /// never on the message.
        /// </exception>
        /// <remarks>
        /// Correlated by cid like every other request, so several calls may be
        /// in flight at once and may answer out of order.
        /// </remarks>
        public Task<string> RpcAsync(string method, string paramsJson = "{}")
        {
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("method is required", nameof(method));
            if (string.IsNullOrEmpty(paramsJson)) paramsJson = "{}";
            // protocol versions the payload rather than the frame type, so a
            // future version is a rejection a client can read.
            var payload = $"{{\"protocol\":1,\"method\":{JsonQuote(method)},\"params\":{paramsJson}}}";
            return SendAsync("rpc.call", payload);
        }

        static string JsonQuote(string s)
        {
            var sb = new System.Text.StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public Task SendMatchInputAsync(string data)
        {
            var payload = JsonUtility.ToJson(new WsMatchInputPayload { data = data });
            return SendFireAndForget("match.input", payload);
        }

        /// <summary>
        /// Browse live matches. Every filter is optional; an omitted one is
        /// not applied.
        /// </summary>
        /// <param name="mode">Only matches running this mode.</param>
        /// <param name="hasCapacity">True to keep only matches with a free slot.</param>
        /// <param name="joinable">
        /// True for matches open to new players, false for the ones that have
        /// closed themselves - a browser showing in-progress matches asks for
        /// false. Separate from <paramref name="hasCapacity"/>: a match with
        /// room may still be closed.
        /// </param>
        /// <remarks>
        /// Only modes that opt in with `listed = true` appear here - a
        /// matchmaker-spawned match is already assigned to its players.
        /// </remarks>
        public Task<string> MatchListAsync(string mode = null, bool? hasCapacity = null, bool? joinable = null)
        {
            string payload;
            if (mode != null || hasCapacity.HasValue || joinable.HasValue)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (mode != null) parts.Add($"\"mode\":\"{mode}\"");
                if (hasCapacity.HasValue) parts.Add($"\"has_capacity\":{(hasCapacity.Value ? "true" : "false")}");
                if (joinable.HasValue) parts.Add($"\"joinable\":{(joinable.Value ? "true" : "false")}");
                payload = "{" + string.Join(",", parts) + "}";
            }
            else
            {
                payload = "{}";
            }
            return SendAsync("match.list", payload);
        }

        /// <summary>
        /// Get into a live match of a mode, spawning one if there is none.
        /// The match twin of <see cref="WorldFindOrCreateAsync"/>.
        /// </summary>
        /// <param name="mode">
        /// The match mode. Match parameters are not client-chosen: size, min
        /// players and the rest come from the server-side mode config.
        /// </param>
        /// <returns>
        /// The raw <c>match.joined</c> envelope, the same frame
        /// <see cref="JoinMatchAsync"/> replies with, so the reply routes
        /// exactly as that one does.
        /// </returns>
        /// <remarks>
        /// Reach for this when you want a player in a match now.
        /// <see cref="MatchListAsync"/> followed by
        /// <see cref="JoinMatchAsync"/> can only join a match that already
        /// exists, and no client call spawns one: the matchmaker spawns only
        /// once it has grouped enough co-queued tickets, and it never drops a
        /// player into a running match. So before this frame an empty listing
        /// left the caller with nothing but the queue. This finds a live match
        /// or spawns one, resolved server-side and serialized, so simultaneous
        /// callers converge on one match.
        ///
        /// A mode opts into it with <c>quick_play</c>, which defaults to false
        /// for match modes; a mode that has not opted in is refused with
        /// <c>quick_play_disabled</c>. That is a separate axis from
        /// <c>listed</c>, which only decides browser visibility.
        ///
        /// Refusals include <c>not_found</c> (the mode is unknown or not
        /// configured, so a misspelt name lands here), <c>quick_play_disabled</c>,
        /// <c>wrong_mode_type</c> (a world mode), <c>match_capacity_reached</c>
        /// (node-wide cap) and <c>join_rate_limited</c> (the same bucket as
        /// <see cref="JoinMatchAsync"/> and <see cref="WorldJoinAsync"/>). More
        /// can be added, so treat an unrecognised reason as a refusal as well.
        ///
        /// A refusal faults the task with <see cref="AsobiException"/>, whose
        /// <c>StatusCode</c> is always -1 for a WebSocket error frame and so
        /// carries nothing. The reason is <c>payload.reason</c> in the raw
        /// envelope, which is what <c>Message</c> holds. Read it with the SDK's
        /// own scanner:
        /// <code>
        /// catch (AsobiException ex)
        /// {
        ///     var payload = JsonScan.ExtractField(ex.Message, "payload");
        ///     var reason = JsonScan.Unquote(JsonScan.ExtractField(payload, "reason"));
        ///     if (reason == "quick_play_disabled") ShowModeClosed();
        /// }
        /// </code>
        /// Branch on <c>payload.reason</c> rather than <c>payload.error.code</c>:
        /// several reasons here share the generic <c>ws.request_failed</c> code,
        /// though not all do - <c>join_refused</c>, <c>match_full</c> and
        /// <c>match_locked</c> carry their own mapped codes. The reason string
        /// is the one field that tells every refusal apart.
        ///
        /// One refusal has a second level worth reading. The lobby can refuse
        /// before the join (<c>not_found</c>, <c>quick_play_disabled</c>,
        /// <c>wrong_mode_type</c>, <c>match_capacity_reached</c>), and the join
        /// itself can refuse after a match is found (<c>join_refused</c>,
        /// <c>match_full</c>, <c>match_locked</c>). When a game's own
        /// <c>join</c> script refuses, <c>reason</c> is the fixed literal
        /// <c>join_refused</c> and the script's own wording is one level
        /// deeper, at <c>payload.error.details.refused_reason</c> - a script
        /// cannot mint an error code, so its text travels as a detail. That
        /// string is the one to show a player:
        /// <code>
        /// if (reason == "join_refused")
        /// {
        ///     var error = JsonScan.ExtractField(payload, "error");
        ///     var details = JsonScan.ExtractField(error, "details");
        ///     ShowRefused(JsonScan.Unquote(JsonScan.ExtractField(details, "refused_reason")));
        /// }
        /// </code>
        ///
        /// Requires asobi core v0.86.0 or later.
        /// </remarks>
        public Task<string> MatchFindOrCreateAsync(string mode)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerPayload { mode = mode });
            return SendAsync("match.find_or_create", payload);
        }

        public Task<string> JoinMatchAsync(string matchId)
        {
            var payload = JsonUtility.ToJson(new WsMatchJoinPayload { match_id = matchId });
            return SendAsync("match.join", payload);
        }

        public Task<string> LeaveMatchAsync()
        {
            return SendAsync("match.leave", "{}");
        }

        public Task<string> JoinChatAsync(string channelId)
        {
            var payload = JsonUtility.ToJson(new WsChatChannelPayload { channel_id = channelId });
            return SendAsync("chat.join", payload);
        }

        public Task<string> LeaveChatAsync(string channelId)
        {
            var payload = JsonUtility.ToJson(new WsChatChannelPayload { channel_id = channelId });
            return SendAsync("chat.leave", payload);
        }

        public Task SendChatMessageAsync(string channelId, string content)
        {
            var payload = JsonUtility.ToJson(new WsChatSendPayload { channel_id = channelId, content = content });
            return SendFireAndForget("chat.send", payload);
        }

        public Task<string> AddToMatchmakerAsync(string mode = "default", string properties = null, string[] party = null)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerAddPayload
            {
                mode = mode,
                properties = properties,
                party = party
            });
            return SendAsync("matchmaker.add", payload);
        }

        public Task<string> RemoveFromMatchmakerAsync(string ticketId)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerRemovePayload { ticket_id = ticketId });
            return SendAsync("matchmaker.remove", payload);
        }

        public Task<string> CastVoteAsync(string voteId, string optionId)
        {
            var payload = $"{{\"vote_id\":\"{voteId}\",\"option_id\":\"{optionId}\"}}";
            return SendAsync("vote.cast", payload);
        }

        public Task<string> CastVoteAsync(string voteId, string[] optionIds)
        {
            var ids = string.Join(",", Array.ConvertAll(optionIds, id => $"\"{id}\""));
            var payload = $"{{\"vote_id\":\"{voteId}\",\"option_id\":[{ids}]}}";
            return SendAsync("vote.cast", payload);
        }

        public Task<string> CastVetoAsync(string voteId)
        {
            var payload = $"{{\"vote_id\":\"{voteId}\"}}";
            return SendAsync("vote.veto", payload);
        }

        public Task<string> UpdatePresenceAsync(string status = "online")
        {
            var payload = JsonUtility.ToJson(new WsPresencePayload { status = status });
            return SendAsync("presence.update", payload);
        }

        // --- World ---

        public Task<string> WorldListAsync(string mode = null, bool? hasCapacity = null)
        {
            string payload;
            if (mode != null || hasCapacity.HasValue)
            {
                var parts = new System.Collections.Generic.List<string>();
                if (mode != null) parts.Add($"\"mode\":\"{mode}\"");
                if (hasCapacity.HasValue) parts.Add($"\"has_capacity\":{(hasCapacity.Value ? "true" : "false")}");
                payload = "{" + string.Join(",", parts) + "}";
            }
            else
            {
                payload = "{}";
            }
            return SendAsync("world.list", payload);
        }

        public Task<string> WorldCreateAsync(string mode)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerPayload { mode = mode });
            return SendAsync("world.create", payload);
        }

        public Task<string> WorldFindOrCreateAsync(string mode)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerPayload { mode = mode });
            return SendAsync("world.find_or_create", payload);
        }

        public Task<string> WorldJoinAsync(string worldId)
        {
            var payload = $"{{\"world_id\":\"{worldId}\"}}";
            return SendAsync("world.join", payload);
        }

        public Task<string> WorldLeaveAsync()
        {
            return SendAsync("world.leave", "{}");
        }

        /// <summary>
        /// Send one input to the world you are in. Fire-and-forget: the frame
        /// carries no <c>cid</c>, so a bad input draws an <c>error</c> frame
        /// with reason <c>invalid_payload</c> that nothing ties back to it.
        /// </summary>
        /// <param name="inputJson">
        /// The input map itself, as a JSON object. <c>world.input</c> takes the
        /// payload verbatim and, unlike <c>match.input</c>, does not JSON-decode
        /// an inner string, so a wrapped input reaches the zone still wrapped.
        /// Null or blank sends an empty map.
        /// </param>
        /// <param name="seq">
        /// A per-input sequence number your client increments, to opt into
        /// world.ack reconciliation; the server echoes back the highest seq it
        /// has consumed on <c>OnWorldAck</c>. Stamped as a top-level sibling of
        /// payload only when provided.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputJson"/> is not a JSON object. Thrown on the
        /// first offending frame rather than sent, because the server answers
        /// one with an uncorrelated <c>error</c> frame and nothing else.
        /// </exception>
        /// <remarks>
        /// <c>data</c> is reserved at the top level of the input map: a payload
        /// whose sole key is <c>data</c> holding an object is unwrapped to that
        /// object. Deprecated, and removed at the next protocol break, so name
        /// your fields anything else (widgrensit/asobi#478). A <c>data</c>
        /// alongside other keys, or one holding anything but an object, is
        /// forwarded verbatim.
        /// </remarks>
        public Task WorldInputAsync(string inputJson, long? seq = null)
        {
            return SendFireAndForget("world.input", WsFrame.WorldInputPayload(inputJson), seq);
        }


        /// <summary>
        /// Asks the server to re-send a complete baseline for one zone, after
        /// frames for it went missing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// asobi core v0.89.0 stamps three fields on the <c>world.tick</c>
        /// payload your <c>OnWorldTick</c> handler already receives in full:
        /// <c>zone</c> as <c>[x, y]</c>, <c>frame_seq</c>, and <c>kf</c>.
        /// </para>
        /// <para>
        /// <c>frame_seq</c> counts frames the zone has broadcast and never
        /// skips, so a jump by more than one means frames were lost. Do not use
        /// <c>tick</c> for this: it skips on the server's broadcast interval and
        /// is suppressed entirely on a tick that changed nothing, so a gap in it
        /// is ambiguous. Call this method with that zone's coordinates, once per
        /// gap. The reply is an ordinary <c>world.tick</c> for that zone with
        /// <c>kf</c> true, listing every entity it holds: replace that zone's
        /// entities with it rather than merging. Adopt it even when its
        /// <c>frame_seq</c> is LOWER than what you have seen, because a zone
        /// restart resets the sequence while the zone's identity does not change.
        /// </para>
        /// <para>
        /// <b>Key your entities on <c>zone</c>.</b> A player is subscribed to an
        /// interest ring of several zones at once, each an independent server
        /// process, and frames from two of them have no order relative to each
        /// other. A crossing emits <c>op: "r"</c> from the zone being left and
        /// <c>op: "a"</c> from the zone being entered, so merging every zone into
        /// one entity dictionary is last-writer-wins, and when the remove lands
        /// last the entity is gone for good. Sequence tracking cannot save you
        /// from that - both zones' sequences stay contiguous through it.
        /// </para>
        /// <para>
        /// This SDK does not detect the gap for you, and that is deliberate
        /// rather than an omission. It hands <c>OnWorldTick</c> the raw payload
        /// and parses none of it, so a detector here would have to string-scan
        /// the frame, and the only scanner available takes the first match in the
        /// whole document - an entity in <c>updates</c> carrying a field named
        /// <c>zone</c> or <c>frame_seq</c> would silently be read instead. You
        /// already parse the frame to use <c>updates</c> at all, so you are
        /// better placed to read the sequence than the SDK is.
        /// </para>
        /// <para>
        /// Rate limited server-side to twice per ten seconds per player: ask once
        /// per gap and wait for the keyframe rather than retrying. Requires asobi
        /// core v0.89.0 or later; an older server answers <c>unknown_type</c>.
        /// </para>
        /// </remarks>
        /// <param name="zoneX">The first element of the frame's <c>zone</c>.</param>
        /// <param name="zoneY">The second element of the frame's <c>zone</c>.</param>
        public Task WorldResyncAsync(long zoneX, long zoneY)
        {
            return SendFireAndForget("world.resync", WsFrame.WorldResyncPayload(zoneX, zoneY));
        }
        // --- DM ---

        public Task SendDmAsync(string recipientId, string content)
        {
            var payload = $"{{\"recipient_id\":\"{recipientId}\",\"content\":\"{content}\"}}";
            return SendFireAndForget("dm.send", payload);
        }

        public async Task DisconnectAsync()
        {
            _disconnectRequested = true;
            _reconnectCts?.Cancel();

            if (_ws == null) return;
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch { }
            _cts?.Cancel();
        }

        async Task<string> SendAsync(string type, string payloadJson)
        {
            var cid = Interlocked.Increment(ref _cidCounter).ToString();
            var tcs = new TaskCompletionSource<string>();
            _pending[cid] = tcs;

            var msg = WsFrame.Request(type, payloadJson, cid);
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);

            // Timeout after 10 seconds
            var timeout = Task.Delay(10000);
            var completed = await Task.WhenAny(tcs.Task, timeout);
            if (completed == timeout)
            {
                _pending.TryRemove(cid, out _);
                throw new TimeoutException($"WebSocket request '{type}' timed out");
            }

            return await tcs.Task;
        }

        async Task SendFireAndForget(string type, string payloadJson, long? seq = null)
        {
            var msg = WsFrame.FireAndForget(type, payloadJson, seq);
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            RaiseDisconnected(result.CloseStatusDescription);
                            ScheduleReconnect();
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    HandleMessage(sb.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                RaiseDisconnected(ex.Message);
                ScheduleReconnect();
            }
        }

        // Only reaches here on an unexpected close (server-initiated close
        // frame or a socket error) - DisconnectAsync() sets
        // _disconnectRequested first, so a game-initiated disconnect never
        // triggers a reconnect.
        void ScheduleReconnect()
        {
            if (_disconnectRequested || !AutoReconnect) return;

            if (_reconnectAttempts >= AsobiReconnectPolicy.MaxAttempts)
            {
                RaiseReconnectFailed();
                return;
            }

            var delay = AsobiReconnectPolicy.GetDelay(_reconnectAttempts);
            _reconnectAttempts++;
            RaiseReconnecting(_reconnectAttempts, AsobiReconnectPolicy.MaxAttempts);

            _reconnectCts = new CancellationTokenSource();
            _ = ReconnectAfterDelayAsync(delay, _reconnectCts.Token);
        }

        async Task ReconnectAfterDelayAsync(TimeSpan delay, CancellationToken ct)
        {
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_disconnectRequested || !AutoReconnect) return;

            try
            {
                await ConnectAsync();
            }
            catch
            {
                ScheduleReconnect();
            }
        }

        protected internal override void OnPendingResponse(string cid, string type, string raw)
        {
            if (!_pending.TryRemove(cid, out var tcs)) return;
            if (type == "rpc.ok" || type == "rpc.error")
            {
                var reply = RpcReply.Parse(type, raw);
                if (reply.IsError)
                    tcs.SetException(new AsobiRpcException(reply.Code, reply.Message, reply.DetailsJson));
                else
                    tcs.SetResult(reply.ResultJson);
            }
            else if (type == "error")
                tcs.SetException(new AsobiException(-1, raw));
            else
                tcs.SetResult(raw);
        }

        public void Dispose()
        {
            _disconnectRequested = true;
            _reconnectCts?.Cancel();
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
