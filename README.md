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

### Guest / anonymous auth

Sign a player in without a username or password. A guest is a real player (with
a persistent `player_id`); the device holds a `{device_id, device_secret}`
keypair, and the same pair resumes the same player on every launch.

#### Guest device (recommended)

`GuestDeviceAsync` manages the keypair for you: it generates a device secret
(32 CSPRNG bytes, standard base64) on first run, persists it in `PlayerPrefs`,
reuses it after, and signs in.

```csharp
using Asobi;

var client = new AsobiClient("localhost", port: 8084);

var resp = await client.Auth.GuestDeviceAsync();
if (resp.created)
    Debug.Log($"New guest {resp.player_id}");        // run first-time onboarding
else
    Debug.Log($"Resumed guest {resp.player_id}");    // welcome back

// Turn the guest into a permanent account (same player_id, nothing lost).
var upgraded = await client.Auth.UpgradeGuestAsync("player1", "secret123");
Debug.Log($"Upgraded: {upgraded.upgraded}");
```

**Switch guest / "forget me"**: erase the stored keypair so the next
`GuestDeviceAsync` mints a brand-new guest. Local-only - pair it with
`LogoutAsync` to end the current session (or `UpgradeGuestAsync` first to keep
the guest).

```csharp
await client.Auth.LogoutAsync();
client.Auth.ClearGuestDevice();   // next launch starts fresh
```

**Delete the account**: `ClearGuestDevice` is local only - the account and its
data stay on the server. For an actual deletion, and for the in-app account
deletion the app stores require, erase it:

```csharp
await client.Players.EraseSelfAsync();               // guest or provider-only
await client.Players.EraseSelfAsync("secret123");    // account with a password
```

Irreversible. A wrong password throws `AsobiException` with
`Error.code == "player.confirmation_failed"` (403) and changes nothing. On
success the local session is cleared, because the server deleted the token pair
in the same transaction; anything afterwards on that session is a `401`, which
for a retried erase means it already worked.

Needs a server carrying `POST /api/v1/players/me/erase`; older ones answer 404.

**Custom storage or a stronger key source**: pass a `DeviceOptions` with your
own `IDeviceStore` (e.g. a file under `Application.persistentDataPath` or an OS
keychain) and/or a `RandomBytes` source.

```csharp
var opts = new DeviceOptions
{
    Store = new PlayerPrefsDeviceStore("mygame_guest"),
    RandomBytes = n => MyCsprng.GetBytes(n),   // must return >= n bytes
};
var resp = await client.Auth.GuestDeviceAsync(opts);
```

#### Bring your own credentials

If you would rather manage the keypair yourself, skip the helper and pass the
values to `GuestAsync` directly. `deviceSecret` must be standard base64 (RFC
4648, `+/` with `=` padding) of at least 32 random bytes; `deviceId` is any
stable per-install string.

```csharp
// deviceId + deviceSecret are yours to generate and persist.
var resp = await client.Auth.GuestAsync(deviceId, deviceSecret);
Debug.Log(resp.created ? $"New guest {resp.player_id}" : $"Resumed {resp.player_id}");
```

See the [WebSocket protocol guide](https://github.com/widgrensit/asobi/blob/main/guides/websocket-protocol.md) for the full event surface.

## Extensions (RPC)

Server extensions expose methods over the same socket:

```csharp
try
{
    var json = await client.Realtime.RpcAsync(
        "quests.claim", "{\"quest_key\":\"daily\"}");
    var claim = JsonUtility.FromJson<QuestClaim>(json);
    Debug.Log($"reward: {claim.reward}");
}
catch (AsobiRpcException e) when (e.Code == "quests.already_claimed")
{
    Debug.Log("already claimed today");
}
```

`RpcAsync` returns the `result` object as raw JSON — deserialize it into
whatever type the extension documents. Calls are correlated by cid, so several
can be in flight at once and may answer out of order.

On failure it throws `AsobiRpcException`. Branch on `Code`; `Message` is for
humans and may be reworded at any time. `DetailsJson` carries whatever the
extension attached, as raw JSON.

## Worlds (client-side prediction)

A world is a server-ticked room: join it with `WorldJoinAsync` (or
`WorldFindOrCreateAsync`), push inputs with `WorldInputAsync`, and accumulate
authoritative state from `OnWorldTick`.

`WorldInputAsync(string inputJson, long? seq = null)` sends a JSON object that
is itself the input map: the field names are your game's, and the server hands
the map to the world script as it stands. One field name is reserved. If the
map has a top-level `data`, the server substitutes it, so an object `data`
becomes the input and a `data` that is anything else leaves the input empty.

Stamp each input with `seq`, a monotonic counter your client owns, to opt into
acknowledgement. Buffer the input under that `seq` and apply it locally at once:

```csharp
long _seq;
readonly List<(long seq, string input)> _pending = new();

async Task SendInput(string inputJson)
{
    var seq = ++_seq;
    _pending.Add((seq, inputJson));
    Predict(inputJson);
    await client.Realtime.WorldInputAsync(inputJson, seq);
}
```

`seq` is the second parameter and optional; omit it and the frame goes out
unstamped. On the wire it is a top-level sibling of `payload`, never nested
inside it:

```json
{"type":"world.input","seq":412,"payload":{"kind":"move","x":600,"y":480}}
```

`OnWorldTick` carries deltas: `payload.updates` is a list of `{op, id, ...}`
entries, where `a` adds an entity with every field, `u` carries only the fields
that changed, and `r` removes it. A zone sends a full `a` snapshot of its
entities on every new subscription, meaning every time you are added to that
zone's subscriber set and not only the first time. At the default `view_radius`
of `1` you are subscribed to the 3x3 block of zones around you, so joining
delivers one frame per loaded, non-empty zone in that ring rather than a single
snapshot.

A crossing re-snapshots too. The server recomputes the ring, unsubscribes the
band that dropped out of it and subscribes the band that just entered, and each
newly subscribed zone replays its full snapshot. Only the destination zone is
exempt: at radius `1` it was already in the old ring, so re-subscribing to it
takes an idempotent no-op branch. Leaving the ring sends `r` for each of that
zone's entities, and walking back in re-subscribes you and replays the whole
snapshot, so a player oscillating across a boundary re-snapshots on every pass.

A zone holding no entities sends no snapshot, but the terrain push is a
separate, unconditional step, so a world with a terrain provider still delivers
that zone's chunk to `OnWorldTerrain`. Everything else is a delta, so
accumulate every tick into your own state map (the entries are heterogeneous,
so parse them with Newtonsoft.Json rather than `JsonUtility`):

```csharp
readonly Dictionary<string, Dictionary<string, object>> _state = new();

client.Realtime.OnWorldTick += raw =>
{
    foreach (var u in ParseUpdates(raw))       // payload.updates
    {
        if (u.op == "r") _state.Remove(u.id);
        else if (u.op == "a") _state[u.id] = u.fields;
        else foreach (var f in u.fields) _state[u.id][f.Key] = f.Value;
    }
};
```

The server answers with `world.ack`, carrying the highest `seq` it has consumed
for you as of `tick`:

```json
{"type":"world.ack","payload":{"tick":42,"seq":412}}
```

That mark is held per zone, not per connection, and every zone you are
subscribed to acks you independently. Inputs only ever reach the zone you are
standing in, so once you have crossed a boundary you get more than one
`world.ack` per broadcast: the zone you left keeps emitting the frozen mark it
recorded before you moved, so `payload.seq` can go backwards between
consecutive acks. Nothing in the frame says which zone sent it. Keep a running
maximum and ignore any ack that does not exceed it. Dropping everything at or
below `ack.seq` is only safe against a mark that never moves backwards; prune
straight from the received `seq` and you re-apply inputs the server has already
consumed. Tracked as
[widgrensit/asobi#477](https://github.com/widgrensit/asobi/issues/477), which
also covers the "per-connection" wording the server source and the protocol
guide still carry.

`OnWorldAck` hands you the raw envelope, so pull out `payload` before
deserializing into `WsWorldAckPayload` (`long tick`, `long seq`); passing the
envelope straight to `JsonUtility` yields zeros, not an error. Advance the
running maximum, drop every buffered input at or below it, rewind to the
accumulated state, replay the rest:

```csharp
long _acked = -1;

client.Realtime.OnWorldAck += raw =>
{
    var ack = JsonUtility.FromJson<WsWorldAckPayload>(
        JsonHelper.ExtractField(raw, "payload"));

    if (ack.seq <= _acked) return;
    _acked = ack.seq;

    _pending.RemoveAll(p => p.seq <= _acked);
    ResetTo(_state);
    foreach (var p in _pending)
        Predict(p.input);
};
```

That is the whole reconciliation loop. `Predict`, `ResetTo` and `ParseUpdates`
are your game's.

- The ack is a high-water mark, not a receipt per input. A rejected input still
  advances it, so an input the world script declines never strands the client.
- Prune and replay in the ack handler, never in the tick handler. A zone with
  deltas to report sends its `world.tick` first and its `world.ack` second, but
  a broadcast with nothing to report skips the tick entirely, so an ack can
  arrive with no `world.tick` in front of it.
- Acknowledgement is opt-in. The server records a `seq` only for players who
  stamp one, so a client that never stamps one gets no `world.ack` at all, and
  no error either.
- The ack never rides the shared `world.tick` broadcast. It is addressed to you
  alone, so the two always arrive as separate messages.
- `seq` must be a non-negative integer below 2^53, narrower than C#'s `long`:
  count up from zero, never seed from a nanosecond timestamp. An out-of-range
  `seq` is ignored, but the input is not. It is queued and applied to the world
  as normal, and only the acknowledgement is skipped; if you already have a
  valid `seq` on record the acks keep arriving every broadcast tick carrying
  that older high-water mark rather than falling silent.
- `broadcast_interval` is a world-level value copied into each zone's config,
  and one ticker per world fans a single shared tick number out to every zone,
  so zones are not on independent schedules. Every `broadcast_interval`
  simulation ticks, `3` by default, each subscribed zone emits its own pair, so
  a full 3x3 ring is up to nine `world.tick` frames and nine `world.ack` frames
  rather than one of each, and they land together on the same broadcast tick
  rather than interleaved across cadences. Subscription snapshots are sent
  immediately and ignore the interval. Set it to `1` in the world mode config
  for an ack every tick. See the
  [world server guide](https://asobi.dev/docs/world-server).
- Needs a server carrying `world.ack`, which is asobi core v0.84.0 or newer. An
  older one sends nothing, and the silence is the only symptom.
- `OnWorldAck` and the `seq` parameter arrived in asobi-unity v0.18.0, but on
  that release the loop above is dead. `WorldInputAsync` still wrapped the
  payload as `{"data":"..."}`, which the zone reads as an empty input map, so
  acks arrive and `seq` advances while nothing you send moves anything. The loop
  needs a release in which `WorldInputAsync` sends the payload verbatim, and
  v0.18.0 is not one. Confirm it on the wire: the input frame's `payload` should
  be your input map, not an object holding a single `data` string.
- `OnWorldAck` fires on a background thread like every other realtime event, so
  reconciliation that touches `UnityEngine.Object` must marshal first.

Frame reference: [client-side prediction](https://asobi.dev/docs/protocols/websocket#client-side-prediction).

## Features

- **Auth** — Register, login, guest (anonymous device create-or-resume + upgrade), OAuth, provider linking, token refresh
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
- **Extensions** — Call server extension methods over RPC

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
