using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiMatches
    {
        readonly AsobiClient _client;
        internal AsobiMatches(AsobiClient client) => _client = client;

        public Task<MatchListResponse> ListAsync()
        {
            return _client.Http.Get<MatchListResponse>("/api/v1/matches");
        }

        public Task<MatchRecord> GetAsync(string matchId)
        {
            return _client.Http.Get<MatchRecord>($"/api/v1/matches/{matchId}");
        }
    }
}
