# Asobi.Core .NET dispatch tests

Standalone .NET test project for the engine-agnostic protocol dispatch
layer. Mirrors `AsobiCore` in `asobi-unreal`: the same code path Unity
runs is exercised here without any Unity license, so dispatch validation
runs on stock `ubuntu-24.04` in CI.

The single source file `Runtime/WebSocket/AsobiDispatcher.cs` is shared
via `<Compile Include="..\..\Runtime\..." Link="..."/>` in the csproj —
no copies. Fixtures under `Tests/Runtime/Resources/Fixtures/` are
referenced via `<None Include="..." CopyToOutputDirectory>` and loaded
at test time from `AppContext.BaseDirectory/Fixtures/`.

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
