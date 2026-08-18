using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Asobi.Tests
{
    // Unit tests for the binary world.tick decoder (asobi ADR 0013).
    //
    // Driven entirely by asobi's own committed fixture corpus under
    // Fixtures/wire: real bytes from the real encoder, with a manifest saying
    // what each one decodes to. Nothing here is hand-rolled test data, which is
    // the point - a decoder checked only against a fixture the same author
    // invented proves the two agree with each other and nothing about whether
    // either matches the server.
    public class AsobiWireTests
    {
        static string WireDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "wire");

        static readonly Dictionary<string, string> OpShort = new Dictionary<string, string>
        {
            { "add", "a" }, { "update", "u" }, { "remove", "r" },
        };

        static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(WireDir, name + ".bin"));

        static JsonElement Manifest() =>
            JsonDocument.Parse(File.ReadAllText(Path.Combine(WireDir, "manifest.json"))).RootElement;

        static IEnumerable<TestCaseData> FixtureCases()
        {
            foreach (var entry in Manifest().EnumerateArray())
            {
                var name = entry.GetProperty("name").GetString();
                yield return new TestCaseData(name).SetName($"Decodes_{name}");
            }
        }

        [Test, TestCaseSource(nameof(FixtureCases))]
        public void DecodesFixtureToWhatTheManifestDescribes(string name)
        {
            var entry = Manifest().EnumerateArray()
                .First(e => e.GetProperty("name").GetString() == name);
            var expected = entry.GetProperty("frame");
            var bytes = Bytes(name);
            Assert.That(bytes.Length, Is.EqualTo(entry.GetProperty("bytes").GetInt32()));

            // A fresh decoder per fixture: the corpus cases are independent frames,
            // not a stream, so sharing slot tables between them would be the test
            // lying to itself.
            var frame = new AsobiWire().Decode(bytes, bytes.Length);
            Assert.That(frame, Is.Not.Null, "decoded to nothing");

            var zone = expected.GetProperty("zone").EnumerateArray().Select(z => z.GetInt64()).ToArray();
            Assert.That(frame.ZoneX, Is.EqualTo(zone[0]));
            Assert.That(frame.ZoneY, Is.EqualTo(zone[1]));
            Assert.That(frame.Tick, Is.EqualTo(expected.GetProperty("tick").GetInt64()));

            // An ungated frame holds no position in the zone's stream and says so by
            // having no frame_seq at all. Reporting 0 instead would have every client
            // past its first frame discard the one message that clears its ghosts.
            if (expected.GetProperty("kind").GetString() == "sequenced")
            {
                Assert.That(frame.FrameSeq, Is.EqualTo(expected.GetProperty("frame_seq").GetInt64()));
                Assert.That(frame.Kf, Is.EqualTo(expected.GetProperty("kf").GetBoolean()));
            }
            else
            {
                Assert.That(frame.FrameSeq, Is.Null, "an ungated frame must carry no frame_seq");
            }

            var records = expected.GetProperty("records").EnumerateArray().ToList();
            Assert.That(frame.Records.Count, Is.EqualTo(records.Count));

            for (var i = 0; i < records.Count; i++)
            {
                var want = records[i];
                var have = frame.Records[i];
                Assert.That(have.Op, Is.EqualTo(OpShort[want.GetProperty("op").GetString()]));
                if (want.TryGetProperty("id", out var id))
                    Assert.That(have.Id, Is.EqualTo(id.GetString()));
                // The generation. A decoder that skipped the byte shifts every later
                // offset and fails loudly; one that read it from the wrong place
                // would not, so pin the value.
                Assert.That(have.Gen, Is.EqualTo(want.GetProperty("gen").GetByte()));

                if (!want.TryGetProperty("fields", out var fields)) continue;
                foreach (var field in fields.EnumerateObject())
                {
                    Assert.That(have.Fields.ContainsKey(field.Name), Is.True, field.Name);
                    var got = have.Fields[field.Name];
                    switch (field.Value.ValueKind)
                    {
                        case JsonValueKind.Number:
                            // float32 on the wire against a float64 in the manifest,
                            // so compare with a tolerance: 12.5 survives exactly,
                            // 1.5 * 7 does not.
                            Assert.That(Convert.ToDouble(got),
                                Is.EqualTo(field.Value.GetDouble()).Within(0.0001), field.Name);
                            break;
                        case JsonValueKind.True:
                            Assert.That(got, Is.True, field.Name);
                            break;
                        case JsonValueKind.False:
                            Assert.That(got, Is.False, field.Name);
                            break;
                        case JsonValueKind.Null:
                            Assert.That(got, Is.Null, field.Name);
                            break;
                        default:
                            Assert.That(got, Is.EqualTo(field.Value.GetString()), field.Name);
                            break;
                    }
                }
            }
        }

        // The reason the slot table lives in the decoder: an update carries the slot
        // alone, and the caller must still see the entity id it saw on the add.
        [Test]
        public void SlotBindingsAreScopedPerZone()
        {
            var decoder = new AsobiWire();
            var kf = decoder.Decode(Bytes("keyframe_all_adds"), Bytes("keyframe_all_adds").Length);
            var ids = kf.Records.Select(r => r.Id).ToHashSet();
            Assert.That(ids, Is.Not.Empty);
            Assert.That(ids.Contains(null), Is.False, "a keyframe's adds must all carry ids");

            // The keyframe is zone [-1, -1]. A frame for a DIFFERENT zone must not
            // resolve against its table: slot 1 in one zone has nothing to do with
            // slot 1 in another, and aliasing them is the corruption per-zone tables
            // exist to prevent.
            var other = decoder.Decode(Bytes("removes_only"), Bytes("removes_only").Length);
            foreach (var record in other.Records)
                Assert.That(ids.Contains(record.Id), Is.False, "a slot resolved across zones");
        }

        // Bindings belong to one connection's stream of adds. Kept across a reconnect
        // they would attach stale ids to slots the server has since reassigned.
        [Test]
        public void ResetForgetsEveryBinding()
        {
            var decoder = new AsobiWire();
            decoder.Decode(Bytes("keyframe_all_adds"), Bytes("keyframe_all_adds").Length);
            decoder.Reset();
            var after = decoder.Decode(Bytes("removes_only"), Bytes("removes_only").Length);
            foreach (var record in after.Records)
                Assert.That(record.Id, Is.Null);
        }

        // These bytes come off the network and this runs on the receive loop, where a
        // throw would tear down the socket.
        [Test]
        public void MalformedFramesReturnNullRatherThanThrowing()
        {
            var good = Bytes("steady_state_40_updates");
            var cases = new Dictionary<string, byte[]>
            {
                { "empty", Array.Empty<byte>() },
                { "one byte", new byte[] { 1 } },
                { "truncated envelope", good.Take(10).ToArray() },
                { "truncated mid-record", good.Take(good.Length - 2).ToArray() },
                { "trailing junk", good.Concat(new byte[] { 0, 0, 0 }).ToArray() },
                { "unknown kind byte", new byte[] { 9 }.Concat(good.Skip(1)).ToArray() },
            };
            foreach (var kv in cases)
                Assert.That(new AsobiWire().Decode(kv.Value, kv.Value.Length), Is.Null, kv.Key);
        }

        // The steady state, and the number the whole design turns on: it has to fit
        // one datagram, and it has to be decisively smaller than the JSON it replaces.
        [Test]
        public void TheSteadyStateDeltaFitsADatagram()
        {
            Assert.That(Bytes("steady_state_40_updates").Length, Is.LessThanOrEqualTo(1200));
        }
    }
}
