using System;
using NUnit.Framework;
using UnityEngine;

namespace Asobi.Tests
{
    /// <summary>
    /// EditMode coverage for the guest device-credential helper plus the
    /// Unity-only <see cref="PlayerPrefsDeviceStore"/>. The generation contract
    /// is pinned off-engine in the .NET test project (DeviceCredentialTests);
    /// these tests confirm the PlayerPrefs-backed store persists, resumes, and
    /// clears through the same <see cref="IDeviceStore"/> contract.
    /// </summary>
    public class DeviceCredentialTests
    {
        const string Key = "asobi_guest_device_test";

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(Key);

        [Test]
        public void GenerateSecretIsStandardBase64OfExactly32Bytes()
        {
            var creds = DeviceCredential.Generate();

            Assert.That(Convert.FromBase64String(creds.DeviceSecret).Length, Is.EqualTo(32));
            Assert.That(creds.DeviceSecret, Does.Not.Contain("-"));
            Assert.That(creds.DeviceSecret, Does.Not.Contain("_"));
        }

        [Test]
        public void PlayerPrefsStorePersistsThenResumesSamePair()
        {
            var opts = new DeviceOptions { Store = new PlayerPrefsDeviceStore(Key) };

            var first = DeviceCredential.LoadOrCreate(opts);
            var second = DeviceCredential.LoadOrCreate(opts);

            Assert.That(second.DeviceId, Is.EqualTo(first.DeviceId));
            Assert.That(second.DeviceSecret, Is.EqualTo(first.DeviceSecret));
        }

        [Test]
        public void ClearErasesSoNextLoadMintsNewGuest()
        {
            var opts = new DeviceOptions { Store = new PlayerPrefsDeviceStore(Key) };

            var before = DeviceCredential.LoadOrCreate(opts);
            DeviceCredential.Clear(opts);
            var after = DeviceCredential.LoadOrCreate(opts);

            Assert.That(after.DeviceSecret, Is.Not.EqualTo(before.DeviceSecret));
        }

        [Test]
        public void DefaultStoreClearErasesTheKeyGuestDeviceAsyncUses()
        {
            var store = new PlayerPrefsDeviceStore();

            DeviceCredential.LoadOrCreate(new DeviceOptions { Store = store });
            store.Clear();

            Assert.That(store.TryLoad(out _), Is.False);
        }
    }
}
