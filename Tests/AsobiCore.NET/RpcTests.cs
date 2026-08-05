using System.IO;
using NUnit.Framework;

namespace Asobi.Tests
{
    // The RPC seam: an extension's reply, decoded off the wire.
    //
    // RpcReply.Parse is the whole of the decoding; AsobiRealtime does nothing
    // with a reply except turn one of these into a completed or faulted Task.
    // That split is what makes this testable at all - AsobiRealtime needs Unity
    // and is not linked into this project.
    public class RpcTests
    {
        static string Fixture(string name) =>
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name));

        [Test]
        public void RpcOk_YieldsTheResultNotTheEnvelope()
        {
            var reply = RpcReply.Parse("rpc.ok", Fixture("rpc.ok.json"));
            Assert.That(reply.IsError, Is.False);
            Assert.That(reply.ResultJson, Is.EqualTo("{\"reward\":100}"));
        }

        [Test]
        public void RpcError_CarriesTheCodeCallersBranchOn()
        {
            var reply = RpcReply.Parse("rpc.error", Fixture("rpc.error.json"));
            Assert.That(reply.IsError, Is.True);
            Assert.That(reply.Code, Is.EqualTo("quests.already_claimed"));
            Assert.That(reply.Message, Is.EqualTo("This quest was already claimed."));
            Assert.That(reply.DetailsJson, Is.EqualTo("{}"));
        }

        // Otherwise a server defect and a domain outcome look identical to a
        // caller branching on Code.
        [Test]
        public void AnEmptyErrorObjectStillGetsACode()
        {
            var reply = RpcReply.Parse("rpc.error", "{\"type\":\"rpc.error\",\"cid\":\"1\",\"payload\":{}}");
            Assert.That(reply.Code, Is.EqualTo("internal"));
            Assert.That(reply.Message, Is.EqualTo("internal"), "message falls back to the code, never null");
        }

        [Test]
        public void AMissingResultIsAnEmptyObjectNotNull()
        {
            var reply = RpcReply.Parse("rpc.ok", "{\"type\":\"rpc.ok\",\"cid\":\"1\",\"payload\":{}}");
            Assert.That(reply.ResultJson, Is.EqualTo("{}"));
        }

        // Details are defined by the extension, so they reach the caller as raw
        // JSON rather than being flattened into something we guessed at.
        [Test]
        public void NestedDetailsSurviveIntact()
        {
            const string raw = "{\"type\":\"rpc.error\",\"cid\":\"1\",\"payload\":{\"error\":{" +
                "\"code\":\"quests.locked\",\"message\":\"no\"," +
                "\"details\":{\"quest\":{\"key\":\"daily\",\"tier\":[1,2]}}}}}";
            var reply = RpcReply.Parse("rpc.error", raw);
            Assert.That(reply.DetailsJson, Is.EqualTo("{\"quest\":{\"key\":\"daily\",\"tier\":[1,2]}}"));
        }

        // A brace or bracket inside a message must not truncate the value being
        // sliced - the reason the reader tracks strings rather than counting
        // braces blindly.
        [Test]
        public void BracesInsideAMessageDoNotTruncate()
        {
            const string raw = "{\"type\":\"rpc.error\",\"cid\":\"1\",\"payload\":{\"error\":{" +
                "\"code\":\"bad\",\"message\":\"expected } or ] here\",\"details\":{\"n\":1}}}}";
            var reply = RpcReply.Parse("rpc.error", raw);
            Assert.That(reply.Message, Is.EqualTo("expected } or ] here"));
            Assert.That(reply.DetailsJson, Is.EqualTo("{\"n\":1}"));
        }

        [Test]
        public void EscapesInAMessageAreDecoded()
        {
            const string raw = "{\"type\":\"rpc.error\",\"cid\":\"1\",\"payload\":{\"error\":{" +
                "\"code\":\"bad\",\"message\":\"line\\none\\t\\\"quoted\\\"\"}}}";
            var reply = RpcReply.Parse("rpc.error", raw);
            Assert.That(reply.Message, Is.EqualTo("line\none\t\"quoted\""));
        }

        [Test]
        public void AResultArrayIsReturnedWhole()
        {
            const string raw = "{\"type\":\"rpc.ok\",\"cid\":\"1\",\"payload\":{\"result\":[{\"a\":1},{\"b\":2}]}}";
            var reply = RpcReply.Parse("rpc.ok", raw);
            Assert.That(reply.ResultJson, Is.EqualTo("[{\"a\":1},{\"b\":2}]"));
        }

        // A key that appears deeper in the document must not be mistaken for
        // the one being looked up at this level.
        [Test]
        public void AShadowingKeyInsideTheResultIsNotPickedUp()
        {
            const string raw = "{\"type\":\"rpc.ok\",\"cid\":\"1\",\"payload\":{\"result\":{\"result\":\"inner\"}}}";
            var reply = RpcReply.Parse("rpc.ok", raw);
            Assert.That(reply.ResultJson, Is.EqualTo("{\"result\":\"inner\"}"));
        }

        [Test]
        public void AnAbsentPathReadsAsNull()
        {
            Assert.That(JsonSlice.Read("{\"payload\":{}}", "payload", "error", "code"), Is.Null);
            Assert.That(JsonSlice.Read("{}", "payload"), Is.Null);
            Assert.That(JsonSlice.Read(null, "payload"), Is.Null);
        }

        [Test]
        public void UnquoteRejectsNonStrings()
        {
            Assert.That(JsonSlice.Unquote("{\"a\":1}"), Is.Null);
            Assert.That(JsonSlice.Unquote("42"), Is.Null);
            Assert.That(JsonSlice.Unquote(null), Is.Null);
            Assert.That(JsonSlice.Unquote("\"ok\""), Is.EqualTo("ok"));
        }

        [Test]
        public void UnicodeEscapesDecode()
        {
            Assert.That(JsonSlice.Unquote("\"\\u00e5\\u00e4\\u00f6\""), Is.EqualTo("åäö"));
        }
    }
}
