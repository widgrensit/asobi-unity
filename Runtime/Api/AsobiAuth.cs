using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiAuth
    {
        readonly AsobiClient _client;
        internal AsobiAuth(AsobiClient client) => _client = client;

        public async Task<AuthResponse> RegisterAsync(string username, string password, string displayName = null)
        {
            var req = new AuthRequest
            {
                username = username,
                password = password,
                display_name = displayName ?? username
            };
            var resp = await _client.Http.Post<AuthResponse>("/api/v1/auth/register", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            var req = new AuthRequest { username = username, password = password };
            var resp = await _client.Http.Post<AuthResponse>("/api/v1/auth/login", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<AuthResponse> GuestAsync(string deviceId, string deviceSecret)
        {
            var req = new GuestRequest { device_id = deviceId, device_secret = deviceSecret };
            var resp = await _client.Http.Post<AuthResponse>("/api/v1/auth/guest", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<AuthResponse> UpgradeGuestAsync(string username, string password)
        {
            var req = new GuestUpgradeRequest { username = username, password = password };
            var resp = await _client.Http.Post<AuthResponse>("/api/v1/auth/guest/upgrade", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<OAuthResponse> OAuthAsync(string provider, string token)
        {
            var req = new OAuthRequest { provider = provider, token = token };
            var resp = await _client.Http.Post<OAuthResponse>("/api/v1/auth/oauth", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<LinkResponse> LinkProviderAsync(string provider, string token)
        {
            var req = new OAuthRequest { provider = provider, token = token };
            return await _client.Http.Post<LinkResponse>("/api/v1/auth/link", req);
        }

        public Task<AsobiResponse> UnlinkProviderAsync(string provider)
        {
            var query = new System.Collections.Generic.Dictionary<string, string>
            {
                { "provider", provider }
            };
            return _client.Http.Delete("/api/v1/auth/unlink", query: query);
        }

        public async Task<RefreshResponse> RefreshAsync()
        {
            if (string.IsNullOrEmpty(_client.RefreshToken))
                throw new AsobiException(-1, "No refresh token available");
            var req = new RefreshRequest { refresh_token = _client.RefreshToken };
            var resp = await _client.Http.Post<RefreshResponse>("/api/v1/auth/refresh", req);
            _client.AccessToken = resp.access_token;
            _client.RefreshToken = resp.refresh_token;
            return resp;
        }

        /// <summary>
        /// Logs out on the server and clears local session state.
        /// </summary>
        /// <remarks>
        /// POST /api/v1/auth/logout revokes the whole refresh-token family and
        /// the presented access token. Clearing the fields locally does not:
        /// the refresh token stays valid for its full lifetime, so anyone
        /// holding it can mint new sessions after the user has "logged out".
        ///
        /// Local state is cleared even if the request fails, so a user can
        /// still log out while offline.
        /// </remarks>
        public async Task LogoutAsync()
        {
            var refreshToken = _client.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    var req = new RefreshRequest { refresh_token = refreshToken };
                    await _client.Http.Post<AsobiResponse>("/api/v1/auth/logout", req);
                }
                catch (AsobiException)
                {
                    // Already-revoked or unreachable server must not strand the
                    // user in a logged-in client.
                }
            }
            ClearSession();
        }

        [System.Obsolete(
            "Logout() only clears local tokens - the server-side refresh family stays valid. Use LogoutAsync()."
        )]
        public void Logout() => ClearSession();

        void ClearSession()
        {
            _client.AccessToken = null;
            _client.RefreshToken = null;
            _client.PlayerId = null;
        }
    }
}
