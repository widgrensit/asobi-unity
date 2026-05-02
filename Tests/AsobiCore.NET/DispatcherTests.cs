using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Asobi.Tests
{
    public class DispatcherTests
    {
        static readonly Dictionary<string, string> Expected = new()
        {
            { "error", nameof(AsobiDispatcher.OnError) },
            { "session.connected", nameof(AsobiDispatcher.OnConnected) },
            { "session.heartbeat", nameof(AsobiDispatcher.OnHeartbeat) },
            { "match.state", nameof(AsobiDispatcher.OnMatchState) },
            { "match.matched", nameof(AsobiDispatcher.OnMatchmakerMatched) },
            { "match.joined", nameof(AsobiDispatcher.OnMatchJoined) },
            { "match.left", nameof(AsobiDispatcher.OnMatchLeft) },
            { "match.finished", nameof(AsobiDispatcher.OnMatchFinished) },
            { "match.matchmaker_expired", nameof(AsobiDispatcher.OnMatchmakerExpired) },
            { "match.matchmaker_failed", nameof(AsobiDispatcher.OnMatchmakerFailed) },
            { "match.vote_start", nameof(AsobiDispatcher.OnVoteStart) },
            { "match.vote_tally", nameof(AsobiDispatcher.OnVoteTally) },
            { "match.vote_result", nameof(AsobiDispatcher.OnVoteResult) },
            { "match.vote_vetoed", nameof(AsobiDispatcher.OnVoteVetoed) },
            { "matchmaker.queued", nameof(AsobiDispatcher.OnMatchmakerQueued) },
            { "matchmaker.removed", nameof(AsobiDispatcher.OnMatchmakerRemoved) },
            { "chat.joined", nameof(AsobiDispatcher.OnChatJoined) },
            { "chat.left", nameof(AsobiDispatcher.OnChatLeft) },
            { "chat.message", nameof(AsobiDispatcher.OnChatMessage) },
            { "dm.sent", nameof(AsobiDispatcher.OnDmSent) },
            { "dm.message", nameof(AsobiDispatcher.OnDmMessage) },
            { "presence.updated", nameof(AsobiDispatcher.OnPresenceUpdated) },
            { "notification.new", nameof(AsobiDispatcher.OnNotification) },
            { "vote.cast_ok", nameof(AsobiDispatcher.OnVoteCastOk) },
            { "vote.veto_ok", nameof(AsobiDispatcher.OnVoteVetoOk) },
            { "world.tick", nameof(AsobiDispatcher.OnWorldTick) },
            { "world.terrain", nameof(AsobiDispatcher.OnWorldTerrain) },
            { "world.list", nameof(AsobiDispatcher.OnWorldList) },
            { "world.joined", nameof(AsobiDispatcher.OnWorldJoined) },
            { "world.left", nameof(AsobiDispatcher.OnWorldLeft) },
            { "world.phase_changed", nameof(AsobiDispatcher.OnWorldPhaseChanged) },
            { "world.finished", nameof(AsobiDispatcher.OnWorldFinished) },
        };

        static IEnumerable<TestCaseData> FixtureCases()
        {
            foreach (var kv in Expected)
                yield return new TestCaseData(kv.Key, kv.Value).SetName($"Dispatches_{kv.Key}");
        }

        [Test, TestCaseSource(nameof(FixtureCases))]
        public void DispatchesFixtureToExpectedEvent(string wireType, string eventName)
        {
            var raw = LoadFixture(wireType);
            Assert.That(raw, Is.Not.Null.And.Not.Empty,
                $"fixture for '{wireType}' missing under Fixtures/");

            var dispatcher = new AsobiDispatcher();
            var fired = false;
            Subscribe(dispatcher, eventName, () => fired = true);

            dispatcher.HandleMessage(raw);

            Assert.That(fired, Is.True,
                $"'{wireType}' did not fire {eventName}");
        }

        [Test]
        public void EveryFixtureHasExpectedMapping()
        {
            var fixtures = LoadAllFixtureNames();
            Assert.That(fixtures.Count, Is.GreaterThan(0),
                "no fixtures loaded from Fixtures/");

            var unmapped = fixtures
                .Where(name => !Expected.ContainsKey(name))
                .ToList();

            Assert.That(unmapped, Is.Empty,
                "fixtures with no Expected mapping (add a dispatch case + entry): "
                + string.Join(", ", unmapped));
        }

        [Test]
        public void EveryExpectedHasFixture()
        {
            var fixtureNames = LoadAllFixtureNames().ToHashSet();

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
            var raw = "{\"type\":\"matchmaker.matched\",\"payload\":{\"match_id\":\"m1\"}}";

            var dispatcher = new AsobiDispatcher();
            var fired = false;
            dispatcher.OnMatchmakerMatched += _ => fired = true;
            dispatcher.HandleMessage(raw);

            Assert.That(fired, Is.True,
                "matchmaker.matched alias should still dispatch to OnMatchmakerMatched");
        }

        // ---- helpers ----

        static string FixtureDir =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures");

        static string LoadFixture(string wireType)
        {
            var path = Path.Combine(FixtureDir, wireType + ".json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        static List<string> LoadAllFixtureNames()
        {
            if (!Directory.Exists(FixtureDir)) return new List<string>();
            return Directory.GetFiles(FixtureDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
        }

        static void Subscribe(AsobiDispatcher dispatcher, string eventName, Action onFire)
        {
            var ev = typeof(AsobiDispatcher).GetEvent(eventName);
            Assert.That(ev, Is.Not.Null, $"AsobiDispatcher has no event named {eventName}");

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

            ev.AddEventHandler(dispatcher, handler);
        }
    }
}
