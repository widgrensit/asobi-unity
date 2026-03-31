# asobi-unity

Unity client SDK for the [Asobi](https://asobi.dev) game backend. Unity 2021.3+, pure C#.

## Installation

Add via Unity Package Manager using the git URL:

```
https://github.com/widgrensit/asobi-unity.git
```

Window > Package Manager > + > Add package from git URL...

## Quick Start

```csharp
using Asobi;

var client = new AsobiClient("localhost", port: 8080);

// Auth
await client.Auth.Register("player1", "secret123");
await client.Auth.Login("player1", "secret123");

// REST APIs
var player = await client.Players.GetSelf();
var top = await client.Leaderboards.GetTop("weekly");
var wallets = await client.Economy.GetWallets();

// Real-time
client.Realtime.OnMatchState += (state) => Debug.Log($"Tick: {state["tick"]}");
await client.Realtime.ConnectAsync();
await client.Realtime.AddToMatchmaker("arena");
```

## Features

- **Auth** - Register, login, token refresh
- **Players** - Profiles, updates
- **Matchmaker** - Queue, status, cancel
- **Matches** - List, details
- **Leaderboards** - Top scores, around player, submit
- **Economy** - Wallets, store, purchases
- **Inventory** - Items, consume
- **Social** - Friends, groups, chat history
- **Tournaments** - List, join
- **Notifications** - List, read, delete
- **Storage** - Cloud saves, key-value
- **IAP** - In-app purchase receipt validation
- **Realtime** - WebSocket for matches, chat, presence, matchmaking

## API Reference

All API modules are accessible from `AsobiClient`:

| Property | Class |
|----------|-------|
| `Auth` | `AsobiAuth` |
| `Players` | `AsobiPlayers` |
| `Matchmaker` | `AsobiMatchmaker` |
| `Matches` | `AsobiMatches` |
| `Leaderboards` | `AsobiLeaderboards` |
| `Economy` | `AsobiEconomy` |
| `Inventory` | `AsobiInventory` |
| `Social` | `AsobiSocial` |
| `Tournaments` | `AsobiTournaments` |
| `Notifications` | `AsobiNotifications` |
| `Storage` | `AsobiStorage` |
| `IAP` | `AsobiIAP` |
| `Realtime` | `AsobiRealtime` |

## Demo

See [asobi-unity-demo](https://github.com/widgrensit/asobi-unity-demo) for a complete example project.

## License

Apache-2.0
