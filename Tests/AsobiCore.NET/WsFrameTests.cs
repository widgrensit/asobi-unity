using NUnit.Framework;

namespace Asobi.Tests
{
    // The outbound half of client-side prediction. AsobiRealtime needs Unity
    // and is not linked here, so the envelope text is built by WsFrame and
    // asserted on its own.
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
    }
}
