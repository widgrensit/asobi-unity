using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiPlayers
    {
        readonly AsobiClient _client;
        internal AsobiPlayers(AsobiClient client) => _client = client;

        public Task<Player> GetAsync(string playerId)
        {
            return _client.Http.Get<Player>($"/api/v1/players/{playerId}");
        }

        public Task<Player> UpdateAsync(string playerId, PlayerUpdateRequest update)
        {
            return _client.Http.Put<Player>($"/api/v1/players/{playerId}", update);
        }

        public Task<Player> GetSelfAsync()
        {
            return GetAsync(_client.PlayerId);
        }
    }
}
