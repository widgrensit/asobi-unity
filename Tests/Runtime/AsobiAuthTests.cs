using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Asobi.Tests
{
    /// <summary>
    /// Unit tests for AsobiAuth over a fake IHttpClient. No backend, no
    /// network: they pin the request each method sends and the token-storage
    /// side effects, and that an error status aborts without storing tokens.
    /// </summary>
    public class AsobiAuthTests
    {
        FakeHttpClient _http;
        AsobiClient _client;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("asobi_refresh_token");
            _http = new FakeHttpClient();
            _client = new AsobiClient(new AsobiConfig("localhost"), _http);
        }

        [Test]
        public async Task LoginSendsCredentialsAndStoresTokens()
        {
            _http.NextResponse = new AuthResponse
            {
                player_id = "pid",
                access_token = "at",
                refresh_token = "rt",
                username = "bob"
            };

            var resp = await _client.Auth.LoginAsync("bob", "pw");

            Assert.That(_http.LastPath, Is.EqualTo("/api/v1/auth/login"));
            var body = (AuthRequest)_http.LastBody;
            Assert.That(body.username, Is.EqualTo("bob"));
            Assert.That(body.password, Is.EqualTo("pw"));

            Assert.That(resp.access_token, Is.EqualTo("at"));
            Assert.That(_client.AccessToken, Is.EqualTo("at"));
            Assert.That(_client.RefreshToken, Is.EqualTo("rt"));
            Assert.That(_client.PlayerId, Is.EqualTo("pid"));
            Assert.That(_http.AccessToken, Is.EqualTo("at"),
                "access token must propagate to the HTTP layer for authenticated calls");
        }

        [Test]
        public async Task RegisterDefaultsDisplayNameToUsername()
        {
            _http.NextResponse = new AuthResponse { access_token = "at", refresh_token = "rt" };

            await _client.Auth.RegisterAsync("bob", "pw");

            Assert.That(_http.LastPath, Is.EqualTo("/api/v1/auth/register"));
            var body = (AuthRequest)_http.LastBody;
            Assert.That(body.display_name, Is.EqualTo("bob"));
        }

        [Test]
        public async Task OAuthStoresTokens()
        {
            _http.NextResponse = new OAuthResponse
            {
                player_id = "pid",
                access_token = "at",
                refresh_token = "rt",
                created = true
            };

            var resp = await _client.Auth.OAuthAsync("google", "id-token");

            Assert.That(_http.LastPath, Is.EqualTo("/api/v1/auth/oauth"));
            var body = (OAuthRequest)_http.LastBody;
            Assert.That(body.provider, Is.EqualTo("google"));
            Assert.That(body.token, Is.EqualTo("id-token"));
            Assert.That(resp.created, Is.True);
            Assert.That(_client.AccessToken, Is.EqualTo("at"));
        }

        [Test]
        public void LoginErrorDoesNotStoreTokens()
        {
            _http.NextError = new AsobiException(401, "invalid_credentials",
                new AsobiError { error = "invalid_credentials" });

            var ex = Assert.ThrowsAsync<AsobiException>(
                async () => await _client.Auth.LoginAsync("bob", "wrong"));

            Assert.That(ex.StatusCode, Is.EqualTo(401));
            Assert.That(ex.Error.error, Is.EqualTo("invalid_credentials"));
            Assert.That(_client.AccessToken, Is.Null);
        }

        class FakeHttpClient : IHttpClient
        {
            public string AccessToken { get; set; }
            public string LastPath;
            public object LastBody;
            public object NextResponse;
            public AsobiException NextError;

            public Task<T> Post<T>(string path, object body = null)
            {
                LastPath = path;
                LastBody = body;
                if (NextError != null) throw NextError;
                return Task.FromResult((T)NextResponse);
            }

            public Task<T> Get<T>(string path, Dictionary<string, string> query = null) => throw new System.NotImplementedException();
            public Task<T> PostJson<T>(string path, string json) => throw new System.NotImplementedException();
            public Task<T> Put<T>(string path, object body = null) => throw new System.NotImplementedException();
            public Task<T> PutJson<T>(string path, string json) => throw new System.NotImplementedException();
            public Task<string> GetRaw(string path, Dictionary<string, string> query = null) => throw new System.NotImplementedException();
            public Task<string> PutRaw(string path, string json) => throw new System.NotImplementedException();
            public Task<AsobiResponse> Delete(string path, object body = null, Dictionary<string, string> query = null) => throw new System.NotImplementedException();
        }
    }
}
