using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Asobi.Tests
{
    /// <summary>
    /// License-free tests for the opt-in guest device-credential helper. The
    /// generation + persistence logic lives in the platform-neutral
    /// <see cref="DeviceCredential"/> so it runs here without a Unity license;
    /// the Unity-only <see cref="PlayerPrefsDeviceStore"/> is exercised through
    /// the same <see cref="IDeviceStore"/> contract via an in-memory fake.
    /// </summary>
    public class DeviceCredentialTests
    {
        class MemoryStore : IDeviceStore
        {
            DeviceCredentials? _value;
            public int Saves;

            public bool TryLoad(out DeviceCredentials creds)
            {
                if (_value.HasValue) { creds = _value.Value; return true; }
                creds = default;
                return false;
            }

            public void Save(DeviceCredentials creds) { _value = creds; Saves++; }
            public void Clear() => _value = null;
        }

        static byte[] Zeros(int n) => new byte[n];

        [Test]
        public void GenerateSecretIsStandardBase64OfExactly32Bytes()
        {
            var creds = DeviceCredential.Generate();

            var decoded = Convert.FromBase64String(creds.DeviceSecret);
            Assert.That(decoded.Length, Is.EqualTo(32));
            // Standard base64 (RFC 4648, '+/' with '=' padding), never URL-safe.
            Assert.That(creds.DeviceSecret, Does.Not.Contain("-"));
            Assert.That(creds.DeviceSecret, Does.Not.Contain("_"));
        }

        [Test]
        public void GenerateSecretDecodesWithinServerBounds()
        {
            for (var i = 0; i < 50; i++)
            {
                var len = Convert.FromBase64String(DeviceCredential.Generate().DeviceSecret).Length;
                Assert.That(len, Is.InRange(32, 128));
            }
        }

        [Test]
        public void GenerateDeviceIdIsNonEmptyAndBounded()
        {
            var id = DeviceCredential.Generate().DeviceId;
            Assert.That(id, Is.Not.Empty);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(id), Is.LessThanOrEqualTo(255));
        }

        [Test]
        public void GenerateHonoursExplicitDeviceId()
        {
            var creds = DeviceCredential.Generate(new DeviceOptions { DeviceId = "fixed-id" });
            Assert.That(creds.DeviceId, Is.EqualTo("fixed-id"));
        }

        [Test]
        public void GenerateUsesInjectedByteSource()
        {
            var creds = DeviceCredential.Generate(new DeviceOptions { RandomBytes = Zeros });
            // 32 zero bytes -> 44-char base64 of all 'A' plus padding.
            Assert.That(creds.DeviceSecret, Is.EqualTo(new string('A', 43) + "="));
            Assert.That(Convert.FromBase64String(creds.DeviceSecret).Length, Is.EqualTo(32));
        }

        [Test]
        public void GenerateRejectsUndersizedByteSource()
        {
            var opts = new DeviceOptions { RandomBytes = n => new byte[Math.Max(0, n - 1)] };
            Assert.Throws<InvalidOperationException>(() => DeviceCredential.Generate(opts));
        }

        [Test]
        public void GenerateProducesDistinctPairs()
        {
            var a = DeviceCredential.Generate();
            var b = DeviceCredential.Generate();
            Assert.That(a.DeviceSecret, Is.Not.EqualTo(b.DeviceSecret));
            Assert.That(a.DeviceId, Is.Not.EqualTo(b.DeviceId));
        }

        [Test]
        public void LoadOrCreatePersistsThenResumesSamePair()
        {
            var store = new MemoryStore();
            var opts = new DeviceOptions { Store = store };

            var first = DeviceCredential.LoadOrCreate(opts);
            var second = DeviceCredential.LoadOrCreate(opts);

            Assert.That(store.Saves, Is.EqualTo(1), "second launch must not re-persist");
            Assert.That(second.DeviceId, Is.EqualTo(first.DeviceId));
            Assert.That(second.DeviceSecret, Is.EqualTo(first.DeviceSecret));
        }

        [Test]
        public void LoadOrCreateRegeneratesWhenStoredSecretDecodesUndersized()
        {
            var store = new MemoryStore();
            // A well-formed base64 string that decodes to only 16 bytes: the
            // server would reject it, so LoadOrCreate must treat it as a miss.
            store.Save(new DeviceCredentials
            {
                DeviceId = "stale-id",
                DeviceSecret = Convert.ToBase64String(new byte[16])
            });

            var refreshed = DeviceCredential.LoadOrCreate(new DeviceOptions { Store = store });

            Assert.That(Convert.FromBase64String(refreshed.DeviceSecret).Length, Is.EqualTo(32));
            Assert.That(refreshed.DeviceId, Is.Not.EqualTo("stale-id"));
        }

        [Test]
        public void LoadOrCreateRegeneratesWhenStoredSecretIsNotBase64()
        {
            var store = new MemoryStore();
            store.Save(new DeviceCredentials { DeviceId = "id", DeviceSecret = "not valid base64!" });

            var refreshed = DeviceCredential.LoadOrCreate(new DeviceOptions { Store = store });

            Assert.That(Convert.FromBase64String(refreshed.DeviceSecret).Length, Is.EqualTo(32));
        }

        [Test]
        public void SignInDoesNotMutateCallerOptions()
        {
            var opts = new DeviceOptions { Store = new MemoryStore() };
            var originalStore = opts.Store;

            _ = DeviceCredential.SignInAsync(opts, (_, _) => Task.FromResult(0)).Result;

            Assert.That(opts.Store, Is.SameAs(originalStore));
            Assert.That(opts.RandomBytes, Is.Null);
            Assert.That(opts.DeviceId, Is.Null);
        }

        [Test]
        public void LoadOrCreateWithoutStoreDoesNotPersist()
        {
            var a = DeviceCredential.LoadOrCreate(new DeviceOptions());
            var b = DeviceCredential.LoadOrCreate(new DeviceOptions());
            Assert.That(a.DeviceSecret, Is.Not.EqualTo(b.DeviceSecret));
        }

        [Test]
        public void ClearErasesSoNextLoadMintsNewGuest()
        {
            var store = new MemoryStore();
            var opts = new DeviceOptions { Store = store };

            var before = DeviceCredential.LoadOrCreate(opts);
            DeviceCredential.Clear(opts);
            var after = DeviceCredential.LoadOrCreate(opts);

            Assert.That(after.DeviceId, Is.Not.EqualTo(before.DeviceId));
            Assert.That(after.DeviceSecret, Is.Not.EqualTo(before.DeviceSecret));
        }

        [Test]
        public async Task SignInForwardsLoadedCredsAndPassesResultThrough()
        {
            var store = new MemoryStore();
            var opts = new DeviceOptions { Store = store };
            var expected = new AuthResponse { player_id = "p1", created = true, guest = true };

            string sentId = null, sentSecret = null;
            var resp = await DeviceCredential.SignInAsync(opts, (id, secret) =>
            {
                sentId = id;
                sentSecret = secret;
                return Task.FromResult(expected);
            });

            var persisted = DeviceCredential.LoadOrCreate(opts);
            Assert.That(sentId, Is.EqualTo(persisted.DeviceId));
            Assert.That(sentSecret, Is.EqualTo(persisted.DeviceSecret));
            Assert.That(resp, Is.SameAs(expected));
        }

        [Test]
        public async Task SignInReusesTheSameGuestAcrossCalls()
        {
            var store = new MemoryStore();
            var opts = new DeviceOptions { Store = store };

            string firstSecret = null, secondSecret = null;
            await DeviceCredential.SignInAsync(opts, (_, s) => { firstSecret = s; return Task.FromResult(0); });
            await DeviceCredential.SignInAsync(opts, (_, s) => { secondSecret = s; return Task.FromResult(0); });

            Assert.That(secondSecret, Is.EqualTo(firstSecret));
            Assert.That(store.Saves, Is.EqualTo(1));
        }
    }
}
