using System;
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

        string _sessionToken;

        public string SessionToken
        {
            get => _sessionToken;
            internal set
            {
                _sessionToken = value;
                Http.SessionToken = value;
            }
        }

        public string PlayerId { get; internal set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(SessionToken);

        public AsobiClient(string host, int port = 8084, bool useSsl = false)
            : this(new AsobiConfig(host, port, useSsl)) { }

        public AsobiClient(AsobiConfig config)
        {
            Config = config;
            Http = new HttpClient(config.BaseUrl);

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
