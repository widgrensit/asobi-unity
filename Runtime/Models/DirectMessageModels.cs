using System;

namespace Asobi
{
    [Serializable]
    public class DirectMessage
    {
        public string id;
        public string sender_id;
        public string recipient_id;
        public string content;
        public long sent_at;
    }

    [Serializable]
    public class DirectMessageListResponse
    {
        public DirectMessage[] messages;
        public string channel_id;
    }

    [Serializable]
    public class SendDmRequest
    {
        public string recipient_id;
        public string content;
    }

    [Serializable]
    public class DmSendResponse
    {
        public bool success;
        public string channel_id;
    }
}
