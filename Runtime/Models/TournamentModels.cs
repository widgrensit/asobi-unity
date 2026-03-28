using System;

namespace Asobi
{
    [Serializable]
    public class Tournament
    {
        public string id;
        public string name;
        public string leaderboard_id;
        public int max_entries;
        public string status;
        public string start_at;
        public string end_at;
        public string inserted_at;
    }

    [Serializable]
    public class TournamentListResponse
    {
        public Tournament[] tournaments;
    }
}
