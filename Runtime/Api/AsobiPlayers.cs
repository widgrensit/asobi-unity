using System.Threading.Tasks;
using UnityEngine;

namespace Asobi
{
    public class AsobiPlayers
    {
        readonly AsobiClient _client;
        internal AsobiPlayers(AsobiClient client) => _client = client;

        public async Task<Player> GetAsync(string playerId)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/players/{playerId}");
            return JsonHelper.ParsePlayer(raw);
        }

        public async Task<Player> UpdateAsync(string playerId, PlayerUpdateRequest update)
        {
            var raw = await _client.Http.PutRaw($"/api/v1/players/{playerId}", JsonUtility.ToJson(update));
            return JsonHelper.ParsePlayer(raw);
        }

        public Task<Player> GetSelfAsync()
        {
            return GetAsync(_client.PlayerId);
        }
    }
}
