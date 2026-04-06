using System;

namespace Asobi
{
    [Serializable]
    public class WorldInfo
    {
        public string world_id;
        public string status;
        public int player_count;
        public int max_players;
        public string mode;
        public int grid_size;
        public long started_at;
        public string[] players;
        public bool HasCapacity => player_count < max_players;
    }

    [Serializable]
    public class WorldListResponse
    {
        public WorldInfo[] worlds;
    }

    [Serializable]
    public class CreateWorldRequest
    {
        public string mode;
    }
}
