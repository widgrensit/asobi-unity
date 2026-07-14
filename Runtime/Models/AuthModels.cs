using System;

namespace Asobi
{
    [Serializable]
    public class AuthRequest
    {
        public string username;
        public string password;
        public string display_name;
    }

    [Serializable]
    public class AuthResponse
    {
        public string player_id;
        public string access_token;
        public string refresh_token;
        public string username;
        public bool created;
        public bool guest;
        public bool upgraded;
    }

    [Serializable]
    public class GuestRequest
    {
        public string device_id;
        public string device_secret;
    }

    [Serializable]
    public class GuestUpgradeRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class OAuthRequest
    {
        public string provider;
        public string token;
    }

    [Serializable]
    public class OAuthResponse
    {
        public string player_id;
        public string access_token;
        public string refresh_token;
        public string username;
        public bool created;
    }

    [Serializable]
    public class LinkResponse
    {
        public string provider;
        public string provider_uid;
        public bool linked;
    }

    [Serializable]
    public class UnlinkRequest
    {
        public string provider;
    }

    [Serializable]
    public class RefreshRequest
    {
        public string refresh_token;
    }

    [Serializable]
    public class RefreshResponse
    {
        public string access_token;
        public string refresh_token;
    }
}
