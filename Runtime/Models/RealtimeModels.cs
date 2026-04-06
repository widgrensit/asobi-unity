using System;

namespace Asobi
{
    [Serializable]
    public class WsMessage
    {
        public string type;
        public string payload;
        public string cid;
    }

    [Serializable]
    internal class WsConnectPayload
    {
        public string token;
    }

    [Serializable]
    internal class WsMatchInputPayload
    {
        public string data;
    }

    [Serializable]
    internal class WsChatSendPayload
    {
        public string channel_id;
        public string content;
    }

    [Serializable]
    internal class WsChatChannelPayload
    {
        public string channel_id;
    }

    [Serializable]
    internal class WsMatchJoinPayload
    {
        public string match_id;
    }

    [Serializable]
    internal class WsMatchmakerPayload
    {
        public string mode;
    }

    [Serializable]
    internal class WsMatchmakerAddPayload
    {
        public string mode;
        public string properties;
        public string[] party;
    }

    [Serializable]
    internal class WsMatchmakerRemovePayload
    {
        public string ticket_id;
    }

    [Serializable]
    internal class WsPresencePayload
    {
        public string status;
    }

    [Serializable]
    internal class WsWorldIdPayload
    {
        public string world_id;
    }

    [Serializable]
    internal class WsWorldListPayload
    {
        public string mode;
        public bool has_capacity;
    }

    [Serializable]
    internal class WsDmSendPayload
    {
        public string recipient_id;
        public string content;
    }

    [Serializable]
    public class WsConnectedPayload
    {
        public string player_id;
    }

    [Serializable]
    public class WsMatchStatePayload
    {
        public string data;
    }

    [Serializable]
    public class WsChatMessagePayload
    {
        public string channel_id;
        public string sender_id;
        public string content;
        public string sent_at;
    }
}
