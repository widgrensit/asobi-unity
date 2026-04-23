using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiNotifications
    {
        readonly AsobiClient _client;
        internal AsobiNotifications(AsobiClient client) => _client = client;

        public async Task<NotificationListResponse> ListAsync(bool? read = null, int? limit = null)
        {
            Dictionary<string, string> query = null;
            if (read.HasValue || limit.HasValue)
            {
                query = new Dictionary<string, string>();
                if (read.HasValue) query["read"] = read.Value.ToString().ToLower();
                if (limit.HasValue) query["limit"] = limit.Value.ToString();
            }
            var raw = await _client.Http.GetRaw("/api/v1/notifications", query);
            return JsonHelper.ParseNotificationList(raw);
        }

        public async Task<Notification> MarkReadAsync(string notificationId)
        {
            var raw = await _client.Http.PutRaw($"/api/v1/notifications/{notificationId}/read", "{}");
            return JsonHelper.ParseNotification(raw);
        }

        public Task<AsobiResponse> DeleteAsync(string notificationId)
        {
            return _client.Http.Delete($"/api/v1/notifications/{notificationId}");
        }
    }
}
