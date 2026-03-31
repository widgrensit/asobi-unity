# asobi-unity

Unity client SDK for the [Asobi](https://github.com/widgrensit/asobi) game backend. Requires Unity 2021.3+.

## Installation

Add via Unity Package Manager using the git URL:

```
https://github.com/widgrensit/asobi-unity.git
```

Window > Package Manager > + > Add package from git URL.

## Quick Start

```csharp
using Asobi;

var client = new AsobiClient("localhost", port: 8080);

// Register & login
await client.Auth.RegisterAsync("player1", "secret123", "Player One");
await client.Auth.LoginAsync("player1", "secret123");

// REST APIs
var player = await client.Players.GetSelfAsync();
var top = await client.Leaderboards.GetTopAsync("weekly");
var wallets = await client.Economy.GetWalletsAsync();

// Matchmaking
var ticket = await client.Matchmaker.AddAsync("arena");

// Real-time
client.Realtime.OnMatchState += state => Debug.Log($"Tick: {state.tick}");
client.Realtime.Connect();
```

## Features

- **Auth** — Register, login, OAuth, provider linking, token refresh
- **Players** — Profiles, updates
- **Matchmaker** — Queue, status, cancel
- **Matches** — List, details
- **Leaderboards** — Top scores, around player, submit
- **Economy** — Wallets, store, purchases
- **Inventory** — Items, consume
- **Social** — Friends, groups, chat history
- **Tournaments** — List, join
- **Notifications** — List, read, delete
- **Storage** — Cloud saves, key-value
- **IAP** — In-app purchase receipt validation
- **Realtime** — WebSocket with events for matches, chat, presence, matchmaking

## License

Apache-2.0
