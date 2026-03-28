using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Asobi
{
    public class AsobiRealtime : IDisposable
    {
        readonly AsobiClient _client;
        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        int _cidCounter;
        readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnMatchState;
        public event Action<string, string> OnMatchEvent;
        public event Action<string> OnChatMessage;
        public event Action<string> OnNotification;
        public event Action<string> OnMatchmakerMatched;
        public event Action<string> OnError;

        internal AsobiRealtime(AsobiClient client) => _client = client;

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

        public Task<string> AddToMatchmakerAsync(string mode = "default")
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerPayload { mode = mode });
            return SendAsync("matchmaker.add", payload);
        }

        public Task<string> RemoveFromMatchmakerAsync(string ticketId)
        {
            var payload = JsonUtility.ToJson(new WsMatchmakerRemovePayload { ticket_id = ticketId });
            return SendAsync("matchmaker.remove", payload);
        }

        public Task<string> UpdatePresenceAsync(string status = "online")
        {
            var payload = JsonUtility.ToJson(new WsPresencePayload { status = status });
            return SendAsync("presence.update", payload);
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
                            OnDisconnected?.Invoke(result.CloseStatusDescription);
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
                OnDisconnected?.Invoke(ex.Message);
            }
        }

        void HandleMessage(string raw)
        {
            var msg = JsonUtility.FromJson<WsMessage>(raw);
            if (msg == null) return;

            // Handle request/response via cid
            if (!string.IsNullOrEmpty(msg.cid) && _pending.TryRemove(msg.cid, out var tcs))
            {
                if (msg.type == "error")
                    tcs.SetException(new AsobiException(-1, msg.payload));
                else
                    tcs.SetResult(raw);
            }

            // Dispatch events
            switch (msg.type)
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
                case "matchmaker.matched":
                    OnMatchmakerMatched?.Invoke(raw);
                    break;
                case "error":
                    OnError?.Invoke(raw);
                    break;
                default:
                    if (msg.type != null && msg.type.StartsWith("match."))
                    {
                        var eventName = msg.type.Substring(6);
                        OnMatchEvent?.Invoke(eventName, raw);
                    }
                    break;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}
