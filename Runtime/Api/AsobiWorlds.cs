using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiWorlds
    {
        readonly AsobiClient _client;
        internal AsobiWorlds(AsobiClient client) => _client = client;

        public Task<WorldListResponse> ListAsync(string mode = null, bool? hasCapacity = null)
        {
            Dictionary<string, string> query = null;
            if (mode != null || hasCapacity.HasValue)
            {
                query = new Dictionary<string, string>();
                if (mode != null)
                    query["mode"] = mode;
                if (hasCapacity.HasValue)
                    query["has_capacity"] = hasCapacity.Value.ToString().ToLower();
            }
            return _client.Http.Get<WorldListResponse>("/api/v1/worlds", query);
        }

        public Task<WorldInfo> GetAsync(string worldId)
        {
            return _client.Http.Get<WorldInfo>($"/api/v1/worlds/{worldId}");
        }

        public Task<WorldInfo> CreateAsync(string mode)
        {
            var req = new CreateWorldRequest { mode = mode };
            return _client.Http.Post<WorldInfo>("/api/v1/worlds", req);
        }
    }
}
