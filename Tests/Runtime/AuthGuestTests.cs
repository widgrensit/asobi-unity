using NUnit.Framework;
using UnityEngine;

namespace Asobi.Tests
{
    /// <summary>
    /// Wire-shape tests for guest auth request/response models.
    ///
    /// Guest sign-in (POST /api/v1/auth/guest) and upgrade
    /// (POST /api/v1/auth/guest/upgrade) are exercised end-to-end against a
    /// live backend by SmokeTest; these EditMode tests pin the JSON contract
    /// (request field names + response flag mapping) without a backend.
    /// </summary>
    public class AuthGuestTests
    {
        [Test]
        public void GuestRequestSerializesDeviceFields()
        {
            var json = JsonUtility.ToJson(new GuestRequest
            {
                device_id = "dev-1",
                device_secret = "c2VjcmV0"
            });

            Assert.That(json, Does.Contain("\"device_id\":\"dev-1\""));
            Assert.That(json, Does.Contain("\"device_secret\":\"c2VjcmV0\""));
        }

        [Test]
        public void GuestUpgradeRequestSerializesCredentials()
        {
            var json = JsonUtility.ToJson(new GuestUpgradeRequest
            {
                username = "player1",
                password = "secret123"
            });

            Assert.That(json, Does.Contain("\"username\":\"player1\""));
            Assert.That(json, Does.Contain("\"password\":\"secret123\""));
        }

        [Test]
        public void AuthResponseParsesGuestCreation()
        {
            var raw = "{\"player_id\":\"p1\",\"access_token\":\"a\",\"refresh_token\":\"r\","
                + "\"username\":\"guest_p1\",\"created\":true,\"guest\":true}";

            var resp = JsonUtility.FromJson<AuthResponse>(raw);

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

            var resp = JsonUtility.FromJson<AuthResponse>(raw);

            Assert.That(resp.guest, Is.True);
            Assert.That(resp.created, Is.False);
        }

        [Test]
        public void AuthResponseParsesUpgrade()
        {
            var raw = "{\"player_id\":\"p1\",\"access_token\":\"a3\",\"refresh_token\":\"r3\","
                + "\"username\":\"player1\",\"upgraded\":true}";

            var resp = JsonUtility.FromJson<AuthResponse>(raw);

            Assert.That(resp.upgraded, Is.True);
            Assert.That(resp.username, Is.EqualTo("player1"));
        }
    }
}
