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

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        internal AsobiRealtime(AsobiClient client) => _client = client;

        // Test-only: construct without a client/WebSocket so dispatch logic
        // can be exercised in isolation.
        internal AsobiRealtime() { }

        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            await _ws.ConnectAsync(new Uri(_client.Config.WsUrl), _cts.Token);
            _ = ReceiveLoop();

            var payload = JsonUtility.ToJson(new WsConnectPayload { token = _client.SessionToken });
            await SendAsync("session.connect", payload);
        }

        public Task<string> SendHeartbeatAsync()
        {
            return SendAsync("session.heartbeat", "{}");
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
            }
        }

        protected internal override void OnPendingResponse(string cid, string type, string raw)
        {
            if (!_pending.TryRemove(cid, out var tcs)) return;
            if (type == "error")
                tcs.SetException(new AsobiException(-1, raw));
            else
                tcs.SetResult(raw);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
