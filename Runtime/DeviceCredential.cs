using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Asobi
{
    /// <summary>
    /// A persisted guest keypair. The same pair, re-presented on every launch,
    /// resumes the same backend player.
    /// </summary>
    public struct DeviceCredentials
    {
        public string DeviceId;
        public string DeviceSecret;
    }

    /// <summary>
    /// Swappable persistence for the guest keypair. The Unity default is
    /// <see cref="PlayerPrefsDeviceStore"/>; tests inject an in-memory store.
    /// </summary>
    public interface IDeviceStore
    {
        bool TryLoad(out DeviceCredentials creds);
        void Save(DeviceCredentials creds);
        void Clear();
    }

    /// <summary>
    /// Overrides for guest device-credential generation and persistence.
    /// </summary>
    public class DeviceOptions
    {
        /// <summary>Where the keypair is persisted. Null generates in-memory without persisting.</summary>
        public IDeviceStore Store;

        /// <summary>Byte source. Must return at least the requested count. Defaults to the OS CSPRNG.</summary>
        public Func<int, byte[]> RandomBytes;

        /// <summary>Fixes the device id explicitly instead of deriving it from random bytes.</summary>
        public string DeviceId;
    }

    /// <summary>
    /// Opt-in guest device-credential helper.
    ///
    /// Guest sign-in needs a stable <c>{device_id, device_secret}</c> the game
    /// generates once, persists, and re-presents on every launch (the same pair
    /// resumes the same player). This does that dance so a game does not hand-roll
    /// base64 + persistence + the &gt;= 32-byte rule. Entirely optional: pass your
    /// own values to <c>AsobiAuth.GuestAsync(...)</c> for your own storage or key
    /// source.
    ///
    /// device_id:     any stable per-install id (here: base64 of 16 random bytes).
    /// device_secret: standard base64 of 32 random bytes. The server requires this
    ///                exact shape; anything decoding to &lt; 32 bytes is rejected as
    ///                weak_device_secret.
    /// </summary>
    public static class DeviceCredential
    {
        const int SecretByteCount = 32;
        const int IdByteCount = 16;

        /// <summary>
        /// Generate a fresh keypair. Does not persist. <paramref name="opts"/>
        /// may supply a stronger/deterministic byte source or fix the device id.
        /// </summary>
        public static DeviceCredentials Generate(DeviceOptions opts = null)
        {
            opts ??= new DeviceOptions();
            var rand = opts.RandomBytes ?? DefaultRandomBytes;

            var secretBytes = rand(SecretByteCount);
            // Fail loud here rather than as an opaque server weak_device_secret 4xx
            // if a custom source under-delivers: the secret must decode to >= 32 bytes.
            if (secretBytes == null || secretBytes.Length < SecretByteCount)
                throw new InvalidOperationException(
                    $"DeviceCredential: RandomBytes({SecretByteCount}) must return at least {SecretByteCount} bytes");

            var deviceId = opts.DeviceId ?? Convert.ToBase64String(rand(IdByteCount));
            var secret = Convert.ToBase64String(secretBytes, 0, SecretByteCount);
            return new DeviceCredentials { DeviceId = deviceId, DeviceSecret = secret };
        }

        /// <summary>
        /// Load the persisted keypair, or generate + persist one on first run.
        /// With no store, generates an in-memory pair without persisting.
        /// </summary>
        public static DeviceCredentials LoadOrCreate(DeviceOptions opts = null)
        {
            opts ??= new DeviceOptions();
            var store = opts.Store;
            if (store == null)
                return Generate(opts);

            if (store.TryLoad(out var existing) && IsValid(existing))
                return existing;

            var created = Generate(opts);
            store.Save(created);
            return created;
        }

        /// <summary>
        /// Erase the stored keypair so the next <see cref="LoadOrCreate"/> mints a
        /// brand-new guest (data.created = true). Use for "switch account" or a
        /// local "forget me". Local-only: it does not delete the server account,
        /// so pair it with logout to end the session, or upgrade the guest first.
        /// </summary>
        public static void Clear(DeviceOptions opts = null)
        {
            opts?.Store?.Clear();
        }

        /// <summary>
        /// Load (or generate + persist) the keypair and hand it to a guest
        /// sign-in delegate, returning whatever the delegate returns. This is the
        /// pure core behind <c>AsobiAuth.GuestDeviceAsync</c>.
        /// </summary>
        public static async Task<T> SignInAsync<T>(DeviceOptions opts, Func<string, string, Task<T>> guest)
        {
            if (guest == null) throw new ArgumentNullException(nameof(guest));
            var creds = LoadOrCreate(opts);
            return await guest(creds.DeviceId, creds.DeviceSecret);
        }

        static bool IsValid(DeviceCredentials creds)
        {
            if (string.IsNullOrEmpty(creds.DeviceId) || string.IsNullOrEmpty(creds.DeviceSecret))
                return false;
            // A secret decoding to < 32 bytes would be rejected server-side as
            // weak_device_secret; treat it as a miss and regenerate.
            try
            {
                return Convert.FromBase64String(creds.DeviceSecret).Length >= SecretByteCount;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static byte[] DefaultRandomBytes(int count)
        {
            var buf = new byte[count];
            RandomNumberGenerator.Fill(buf);
            return buf;
        }
    }
}
