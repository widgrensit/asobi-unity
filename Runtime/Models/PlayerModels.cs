using System;

namespace Asobi
{
    [Serializable]
    public class Player
    {
        public string id;
        public string username;
        public string display_name;
        public string avatar_url;
        public string banned_at;
        public string inserted_at;
        public string updated_at;
    }

    [Serializable]
    public class PlayerStats
    {
        public string player_id;
        public int games_played;
        public int wins;
        public int losses;
        public float rating;
        public float rating_dev;
        public string updated_at;
    }

    [Serializable]
    public class PlayerUpdateRequest
    {
        public string display_name;
        public string avatar_url;
    }
}
