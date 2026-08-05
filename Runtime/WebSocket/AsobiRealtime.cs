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

        public Task WorldInputAsync(string data)
        {
            var payload = JsonUtility.ToJson(new WsMatchInputPayload { data = data });
            return SendFireAndForget("world.input", payload);
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

            var msg = $"{{\"type\":\"{type}\",\"payload\":{payloadJson},\"cid\":\"{cid}\"}}";
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

        async Task SendFireAndForget(string type, string payloadJson)
        {
            var msg = $"{{\"type\":\"{type}\",\"payload\":{payloadJson}}}";
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
