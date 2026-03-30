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
        public string session_token;
        public string username;
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
        public string session_token;
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
        public string session_token;
    }

    [Serializable]
    public class RefreshResponse
    {
        public string player_id;
        public string session_token;
    }
}
