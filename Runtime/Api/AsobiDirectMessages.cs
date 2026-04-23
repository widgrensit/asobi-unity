using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiDirectMessages
    {
        readonly AsobiClient _client;
        internal AsobiDirectMessages(AsobiClient client) => _client = client;

        public Task<DmSendResponse> SendAsync(string recipientId, string content)
        {
            var req = new SendDmRequest { recipient_id = recipientId, content = content };
            return _client.Http.Post<DmSendResponse>("/api/v1/dm", req);
        }

        public Task<DirectMessageListResponse> GetHistoryAsync(string playerId, int limit = 50)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            return _client.Http.Get<DirectMessageListResponse>($"/api/v1/dm/{playerId}/history", query);
        }
    }
}
