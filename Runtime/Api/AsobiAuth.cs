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
            _client.SessionToken = resp.session_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            var req = new AuthRequest { username = username, password = password };
            var resp = await _client.Http.Post<AuthResponse>("/api/v1/auth/login", req);
            _client.SessionToken = resp.session_token;
            _client.PlayerId = resp.player_id;
            return resp;
        }

        public async Task<OAuthResponse> OAuthAsync(string provider, string token)
        {
            var req = new OAuthRequest { provider = provider, token = token };
            var resp = await _client.Http.Post<OAuthResponse>("/api/v1/auth/oauth", req);
            _client.SessionToken = resp.session_token;
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
            var req = new RefreshRequest { session_token = _client.SessionToken };
            var resp = await _client.Http.Post<RefreshResponse>("/api/v1/auth/refresh", req);
            _client.SessionToken = resp.session_token;
            return resp;
        }

        public void Logout()
        {
            _client.SessionToken = null;
            _client.PlayerId = null;
        }
    }
}
