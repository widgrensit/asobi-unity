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

    // Dev-mode only: sent when a Lua game-script callback errors while
    // handling this player's input (ASOBI_DEV_ERRORS=true server-side).
    [Serializable]
    public class WsGameErrorPayload
    {
        public string callback;
        public string script;
        public string message;
    }

    // Sent unconditionally in production whenever a Lua game script calls
    // game.send(player_id, message). `message` is whatever value the
    // script passed - string, number, or table (JSON object/array) - so
    // it's typed `object` rather than `string` to avoid throwing or
    // silently truncating on non-string payloads. Deserializing with
    // System.Text.Json boxes an object-typed member as a JsonElement -
    // cast to JsonElement to inspect ValueKind/GetString/GetInt32/etc.
    //
    // Unity's JsonUtility does NOT support this: it has no notion of an
    // untyped/polymorphic field, so it silently drops `message` (no
    // exception, no warning - the field is just left at its default null).
    // JsonUtility.FromJson<WsGameMessagePayload>(...) will therefore never
    // populate `message`. Unity/JsonUtility consumers should instead call
    // JsonHelper.ExtractField(payloadJson, "message") to get the raw JSON
    // text for the field, then JsonUtility.FromJson<T> that slice (for
    // object/array payloads) or parse the scalar text directly.
    [Serializable]
    public class WsGameMessagePayload
    {
        public object message;
    }
}
