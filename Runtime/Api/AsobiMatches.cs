using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiMatches
    {
        readonly AsobiClient _client;
        internal AsobiMatches(AsobiClient client) => _client = client;

        public async Task<MatchListResponse> ListAsync()
        {
            var raw = await _client.Http.GetRaw("/api/v1/matches");
            return JsonHelper.ParseMatchList(raw);
        }

        public async Task<MatchRecord> GetAsync(string matchId)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/matches/{matchId}");
            return JsonHelper.ParseMatchRecord(raw);
        }
    }
}
