using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiMatches
    {
        readonly AsobiClient _client;
        internal AsobiMatches(AsobiClient client) => _client = client;

        public async Task<MatchListResponse> ListAsync(string mode = null, string status = null, int? limit = null)
        {
            Dictionary<string, string> query = null;
            if (mode != null || status != null || limit.HasValue)
            {
                query = new Dictionary<string, string>();
                if (mode != null) query["mode"] = mode;
                if (status != null) query["status"] = status;
                if (limit.HasValue) query["limit"] = limit.Value.ToString();
            }
            var raw = await _client.Http.GetRaw("/api/v1/matches", query);
            return JsonHelper.ParseMatchList(raw);
        }

        public async Task<MatchRecord> GetAsync(string matchId)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/matches/{matchId}");
            return JsonHelper.ParseMatchRecord(raw);
        }
    }
}
