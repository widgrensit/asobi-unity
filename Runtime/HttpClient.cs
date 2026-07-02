using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Asobi
{
    internal class HttpClient
    {
        readonly string _baseUrl;
        readonly object _refreshLock = new object();
        Task<bool> _refreshInFlight;
        public string AccessToken { get; set; }
        public Func<Task<bool>> RefreshHandler { get; set; }

        public HttpClient(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public Task<T> Get<T>(string path, Dictionary<string, string> query = null)
            => Send<T>(new RequestSpec("GET", path, BuildUrl(path, query)));

        public Task<T> Post<T>(string path, object body = null)
            => Send<T>(JsonSpec("POST", path, body));

        public Task<T> PostJson<T>(string path, string json)
            => Send<T>(RawSpec("POST", path, json));

        public Task<T> Put<T>(string path, object body = null)
            => Send<T>(JsonSpec("PUT", path, body));

        public Task<T> PutJson<T>(string path, string json)
            => Send<T>(RawSpec("PUT", path, json));

        public Task<string> GetRaw(string path, Dictionary<string, string> query = null)
            => SendRaw(new RequestSpec("GET", path, BuildUrl(path, query)));

        public Task<string> PutRaw(string path, string json)
            => SendRaw(RawSpec("PUT", path, json));

        public Task<AsobiResponse> Delete(string path, object body = null, Dictionary<string, string> query = null)
        {
            var spec = new RequestSpec("DELETE", path, BuildUrl(path, query));
            if (body != null)
            {
                spec.Body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
                spec.ContentType = "application/json";
            }
            return Send<AsobiResponse>(spec);
        }

        RequestSpec JsonSpec(string method, string path, object body)
        {
            var spec = new RequestSpec(method, path, BuildUrl(path));
            if (body != null)
            {
                spec.Body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
                spec.ContentType = "application/json";
            }
            return spec;
        }

        RequestSpec RawSpec(string method, string path, string json)
            => new RequestSpec(method, path, BuildUrl(path))
            {
                Body = Encoding.UTF8.GetBytes(json),
                ContentType = "application/json"
            };

        async Task<string> SendRaw(RequestSpec spec)
        {
            var (code, text, connError) = await SendOnce(spec);
            if (connError != null)
                throw new AsobiException(-1, $"Connection error: {connError}");

            if (code == 401 && ShouldAttemptRefresh(spec.Path) && RefreshHandler != null)
            {
                if (await TryRefresh())
                {
                    (code, text, connError) = await SendOnce(spec);
                    if (connError != null)
                        throw new AsobiException(-1, $"Connection error: {connError}");
                }
            }

            ThrowIfError(code, text);
            return text;
        }

        async Task<T> Send<T>(RequestSpec spec)
        {
            var responseText = await SendRaw(spec);
            if (string.IsNullOrEmpty(responseText))
                return default;
            return JsonUtility.FromJson<T>(responseText);
        }

        Task<bool> TryRefresh()
        {
            lock (_refreshLock)
                _refreshInFlight ??= RunRefresh();
            return _refreshInFlight;
        }

        async Task<bool> RunRefresh()
        {
            try { return await RefreshHandler(); }
            catch { return false; }
            finally
            {
                lock (_refreshLock)
                    _refreshInFlight = null;
            }
        }

        async Task<(long code, string text, string connError)> SendOnce(RequestSpec spec)
        {
            var req = new UnityWebRequest(spec.Url, spec.Method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (spec.Body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(spec.Body);
                req.SetRequestHeader("Content-Type", spec.ContentType ?? "application/json");
            }
            if (!string.IsNullOrEmpty(AccessToken))
                req.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.ConnectionError)
                return (0, null, req.error);

            return (req.responseCode, req.downloadHandler?.text, null);
        }

        static bool ShouldAttemptRefresh(string path)
            => !string.IsNullOrEmpty(path) && !path.Contains("/auth/");

        static void ThrowIfError(long code, string responseText)
        {
            if (code < 400) return;
            AsobiError error = null;
            try { error = JsonUtility.FromJson<AsobiError>(responseText); } catch { }
            throw new AsobiException((int)code, error?.error ?? $"HTTP {code}", error);
        }

        string BuildUrl(string path, Dictionary<string, string> query = null)
        {
            var url = $"{_baseUrl}{path}";
            if (query != null && query.Count > 0)
            {
                var sb = new StringBuilder(url);
                sb.Append('?');
                var first = true;
                foreach (var kv in query)
                {
                    if (!first) sb.Append('&');
                    sb.Append(UnityWebRequest.EscapeURL(kv.Key));
                    sb.Append('=');
                    sb.Append(UnityWebRequest.EscapeURL(kv.Value));
                    first = false;
                }
                url = sb.ToString();
            }
            return url;
        }
    }

    internal class RequestSpec
    {
        public string Method;
        public string Path;
        public string Url;
        public byte[] Body;
        public string ContentType;

        public RequestSpec(string method, string path, string url)
        {
            Method = method;
            Path = path;
            Url = url;
        }
    }

    [Serializable]
    public class AsobiError
    {
        public string error;
    }

    [Serializable]
    public class AsobiResponse
    {
        public bool success;
    }

    public class AsobiException : Exception
    {
        public int StatusCode { get; }
        public AsobiError Error { get; }

        public AsobiException(int statusCode, string message, AsobiError error = null)
            : base(message)
        {
            StatusCode = statusCode;
            Error = error;
        }
    }
}
