using Asobi;
using NUnit.Framework;

namespace Asobi.Tests
{
    /// <summary>
    /// asobi answers every failure with {"error": {code, message, details}}.
    /// The client used to read that into a model with a single `string error`
    /// field, and JsonUtility drops object-typed members silently - so no code
    /// and no message survived and every failure collapsed to "HTTP 4xx".
    /// </summary>
    public class ErrorParserTests
    {
        [Test]
        public void ReadsCodeAndMessageFromTheSharedErrorObject()
        {
            var err = AsobiErrorParser.Parse(
                "{\"error\":{\"code\":\"player.confirmation_failed\",\"message\":\"Wrong.\",\"details\":{}}}");

            Assert.That(err, Is.Not.Null);
            Assert.That(err.code, Is.EqualTo("player.confirmation_failed"));
            Assert.That(err.error, Is.EqualTo("Wrong."));
        }

        [Test]
        public void NestedDetailsDoNotSwallowTheFieldsAfterThem()
        {
            var err = AsobiErrorParser.Parse(
                "{\"error\":{\"details\":{\"retry_after\":5},\"code\":\"guest.rate_limited\",\"message\":\"Slow down.\"}}");

            Assert.That(err.code, Is.EqualTo("guest.rate_limited"));
            Assert.That(err.error, Is.EqualTo("Slow down."));
        }

        [Test]
        public void StillAcceptsTheFlatLegacyBody()
        {
            var err = AsobiErrorParser.Parse("{\"error\":\"guest_auth_disabled\"}");

            Assert.That(err.error, Is.EqualTo("guest_auth_disabled"));
            Assert.That(err.code, Is.EqualTo(""));
        }

        [Test]
        public void ReturnsNullWhenThereIsNoErrorField()
        {
            Assert.That(AsobiErrorParser.Parse("{\"deleted\":true}"), Is.Null);
            Assert.That(AsobiErrorParser.Parse(""), Is.Null);
            Assert.That(AsobiErrorParser.Parse(null), Is.Null);
        }

        [Test]
        public void UnescapesAQuotedMessage()
        {
            var err = AsobiErrorParser.Parse(
                "{\"error\":{\"code\":\"c\",\"message\":\"He said \\\"no\\\".\"}}");

            Assert.That(err.error, Is.EqualTo("He said \"no\"."));
        }

        // A WebSocket refusal faults the task with AsobiException carrying the
        // raw envelope, so the reason is only readable off payload.reason. The
        // nested error object maps most refusals onto one code, which is why
        // MatchFindOrCreateAsync's doc points callers at the reason instead.
        [Test]
        public void AWsRefusalReasonIsReadFromThePayloadNotFromTheErrorCode()
        {
            const string raw =
                "{\"type\":\"error\",\"payload\":{\"reason\":\"quick_play_disabled\"," +
                "\"error\":{\"code\":\"ws.request_failed\"," +
                "\"message\":\"The request failed. See `details.reason`.\"," +
                "\"details\":{\"reason\":\"quick_play_disabled\"}}}}";

            var payload = JsonScan.ExtractField(raw, "payload");
            var reason = JsonScan.Unquote(JsonScan.ExtractField(payload, "reason"));

            Assert.That(reason, Is.EqualTo("quick_play_disabled"));
            Assert.That(AsobiErrorParser.Parse(raw).code, Is.EqualTo("ws.request_failed"));
        }
    }
}
