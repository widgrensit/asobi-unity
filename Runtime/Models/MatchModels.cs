using System;

namespace Asobi
{
    [Serializable]
    public class MatchRecord
    {
        public string id;
        public string mode;
        public string status;
        public string result;
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
    public class LiveMatchPhase
    {
        public string status;
        public string phase;
        public long remaining_ms;
        public string start_condition;
    }

    [Serializable]
    public class LiveMatch
    {
        public string match_id;
        public string status;
        public int player_count;
        public int max_players;
        public string mode;
        public LiveMatchPhase phase;
        public bool HasCapacity => player_count < max_players;
    }

    [Serializable]
    public class LiveMatchListResponse
    {
        public LiveMatch[] matches;
    }

    [Serializable]
    public class MatchmakerRequest
    {
        public string mode;
        public string properties;
        public string[] party;
    }

    [Serializable]
    public class MatchmakerTicket
    {
        public string ticket_id;
        public string status;
    }
}
