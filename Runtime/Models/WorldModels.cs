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

    [Serializable]
    public class WorldTerrainChunk
    {
        public int[] coords;
        public string data;

        public int CoordX => coords != null && coords.Length > 0 ? coords[0] : 0;
        public int CoordY => coords != null && coords.Length > 1 ? coords[1] : 0;
    }
}
