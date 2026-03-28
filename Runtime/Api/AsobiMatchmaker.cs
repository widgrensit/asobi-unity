using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiMatchmaker
    {
        readonly AsobiClient _client;
        internal AsobiMatchmaker(AsobiClient client) => _client = client;

        public Task<MatchmakerTicket> AddAsync(string mode = "default")
        {
            var req = new MatchmakerRequest { mode = mode };
            return _client.Http.Post<MatchmakerTicket>("/api/v1/matchmaker", req);
        }

        public Task<MatchmakerTicket> StatusAsync(string ticketId)
        {
            return _client.Http.Get<MatchmakerTicket>($"/api/v1/matchmaker/{ticketId}");
        }

        public Task<AsobiResponse> CancelAsync(string ticketId)
        {
            return _client.Http.Delete($"/api/v1/matchmaker/{ticketId}");
        }
    }
}
