using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Asobi.Tests
{
    /// <summary>
    /// Per-SDK protocol dispatch unit test.
    ///
    /// Feeds every canonical server-emitted message envelope through the
    /// SDK's WebSocket message handler and asserts the right SDK event
    /// fires. Catches doc-vs-server drift (silent failures) before any
    /// user reports a missed callback.
    ///
    /// Fixtures live under Tests/Runtime/Resources/Fixtures/*.json and are
    /// vendored from asobi/priv/protocol/fixtures (the canonical corpus).
    /// </summary>
    public class DispatchTests
    {
        // Maps server wire `type` -> the event name on AsobiRealtime that
        // should fire for that fixture. Must stay in sync with the asobi
        // protocol fixture corpus and the dispatch switch in
        // AsobiRealtime.HandleMessage.
        static readonly Dictionary<string, string> Expected = new()
        {
            { "error", nameof(AsobiRealtime.OnError) },
            { "session.connected", nameof(AsobiRealtime.OnConnected) },
            { "session.heartbeat", nameof(AsobiRealtime.OnHeartbeat) },
            { "match.state", nameof(AsobiRealtime.OnMatchState) },
            { "match.matched", nameof(AsobiRealtime.OnMatchmakerMatched) },
            { "match.joined", nameof(AsobiRealtime.OnMatchJoined) },
            { "match.left", nameof(AsobiRealtime.OnMatchLeft) },
            { "match.finished", nameof(AsobiRealtime.OnMatchFinished) },
            { "match.matchmaker_expired", nameof(AsobiRealtime.OnMatchmakerExpired) },
            { "match.matchmaker_failed", nameof(AsobiRealtime.OnMatchmakerFailed) },
            { "match.vote_start", nameof(AsobiRealtime.OnVoteStart) },
            { "match.vote_tally", nameof(AsobiRealtime.OnVoteTally) },
            { "match.vote_result", nameof(AsobiRealtime.OnVoteResult) },
            { "match.vote_vetoed", nameof(AsobiRealtime.OnVoteVetoed) },
            { "matchmaker.queued", nameof(AsobiRealtime.OnMatchmakerQueued) },
            { "matchmaker.removed", nameof(AsobiRealtime.OnMatchmakerRemoved) },
            { "chat.joined", nameof(AsobiRealtime.OnChatJoined) },
            { "chat.left", nameof(AsobiRealtime.OnChatLeft) },
            { "chat.message", nameof(AsobiRealtime.OnChatMessage) },
            { "dm.sent", nameof(AsobiRealtime.OnDmSent) },
            { "dm.message", nameof(AsobiRealtime.OnDmMessage) },
            { "presence.updated", nameof(AsobiRealtime.OnPresenceUpdated) },
            { "notification.new", nameof(AsobiRealtime.OnNotification) },
            { "vote.cast_ok", nameof(AsobiRealtime.OnVoteCastOk) },
            { "vote.veto_ok", nameof(AsobiRealtime.OnVoteVetoOk) },
            { "world.tick", nameof(AsobiRealtime.OnWorldTick) },
            { "world.terrain", nameof(AsobiRealtime.OnWorldTerrain) },
            { "world.list", nameof(AsobiRealtime.OnWorldList) },
            { "world.joined", nameof(AsobiRealtime.OnWorldJoined) },
            { "world.left", nameof(AsobiRealtime.OnWorldLeft) },
            { "world.phase_changed", nameof(AsobiRealtime.OnWorldPhaseChanged) },
            { "world.finished", nameof(AsobiRealtime.OnWorldFinished) },
        };

        static IEnumerable<TestCaseData> FixtureCases()
        {
            foreach (var kv in Expected)
            {
                yield return new TestCaseData(kv.Key, kv.Value).SetName($"Dispatches_{kv.Key}");
            }
        }

        [Test, TestCaseSource(nameof(FixtureCases))]
        public void DispatchesFixtureToExpectedEvent(string wireType, string eventName)
        {
            var raw = LoadFixture(wireType);
            Assert.That(raw, Is.Not.Null.And.Not.Empty,
                $"fixture for '{wireType}' missing under Resources/Fixtures/");

            var realtime = new AsobiRealtime();
            var fired = false;
            Subscribe(realtime, eventName, () => fired = true);

            realtime.HandleMessage(raw);

            Assert.That(fired, Is.True,
                $"'{wireType}' did not fire {eventName}");
        }

        [Test]
        public void EveryFixtureHasExpectedMapping()
        {
            var fixtures = Resources.LoadAll<TextAsset>("Fixtures");
            Assert.That(fixtures.Length, Is.GreaterThan(0),
                "no fixtures loaded from Resources/Fixtures/");

            var unmapped = fixtures
                .Select(f => f.name)
                .Where(name => !Expected.ContainsKey(name))
                .ToList();

            Assert.That(unmapped, Is.Empty,
                "fixtures with no Expected mapping (add a dispatch case + entry): "
                + string.Join(", ", unmapped));
        }

        [Test]
        public void EveryExpectedHasFixture()
        {
            var fixtureNames = Resources.LoadAll<TextAsset>("Fixtures")
                .Select(f => f.name)
                .ToHashSet();

            var stale = Expected.Keys
                .Where(t => !fixtureNames.Contains(t))
                .ToList();

            Assert.That(stale, Is.Empty,
                "Expected entries with no fixture (stale or missing fixture): "
                + string.Join(", ", stale));
        }

        [Test]
        public void MatchmakerMatchedAliasesMatchMatched()
        {
            // Server only emits "match.matched"; "matchmaker.matched" is a
            // historical alias kept defensively. This test pins that alias
            // until it's removed in a future major.
            var raw = "{\"type\":\"matchmaker.matched\",\"payload\":{\"match_id\":\"m1\"}}";

            var realtime = new AsobiRealtime();
            var fired = false;
            realtime.OnMatchmakerMatched += _ => fired = true;
            realtime.HandleMessage(raw);

            Assert.That(fired, Is.True,
                "matchmaker.matched alias should still dispatch to OnMatchmakerMatched");
        }

        // ---- helpers ----

        static Dictionary<string, string> _fixtureCache;

        static string LoadFixture(string wireType)
        {
            if (_fixtureCache == null)
            {
                _fixtureCache = Resources.LoadAll<TextAsset>("Fixtures")
                    .ToDictionary(t => t.name, t => t.text);
            }
            return _fixtureCache.TryGetValue(wireType, out var raw) ? raw : null;
        }

        static void Subscribe(AsobiRealtime realtime, string eventName, Action onFire)
        {
            // Reflectively attach a handler to the named event so the test
            // stays data-driven. Supports the two delegate shapes used by
            // AsobiRealtime: Action and Action<string>.
            var ev = typeof(AsobiRealtime).GetEvent(eventName);
            Assert.That(ev, Is.Not.Null, $"AsobiRealtime has no event named {eventName}");

            var handlerType = ev.EventHandlerType;

            Delegate handler;
            if (handlerType == typeof(Action))
            {
                handler = onFire;
            }
            else if (handlerType == typeof(Action<string>))
            {
                Action<string> wrapped = _ => onFire();
                handler = wrapped;
            }
            else
            {
                throw new InvalidOperationException(
                    $"event {eventName} has unsupported delegate type {handlerType}");
            }

            ev.AddEventHandler(realtime, handler);
        }
    }
}
