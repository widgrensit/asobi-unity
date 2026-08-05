using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Asobi.Tests
{
    public class DispatcherTests
    {
        static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };

        // Answers correlated by cid rather than fired as an event: the reply
        // goes to the caller that made the request, so there is no event to
        // assert. Listed so a new fixture still has to be accounted for, and
        // covered for real in RpcTests.
        const string Correlated = "<correlated by cid>";

        static readonly Dictionary<string, string> Expected = new()
        {
            { "rpc.ok", Correlated },
            { "rpc.error", Correlated },
            // Every WS request in this SDK goes out through SendAsync and gets
            // its answer back on the cid, so a match.list reply would never be
            // an event even once the request exists. It does not yet: this SDK
            // has no realtime match listing, unlike defold/godot/love2d.
            { "match.list", Correlated },
            { "error", nameof(AsobiDispatcher.OnError) },
            { "game.error", nameof(AsobiDispatcher.OnGameError) },
            { "game.message", nameof(AsobiDispatcher.OnGameMessage) },
            { "module.error", nameof(AsobiDispatcher.OnGameError) },
            { "module.message", nameof(AsobiDispatcher.OnGameMessage) },
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

            if (eventName == Correlated)
            {
                Assert.Pass($"{wireType} is correlated by cid - see RpcTests");
                return;
            }

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

        [Test]
        public void GameErrorDispatchesWithFields()
        {
            var raw = LoadFixture("game.error");
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "fixture for 'game.error' missing under Fixtures/");

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameError += payload => received = payload;

            dispatcher.HandleMessage(raw);

            Assert.That(received, Is.Not.Null, "game.error did not fire OnGameError");

            // OnGameError hands callers the full raw envelope (type/payload/
            // cid), not just the payload - WsMessage.payload is typed
            // `string` (JsonUtility can't target a nested object at
            // runtime), so this test-only envelope type targets
            // WsGameErrorPayload directly instead. Deserializing is what
            // would actually break if WsGameErrorPayload's fields were
            // renamed; the substring match this replaced only checked the
            // raw envelope text and would have kept passing regardless.
            var envelope = JsonSerializer.Deserialize<GameErrorEnvelope>(received, Opts);
            Assert.That(envelope, Is.Not.Null);
            Assert.That(envelope.payload, Is.Not.Null);
            Assert.That(envelope.payload.callback, Is.EqualTo("handle_input"));
            Assert.That(envelope.payload.script, Is.EqualTo("match.lua"));
            Assert.That(envelope.payload.message, Is.EqualTo("bad arithmetic + on nil, 1"));
        }

        class GameErrorEnvelope
        {
            public WsGameErrorPayload payload;
        }

        [Test]
        public void GameMessageDispatchesStringPayload()
        {
            var raw = LoadFixture("game.message");
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "fixture for 'game.message' missing under Fixtures/");

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameMessage += payload => received = payload;

            dispatcher.HandleMessage(raw);

            Assert.That(received, Is.Not.Null, "game.message did not fire OnGameMessage");

            var envelope = JsonSerializer.Deserialize<GameMessageEnvelope>(received, Opts);
            Assert.That(envelope, Is.Not.Null);
            Assert.That(envelope.payload, Is.Not.Null);
            var message = (JsonElement)envelope.payload.message;
            Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(message.GetString(), Is.EqualTo("jij bent speler nummer 3"));
        }

        // game.send/2 accepts any Lua value as the message (string, number,
        // table) - the server wraps it in {"message": <value>} specifically
        // so it isn't constrained to a string. Prove numeric and nested-
        // object payloads round-trip via JsonElement instead of throwing or
        // getting coerced/truncated.
        [Test]
        public void GameMessageDispatchesNumberPayload()
        {
            var raw = "{\"type\":\"game.message\",\"payload\":{\"message\":42}}";

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameMessage += payload => received = payload;

            dispatcher.HandleMessage(raw);

            var envelope = JsonSerializer.Deserialize<GameMessageEnvelope>(received, Opts);
            var message = (JsonElement)envelope.payload.message;
            Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(message.GetInt32(), Is.EqualTo(42));
        }

        [Test]
        public void GameMessageDispatchesObjectPayload()
        {
            var raw = "{\"type\":\"game.message\",\"payload\":{\"message\":{\"score\":3,\"team\":\"red\"}}}";

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameMessage += payload => received = payload;

            dispatcher.HandleMessage(raw);

            var envelope = JsonSerializer.Deserialize<GameMessageEnvelope>(received, Opts);
            var message = (JsonElement)envelope.payload.message;
            Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(message.GetProperty("score").GetInt32(), Is.EqualTo(3));
            Assert.That(message.GetProperty("team").GetString(), Is.EqualTo("red"));
        }

        [Test]
        public void GameMessageDispatchesArrayPayload()
        {
            var raw = "{\"type\":\"game.message\",\"payload\":{\"message\":[1,2,3]}}";

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameMessage += payload => received = payload;

            dispatcher.HandleMessage(raw);

            var envelope = JsonSerializer.Deserialize<GameMessageEnvelope>(received, Opts);
            var message = (JsonElement)envelope.payload.message;
            Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.Array));
            var items = message.EnumerateArray().Select(e => e.GetInt32()).ToList();
            Assert.That(items, Is.EqualTo(new List<int> { 1, 2, 3 }));
        }

        // game.send(player_id, nil) is a valid production call (e.g. a
        // script clearing a previously-sent value) - the server wraps it as
        // {"message": null} rather than omitting the field. For an
        // `object`-typed member, System.Text.Json deserializes a JSON null
        // straight to a C# null (not a JsonElement with ValueKind.Null), so
        // this asserts against `Is.Null` rather than casting to JsonElement.
        [Test]
        public void GameMessageDispatchesNullPayload()
        {
            var raw = "{\"type\":\"game.message\",\"payload\":{\"message\":null}}";

            var dispatcher = new AsobiDispatcher();
            string received = null;
            dispatcher.OnGameMessage += payload => received = payload;

            dispatcher.HandleMessage(raw);

            var envelope = JsonSerializer.Deserialize<GameMessageEnvelope>(received, Opts);
            Assert.That(envelope.payload, Is.Not.Null);
            Assert.That(envelope.payload.message, Is.Null);
        }

        class GameMessageEnvelope
        {
            public WsGameMessagePayload payload;
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
