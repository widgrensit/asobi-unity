using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiLeaderboards
    {
        readonly AsobiClient _client;
        internal AsobiLeaderboards(AsobiClient client) => _client = client;

        public Task<LeaderboardResponse> GetTopAsync(string leaderboardId, int limit = 100)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            return _client.Http.Get<LeaderboardResponse>($"/api/v1/leaderboards/{leaderboardId}", query);
        }

        public Task<LeaderboardResponse> GetAroundPlayerAsync(string leaderboardId, string playerId, int range = 5)
        {
            var query = new Dictionary<string, string> { { "range", range.ToString() } };
            return _client.Http.Get<LeaderboardResponse>(
                $"/api/v1/leaderboards/{leaderboardId}/around/{playerId}", query);
        }

        public Task<LeaderboardResponse> GetAroundSelfAsync(string leaderboardId, int range = 5)
        {
            return GetAroundPlayerAsync(leaderboardId, _client.PlayerId, range);
        }

        public Task<LeaderboardEntry> SubmitScoreAsync(string leaderboardId, long score, long subScore = 0)
        {
            var req = new ScoreSubmitRequest { score = score, sub_score = subScore };
            return _client.Http.Post<LeaderboardEntry>($"/api/v1/leaderboards/{leaderboardId}", req);
        }
    }
}
