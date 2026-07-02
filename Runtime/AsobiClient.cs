using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Asobi
{
    public class AsobiClient : IDisposable
    {
        public AsobiConfig Config { get; }
        public AsobiAuth Auth { get; }
        public AsobiPlayers Players { get; }
        public AsobiMatchmaker Matchmaker { get; }
        public AsobiMatches Matches { get; }
        public AsobiLeaderboards Leaderboards { get; }
        public AsobiEconomy Economy { get; }
        public AsobiInventory Inventory { get; }
        public AsobiSocial Social { get; }
        public AsobiTournaments Tournaments { get; }
        public AsobiNotifications Notifications { get; }
        public AsobiStorage Storage { get; }
        public AsobiIAP IAP { get; }
        public AsobiVotes Votes { get; }
        public AsobiWorlds Worlds { get; }
        public AsobiDirectMessages DirectMessages { get; }
        public AsobiRealtime Realtime { get; }

        internal HttpClient Http { get; }

        const string RefreshTokenPrefKey = "asobi.refresh_token";

        string _accessToken;
        string _refreshToken;

        public event Action OnAuthExpired;

        public string AccessToken
        {
            get => _accessToken;
            internal set
            {
                _accessToken = value;
                Http.AccessToken = value;
                if (!string.IsNullOrEmpty(value))
                    Realtime?.OnAccessTokenRotated();
            }
        }

        public string RefreshToken
        {
            get => _refreshToken;
            internal set
            {
                _refreshToken = value;
                if (string.IsNullOrEmpty(value))
                    PlayerPrefs.DeleteKey(RefreshTokenPrefKey);
                else
                    PlayerPrefs.SetString(RefreshTokenPrefKey, value);
                PlayerPrefs.Save();
            }
        }

        public string PlayerId { get; internal set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        public AsobiClient(string host, int port = 8084, bool useSsl = false)
            : this(new AsobiConfig(host, port, useSsl)) { }

        public AsobiClient(AsobiConfig config)
        {
            Config = config;
            Http = new HttpClient(config.BaseUrl);
            Http.RefreshHandler = HandleTokenRefreshAsync;
            _refreshToken = PlayerPrefs.HasKey(RefreshTokenPrefKey)
                ? PlayerPrefs.GetString(RefreshTokenPrefKey)
                : null;

            Auth = new AsobiAuth(this);
            Players = new AsobiPlayers(this);
            Matchmaker = new AsobiMatchmaker(this);
            Matches = new AsobiMatches(this);
            Leaderboards = new AsobiLeaderboards(this);
            Economy = new AsobiEconomy(this);
            Inventory = new AsobiInventory(this);
            Social = new AsobiSocial(this);
            Tournaments = new AsobiTournaments(this);
            Notifications = new AsobiNotifications(this);
            Storage = new AsobiStorage(this);
            IAP = new AsobiIAP(this);
            Votes = new AsobiVotes(this);
            Worlds = new AsobiWorlds(this);
            DirectMessages = new AsobiDirectMessages(this);
            Realtime = new AsobiRealtime(this);
        }

        async Task<bool> HandleTokenRefreshAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                SurfaceAuthExpired();
                return false;
            }

            try
            {
                await Auth.RefreshAsync();
                return true;
            }
            catch (AsobiException)
            {
                SurfaceAuthExpired();
                return false;
            }
        }

        void SurfaceAuthExpired()
        {
            AccessToken = null;
            RefreshToken = null;
            PlayerId = null;
            OnAuthExpired?.Invoke();
        }

        public void Dispose()
        {
            Realtime?.Dispose();
        }
    }

    public class AsobiConfig
    {
        public string Host { get; }
        public int Port { get; }
        public bool UseSsl { get; }
        public string BaseUrl { get; }
        public string WsUrl { get; }

        public AsobiConfig(string host, int port = 8084, bool useSsl = false)
        {
            Host = host;
            Port = port;
            UseSsl = useSsl;

            var scheme = useSsl ? "https" : "http";
            var wsScheme = useSsl ? "wss" : "ws";
            BaseUrl = $"{scheme}://{host}:{port}";
            WsUrl = $"{wsScheme}://{host}:{port}/ws";
        }
    }
}
