using System;

namespace Asobi
{
    [Serializable]
    public class Notification
    {
        public string id;
        public string player_id;
        public string type;
        public string subject;
        public string content;
        public bool read;
        public string sent_at;
    }

    [Serializable]
    public class NotificationListResponse
    {
        public Notification[] notifications;
    }
}
