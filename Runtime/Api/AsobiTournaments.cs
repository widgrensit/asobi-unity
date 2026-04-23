using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiTournaments
    {
        readonly AsobiClient _client;
        internal AsobiTournaments(AsobiClient client) => _client = client;

        public Task<TournamentListResponse> ListAsync(string status = null, int? limit = null)
        {
            Dictionary<string, string> query = null;
            if (status != null || limit.HasValue)
            {
                query = new Dictionary<string, string>();
                if (status != null) query["status"] = status;
                if (limit.HasValue) query["limit"] = limit.Value.ToString();
            }
            return _client.Http.Get<TournamentListResponse>("/api/v1/tournaments", query);
        }

        public Task<Tournament> GetAsync(string tournamentId)
        {
            return _client.Http.Get<Tournament>($"/api/v1/tournaments/{tournamentId}");
        }

        public Task<AsobiResponse> JoinAsync(string tournamentId)
        {
            return _client.Http.Post<AsobiResponse>($"/api/v1/tournaments/{tournamentId}/join");
        }
    }
}
