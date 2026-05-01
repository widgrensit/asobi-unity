# Unity tests

## DispatchTests

`DispatchTests.cs` is a pure-unit dispatch test. Feeds every canonical server-emitted message envelope (vendored under `Resources/Fixtures/` from `asobi/priv/protocol/fixtures`) through `AsobiRealtime.HandleMessage` and asserts the matching event fires. Catches doc-vs-server drift before any user reports a silent failure.

It runs in PlayMode (where the `Tests/Runtime` asmdef lives) but uses `[Test]` (not `[UnityTest]`) and does not require a backend or a scene.

## SmokeTest

`SmokeTest.cs` exercises the 3 canonical scenarios (auth + WS, matchmaker → match.matched, input → state) against [asobi-test-harness](https://github.com/widgrensit/asobi-test-harness). It's a Unity Test Framework `UnityTest`.

## Running locally

1. Start the harness:

   ```bash
   git clone https://github.com/widgrensit/asobi-test-harness.git
   cd asobi-test-harness && docker compose up -d
   ```

2. Open a Unity project that references the `com.asobi.client` package (for example, create a new 2D project and add this repo via the Package Manager git URL). Make sure your `manifest.json` has:

   ```json
   "com.asobi.client": "https://github.com/widgrensit/asobi-unity.git"
   ```

3. In Unity, open **Window → General → Test Runner**, select **PlayMode**, find `Asobi.Tests.SmokeTest.RunsCanonicalFlow`, and run it.

Alternatively from the CLI:

```bash
Unity -batchmode -nographics -runTests \
      -projectPath /path/to/test-project \
      -testPlatform PlayMode \
      -testResults results.xml
```

## CI status

No CI job yet. Running Unity Test Framework in CI requires:
- A Unity license (Personal or Pro), activated via `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` secrets.
- The `game-ci/unity-test-runner` action, wired to a test-only Unity project that pulls this package.

Defer to a follow-up — the smoke test itself is kept parity with the other SDKs so the CI wrap-up is straightforward when ready.

## Canonical scenarios

See [widgrensit/asobi-test-harness/scenarios/canonical.md](https://github.com/widgrensit/asobi-test-harness/blob/main/scenarios/canonical.md).
