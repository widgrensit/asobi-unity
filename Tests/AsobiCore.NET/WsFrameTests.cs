using NUnit.Framework;

namespace Asobi.Tests
{
    // The outbound half of client-side prediction. AsobiRealtime needs Unity
    // and is not linked here, so both the envelope text and the world.input
    // payload decision live in WsFrame and are asserted on their own.
    public class WsFrameTests
    {
        [Test]
        public void AnUnstampedFrameCarriesNoSeqAtAll()
        {
            Assert.That(
                WsFrame.FireAndForget("world.input", "{\"kind\":\"move\"}"),
                Is.EqualTo("{\"type\":\"world.input\",\"payload\":{\"kind\":\"move\"}}"));
        }

        // The server reads `seq` beside `payload`. Nested inside it there is no
        // seq to read, so the input still applies but nothing acks it.
        [Test]
        public void SeqIsASiblingOfPayloadNotAFieldInIt()
        {
            Assert.That(
                WsFrame.FireAndForget("world.input", "{\"kind\":\"move\"}", 412),
                Is.EqualTo("{\"type\":\"world.input\",\"seq\":412,\"payload\":{\"kind\":\"move\"}}"));
        }

        // world.input takes the payload verbatim as the input map, so nothing
        // may wrap or re-encode what the caller passed.
        [Test]
        public void ThePayloadReachesTheWireUntouched()
        {
            const string input = "{\"kind\":\"move\",\"x\":600,\"y\":480}";
            Assert.That(
                WsFrame.FireAndForget("world.input", input, 0),
                Is.EqualTo("{\"type\":\"world.input\",\"seq\":0,\"payload\":" + input + "}"));
        }

        // A correlated request: OnPendingResponse matches the reply on `cid`,
        // so it is a sibling of payload, never a field inside it.
        [Test]
        public void ARequestCarriesItsCidBesidePayload()
        {
            Assert.That(
                WsFrame.Request("match.join", "{\"match_id\":\"m-1\"}", "7"),
                Is.EqualTo("{\"type\":\"match.join\",\"payload\":{\"match_id\":\"m-1\"},\"cid\":\"7\"}"));
        }

        [Test]
        public void AnEmptyRequestPayloadIsStillAnObject()
        {
            Assert.That(
                WsFrame.Request("world.leave", "{}", "12"),
                Is.EqualTo("{\"type\":\"world.leave\",\"payload\":{},\"cid\":\"12\"}"));
        }

        // --- world.input payload ---

        // Nothing to send is an empty input map, not an empty payload: the
        // payload position always has to hold an object.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n")]
        public void AnAbsentInputBecomesAnEmptyMap(string input)
        {
            Assert.That(WsFrame.WorldInputPayload(input), Is.EqualTo("{}"));
        }

        // The whole point of the fix: the caller's object is the input map, so
        // it goes out exactly as written, never wrapped in {"data":"..."}.
        [Test]
        public void AnObjectInputIsPassedThroughVerbatim()
        {
            const string input = "{\"kind\":\"move\",\"x\":600,\"y\":480}";
            Assert.That(WsFrame.WorldInputPayload(input), Is.EqualTo(input));
        }

        // `data` is reserved at the top level (widgrensit/asobi#478): the
        // server unwraps a payload whose sole key is `data` holding an object.
        // The SDK sends it verbatim either way - quietly renaming or re-nesting
        // it here would hide the server's rule instead of letting the game hit it.
        [Test]
        public void ATopLevelDataKeyIsStillSentVerbatim()
        {
            const string input = "{\"data\":{\"kind\":\"move\"},\"x\":600}";
            Assert.That(WsFrame.WorldInputPayload(input), Is.EqualTo(input));
        }

        // Leading whitespace is legal JSON; the object behind it is what counts.
        [Test]
        public void LeadingWhitespaceBeforeAnObjectIsAccepted()
        {
            const string input = "  {\"kind\":\"move\"}";
            Assert.That(WsFrame.WorldInputPayload(input), Is.EqualTo(input));
        }

        // Anything that is not an object either splices onto the wire as a
        // malformed frame or draws an invalid_payload error frame. world.input
        // has no cid, so neither can be correlated back: throw on the developer's
        // first frame instead of failing silently for the life of the game.
        [TestCase("not json")]
        [TestCase("[1,2,3]")]
        [TestCase("  [1,2,3]")]
        [TestCase("null")]
        [TestCase("42")]
        [TestCase("\"move\"")]
        public void ANonObjectInputIsRejected(string input)
        {
            Assert.That(() => WsFrame.WorldInputPayload(input), Throws.ArgumentException);
        }

        [Test]
        public void AMalformedOrTrailingPayloadIsRejected()
        {
            // Every one of these passed the old first-character guard.
            foreach (var bad in new[]
                     {
                     "{",                      // unterminated
                     "{\"a\":1",              // unterminated with content
                     "{}}",                    // trailing brace
                     "{},\"cid\":\"9\"",       // injects a cid
                     "{},\"seq\":999",         // injects a seq the client never stamped
                     "{\"a\":1} trailing",     // trailing text
                     "\u00A0{\"a\":1}",        // non-breaking space is not JSON whitespace
                     "{\"a\":\"unterminated}"  // brace inside an unterminated string
                 })
            {
                Assert.That(
                    () => WsFrame.WorldInputPayload(bad),
                    Throws.ArgumentException,
                    $"expected rejection for: {bad}");
            }
        }

        [Test]
        public void BracesInsideStringsDoNotUnbalanceTheScan()
        {
            const string payload = "{\"chat\":\"} not the end {\",\"x\":1}";
            Assert.That(WsFrame.WorldInputPayload(payload), Is.EqualTo(payload));

            const string escaped = "{\"quote\":\"a \\\" brace } here\"}";
            Assert.That(WsFrame.WorldInputPayload(escaped), Is.EqualTo(escaped));
        }

        [Test]
        public void NestedObjectsAndTrailingJsonWhitespaceAreAccepted()
        {
            Assert.That(WsFrame.WorldInputPayload("{\"a\":{\"b\":[1,2]}}"), Is.EqualTo("{\"a\":{\"b\":[1,2]}}"));
            Assert.That(WsFrame.WorldInputPayload(" {\"a\":1} \n"), Is.EqualTo(" {\"a\":1} \n"));
        }
    }
}
