using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiNotifications
    {
        readonly AsobiClient _client;
        internal AsobiNotifications(AsobiClient client) => _client = client;

        public Task<NotificationListResponse> ListAsync()
        {
            return _client.Http.Get<NotificationListResponse>("/api/v1/notifications");
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
