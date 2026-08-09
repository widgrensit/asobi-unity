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

        /// <summary>
        /// Erases the signed-in account and everything the server holds for it -
        /// saves, storage, inventory, wallets, leaderboard entries, identities.
        /// Irreversible.
        /// </summary>
        /// <param name="password">
        /// Required only for an account that has one. A guest or a
        /// provider-only account has no credential the client can re-present,
        /// so its session is the whole confirmation: pass null.
        /// </param>
        /// <remarks>
        /// Clears the local session on success only, deliberately unlike
        /// <c>LogoutAsync</c> which clears regardless. A refused confirmation
        /// (403 <c>player.confirmation_failed</c>) or a credential change
        /// mid-flight (409) leaves a live account whose session must survive.
        /// On success the server deleted the token pair inside the erase
        /// transaction, so keeping it would only buy a doomed refresh.
        ///
        /// Needs a server carrying <c>POST /api/v1/players/me/erase</c>; older
        /// ones answer 404.
        /// </remarks>
        public async Task EraseSelfAsync(string password = null)
        {
            // PostJson, not Post: JsonUtility has no way to omit a field, so a
            // model with a null password would put {"password":""} on the wire
            // for every guest. Harmless server-side today, but it says the
            // client tried to confirm and failed, which is not what happened.
            var json = string.IsNullOrEmpty(password)
                ? "{}"
                : JsonUtility.ToJson(new EraseAccountRequest { password = password });
            await _client.Http.PostJson<AsobiResponse>("/api/v1/players/me/erase", json);
            _client.AccessToken = null;
            _client.RefreshToken = null;
            _client.PlayerId = null;
        }
    }
}
