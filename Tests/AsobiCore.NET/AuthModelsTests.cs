using System.Text.Json;
using NUnit.Framework;

namespace Asobi.Tests
{
    /// <summary>
    /// License-free wire-contract tests for the guest auth models. Runs in
    /// the .NET test project (no Unity license) so the field names the
    /// backend sees are pinned in CI even when the Unity EditMode/PlayMode
    /// jobs are skipped.
    ///
    /// At runtime the SDK serializes with UnityEngine.JsonUtility, which maps
    /// public fields to their literal names. System.Text.Json with
    /// IncludeFields reproduces that name mapping for these flat POCOs, so a
    /// field rename here fails the build the same way it would break the wire.
    /// </summary>
    public class AuthModelsTests
    {
        static readonly JsonSerializerOptions Opts = new() { IncludeFields = true };

        [Test]
        public void GuestRequestSerializesDeviceFields()
        {
            var json = JsonSerializer.Serialize(
                new GuestRequest { device_id = "dev-1", device_secret = "c2VjcmV0" }, Opts);

            Assert.That(json, Does.Contain("\"device_id\":\"dev-1\""));
            Assert.That(json, Does.Contain("\"device_secret\":\"c2VjcmV0\""));
        }

        [Test]
        public void GuestUpgradeRequestSerializesCredentials()
        {
            var json = JsonSerializer.Serialize(
                new GuestUpgradeRequest { username = "player1", password = "secret123" }, Opts);

            Assert.That(json, Does.Contain("\"username\":\"player1\""));
            Assert.That(json, Does.Contain("\"password\":\"secret123\""));
        }

        [Test]
        public void AuthResponseParsesGuestCreation()
        {
            var raw = "{\"player_id\":\"p1\",\"access_token\":\"a\",\"refresh_token\":\"r\","
                + "\"username\":\"guest_p1\",\"created\":true,\"guest\":true}";

            var resp = JsonSerializer.Deserialize<AuthResponse>(raw, Opts);

            Assert.That(resp.player_id, Is.EqualTo("p1"));
            Assert.That(resp.access_token, Is.EqualTo("a"));
            Assert.That(resp.refresh_token, Is.EqualTo("r"));
            Assert.That(resp.created, Is.True);
            Assert.That(resp.guest, Is.True);
            Assert.That(resp.upgraded, Is.False);
        }

        [Test]
        public void AuthResponseParsesGuestResume()
        {
            var raw = "{\"player_id\":\"p1\",\"access_token\":\"a2\",\"refresh_token\":\"r2\","
                + "\"username\":\"guest_p1\",\"guest\":true}";

            var resp = JsonSerializer.Deserialize<AuthResponse>(raw, Opts);

            Assert.That(resp.guest, Is.True);
            Assert.That(resp.created, Is.False);
        }

        [Test]
        public void AuthResponseParsesUpgrade()
        {
            var raw = "{\"player_id\":\"p1\",\"access_token\":\"a3\",\"refresh_token\":\"r3\","
                + "\"username\":\"player1\",\"upgraded\":true}";

            var resp = JsonSerializer.Deserialize<AuthResponse>(raw, Opts);

            Assert.That(resp.upgraded, Is.True);
            Assert.That(resp.username, Is.EqualTo("player1"));
        }
    }
}
