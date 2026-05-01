# asobi-unity

Unity client SDK for the [Asobi](https://github.com/widgrensit/asobi) game backend. Tested on Unity 2022.3 LTS and Unity 6.0 LTS, Mono and IL2CPP. WebGL is not supported (the SDK uses `System.Net.WebSockets.ClientWebSocket`).

## Installation

Add via Unity Package Manager using the git URL:

```
https://github.com/widgrensit/asobi-unity.git
```

*Window → Package Manager → + → Add package from git URL.*

## Run a backend first

The SDK talks to an Asobi server. The fastest way to get one is the canonical SDK demo backend:

```bash
git clone https://github.com/widgrensit/sdk_demo_backend
cd sdk_demo_backend && docker compose up -d
```

That serves at `http://localhost:8084` (HTTP + WebSocket on `/ws`) with a 2-player `demo` mode. For the full reference game (arena shooter) see [`asobi_arena_lua`](https://github.com/widgrensit/asobi_arena_lua).

## Quick Start

```csharp
using Asobi;
using UnityEngine;

var client = new AsobiClient("localhost", port: 8084);

// Register and connect
await client.Auth.RegisterAsync("player1", "secret123", "Player One");

// Realtime events deliver the raw WebSocket envelope JSON; deserialize
// with your preferred library (JsonUtility for simple shapes,
// Newtonsoft.Json for nested ones).
client.Realtime.OnMatchState += rawJson =>
    Debug.Log($"State: {rawJson}");

// match.matched (matchmaker push) and match.joined (reply to a
// client-initiated match.join) both signal "in a match — match.state
// will follow." Listen on the generic OnMatchEvent for the matchmade case.
client.Realtime.OnMatchEvent += (eventName, payloadJson) =>
{
    if (eventName == "matched")
        Debug.Log($"Matched: {payloadJson}");
};

await client.Realtime.ConnectAsync();
await client.Matchmaker.AddAsync("demo");
```

> ⚠️ **Threading**: realtime events fire on a background thread. Marshal to the main thread before touching `UnityEngine.Object` (see the demo's `UnityMainThread` helper). A future SDK version will own this dispatch.

See the [WebSocket protocol guide](https://github.com/widgrensit/asobi/blob/main/guides/websocket-protocol.md) for the full event surface.

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

## Build targets

| Target | Status |
|---|---|
| Standalone (Win/Mac/Linux) — Mono | ✓ |
| Standalone — IL2CPP | ✓ (ship a `link.xml` if your project uses managed-code stripping) |
| Android — IL2CPP | ✓ |
| iOS — IL2CPP | ✓ |
| WebGL | ✗ — `ClientWebSocket` is not supported on WebGL JS runtime |

## License

Apache-2.0
