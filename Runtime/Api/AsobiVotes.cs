using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiVotes
    {
        readonly AsobiClient _client;
        internal AsobiVotes(AsobiClient client) => _client = client;

        public async Task<string> ListForMatchAsync(string matchId)
        {
            return await _client.Http.GetRaw($"/api/v1/matches/{matchId}/votes");
        }

        public async Task<string> GetAsync(string voteId)
        {
            return await _client.Http.GetRaw($"/api/v1/votes/{voteId}");
        }
    }
}
