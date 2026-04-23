# Unity smoke test

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
