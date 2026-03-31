using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiNotifications
    {
        readonly AsobiClient _client;
        internal AsobiNotifications(AsobiClient client) => _client = client;

        public async Task<NotificationListResponse> ListAsync()
        {
            var raw = await _client.Http.GetRaw("/api/v1/notifications");
            return JsonHelper.ParseNotificationList(raw);
        }

        public Task<AsobiResponse> MarkReadAsync(string notificationId)
        {
            return _client.Http.Put<AsobiResponse>($"/api/v1/notifications/{notificationId}/read");
        }

        public Task<AsobiResponse> DeleteAsync(string notificationId)
        {
            return _client.Http.Delete($"/api/v1/notifications/{notificationId}");
        }
    }
}
