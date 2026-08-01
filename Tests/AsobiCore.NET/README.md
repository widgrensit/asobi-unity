# Asobi.Core .NET dispatch tests

Standalone .NET test project for the engine-agnostic protocol dispatch
layer. Mirrors `AsobiCore` in `asobi-unreal`: the same code path Unity
runs is exercised here without any Unity license, so dispatch validation
runs on stock `ubuntu-24.04` in CI.

Source files are shared via `<Compile Include="..\..\Runtime\..." Link="..."/>`
in the csproj — no copies:
- `Runtime/WebSocket/AsobiDispatcher.cs`
- `Runtime/WebSocket/AsobiReconnectPolicy.cs`
- `Runtime/Models/AuthModels.cs`, `Runtime/Models/RealtimeModels.cs`
- `Runtime/DeviceCredential.cs`

Fixtures under `Tests/Runtime/Resources/Fixtures/` are referenced via
`<None Include="..." CopyToOutputDirectory>` and loaded at test time
from `AppContext.BaseDirectory/Fixtures/`.

## Reconnection coverage

`AsobiRealtime.cs` (the WebSocket lifecycle + reconnect loop) depends on
`UnityEngine.JsonUtility` and can't be linked into this plain .NET
project, so it isn't exercised here. What's covered instead:

- `ReconnectPolicyTests.cs` — the pure exponential-backoff math
  (`AsobiReconnectPolicy.GetDelay`): 1s base doubling to a 512s ceiling
  across the 10-attempt cap, extracted into its own dependency-free file
  specifically so it's headlessly testable.
- `ReconnectDispatchTests.cs` — the `OnReconnecting`/`OnReconnectFailed`
  event wiring on `AsobiDispatcher`.

**Untestable without Unity PlayMode:** the actual reconnect loop in
`AsobiRealtime` — that an unexpected close (server close frame or
`WebSocketException`) triggers `ScheduleReconnect()`, that a
game-initiated `DisconnectAsync()` suppresses it via
`_disconnectRequested`, that `AutoReconnect = false` opts out, that the
attempt counter resets to 0 on a successful reconnect, and that
`OnReconnecting`/`OnReconnectFailed` actually fire during a live retry
sequence. Covering that requires a real (or fake) `ClientWebSocket`
transport, which only exists in the PlayMode suite under `Tests/Runtime/`
today.

## Run

```sh
dotnet test Tests/AsobiCore.NET/Asobi.Core.Tests.csproj
```

## What it covers

- 32 fixture cases from `asobi/priv/protocol/fixtures` — every wire
  envelope the server emits is fed through `AsobiDispatcher.HandleMessage`
  and the matching event must fire.
- `EveryFixtureHasExpectedMapping` / `EveryExpectedHasFixture` pin the
  bijection between the fixture set and the dispatcher's switch cases.
- `MatchmakerMatchedAliasesMatchMatched` pins the historical
  `matchmaker.matched` alias.
