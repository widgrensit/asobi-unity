using System;
using UnityEngine;

namespace Asobi
{
    /// <summary>
    /// Default <see cref="IDeviceStore"/>: persists the guest keypair in
    /// <see cref="PlayerPrefs"/>, alongside where the SDK already stores the
    /// refresh token. Thin by design - the generation logic lives in the
    /// platform-neutral <see cref="DeviceCredential"/> so it stays unit-testable
    /// off-engine.
    /// </summary>
    public class PlayerPrefsDeviceStore : IDeviceStore
    {
        const string DefaultKey = "asobi_guest_device";

        readonly string _key;

        public PlayerPrefsDeviceStore(string key = DefaultKey) => _key = key;

        public bool TryLoad(out DeviceCredentials creds)
        {
            creds = default;
            var raw = PlayerPrefs.GetString(_key, "");
            if (string.IsNullOrEmpty(raw)) return false;

            var stored = JsonUtility.FromJson<StoredDevice>(raw);
            if (stored == null
                || string.IsNullOrEmpty(stored.device_id)
                || string.IsNullOrEmpty(stored.device_secret))
                return false;

            creds = new DeviceCredentials
            {
                DeviceId = stored.device_id,
                DeviceSecret = stored.device_secret
            };
            return true;
        }

        public void Save(DeviceCredentials creds)
        {
            var json = JsonUtility.ToJson(new StoredDevice
            {
                device_id = creds.DeviceId,
                device_secret = creds.DeviceSecret
            });
            PlayerPrefs.SetString(_key, json);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }

        [Serializable]
        class StoredDevice
        {
            public string device_id;
            public string device_secret;
        }
    }
}
