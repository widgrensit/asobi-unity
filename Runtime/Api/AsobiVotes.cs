using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiVotes
    {
        readonly AsobiClient _client;
        internal AsobiVotes(AsobiClient client) => _client = client;

        public async Task<VoteListResponse> ListForMatchAsync(string matchId)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/matches/{matchId}/votes");
            return JsonHelper.ParseVoteList(raw);
        }

        public async Task<Vote> GetAsync(string voteId)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/votes/{voteId}");
            return JsonHelper.ParseVote(raw);
        }
    }
}
