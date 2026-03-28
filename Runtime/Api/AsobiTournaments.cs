using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiTournaments
    {
        readonly AsobiClient _client;
        internal AsobiTournaments(AsobiClient client) => _client = client;

        public Task<TournamentListResponse> ListAsync()
        {
            return _client.Http.Get<TournamentListResponse>("/api/v1/tournaments");
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
