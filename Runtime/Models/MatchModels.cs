using System;

namespace Asobi
{
    [Serializable]
    public class MatchRecord
    {
        public string id;
        public string mode;
        public string status;
        public string started_at;
        public string finished_at;
        public string inserted_at;
    }

    [Serializable]
    public class MatchListResponse
    {
        public MatchRecord[] matches;
    }

    [Serializable]
    public class MatchmakerRequest
    {
        public string mode;
    }

    [Serializable]
    public class MatchmakerTicket
    {
        public string ticket_id;
        public string status;
    }
}
