using System;

namespace Asobi
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string leaderboard_id;
        public string player_id;
        public long score;
        public long sub_score;
        public int rank;
        public string metadata;
        public string updated_at;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public LeaderboardEntry[] entries;
    }

    [Serializable]
    public class ScoreSubmitRequest
    {
        public long score;
        public long sub_score;
    }
}
