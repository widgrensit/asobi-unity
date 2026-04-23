using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Asobi.Tests
{
    /// <summary>
    /// Smoke test against asobi-test-harness.
    ///
    /// Exercises the 3 canonical scenarios (auth + WS, matchmaker →
    /// match.matched, input → state) against a running harness at
    /// ASOBI_URL (default localhost:8080).
    ///
    /// Run locally:
    ///   Unity -batchmode -nographics -runTests -projectPath . \
    ///         -testPlatform PlayMode -testResults results.xml
    /// </summary>
    public class SmokeTest
    {
        private const string MatchMode = "smoke";
        private const int StartupTimeoutSec = 60;
        private const int MatchTimeoutSec = 10;
        private const int StateTimeoutSec = 3;

        [UnityTest]
        public IEnumerator RunsCanonicalFlow()
        {
            var task = RunFlow();
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception!;
        }

        private static async Task RunFlow()
        {
            var (host, port, useSsl) = ParseUrl(
                Environment.GetEnvironmentVariable("ASOBI_URL") ?? "http://localhost:8080"
            );
            Log($"Waiting for harness at {host}:{port}");
            await WaitForServer(host, port, useSsl);
            Log("Harness reachable.");

            var a = await SpawnPlayer("a", host, port);
            var b = await SpawnPlayer("b", host, port);
            Log($"Registered: {a.client.PlayerId} | {b.client.PlayerId}");

            // match.matched listeners BEFORE queueing to avoid races.
            var matchedA = new TaskCompletionSource<string>();
            var matchedB = new TaskCompletionSource<string>();
            a.client.Realtime.OnMatchmakerMatched += p => matchedA.TrySetResult(p.match_id);
            b.client.Realtime.OnMatchmakerMatched += p => matchedB.TrySetResult(p.match_id);

            await a.client.Matchmaker.AddAsync(MatchMode);
            await b.client.Matchmaker.AddAsync(MatchMode);
            Log("Both queued.");

            var bothMatched = Task.WhenAll(matchedA.Task, matchedB.Task);
            var timeout = Task.Delay(TimeSpan.FromSeconds(MatchTimeoutSec));
            var winner = await Task.WhenAny(bothMatched, timeout);
            if (winner == timeout)
                throw new Exception("timeout waiting for match.matched");

            if (matchedA.Task.Result != matchedB.Task.Result)
                throw new Exception($"match_id mismatch: {matchedA.Task.Result} vs {matchedB.Task.Result}");
            Log($"Both matched, match_id = {matchedA.Task.Result}");

            var stateTcs = new TaskCompletionSource<float>();
            a.client.Realtime.OnMatchState += state =>
            {
                if (state.players != null
                    && state.players.TryGetValue(a.client.PlayerId, out var me)
                    && me.x >= 1f)
                {
                    stateTcs.TrySetResult(me.x);
                }
            };

            a.client.Realtime.SendMatchInput(new MatchInput { move_x = 1, move_y = 0 });

            var stateTimeout = Task.Delay(TimeSpan.FromSeconds(StateTimeoutSec));
            var stateWinner = await Task.WhenAny(stateTcs.Task, stateTimeout);
            if (stateWinner == stateTimeout)
                throw new Exception("timeout waiting for match.state with input applied");

            Log($"match.state confirmed: x = {stateTcs.Task.Result}");
            a.client.Realtime.Disconnect();
            b.client.Realtime.Disconnect();
            Log("PASS");
        }

        // ---- helpers ----

        private readonly struct Player
        {
            public readonly AsobiClient client;
            public Player(AsobiClient c) { client = c; }
        }

        private static async Task<Player> SpawnPlayer(string label, string host, int port)
        {
            var client = new AsobiClient(host, port: port);
            var rng = new System.Random();
            var username = $"smoke_{label}_{DateTime.UtcNow.Ticks}_{rng.Next(10000)}";
            await client.Auth.RegisterAsync(username, "smoke_pw_12345", username);
            await client.Realtime.ConnectAsync();
            return new Player(client);
        }

        private static (string host, int port, bool useSsl) ParseUrl(string raw)
        {
            var uri = new Uri(raw);
            return (uri.Host,
                    uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80),
                    uri.Scheme == "https");
        }

        private static async Task WaitForServer(string host, int port, bool useSsl)
        {
            var deadline = DateTime.UtcNow.AddSeconds(StartupTimeoutSec);
            var scheme = useSsl ? "https" : "http";
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var res = await client.GetAsync($"{scheme}://{host}:{port}/api/v1/auth/register");
                    if ((int)res.StatusCode < 500) return;
                }
                catch { /* retry */ }
                await Task.Delay(1000);
            }
            throw new Exception($"harness never became reachable at {scheme}://{host}:{port}");
        }

        private static void Log(string msg) => Debug.Log($"[smoke] {msg}");
    }
}
