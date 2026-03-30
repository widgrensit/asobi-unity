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
        public string SessionToken { get; set; }

        public HttpClient(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public Task<T> Get<T>(string path, Dictionary<string, string> query = null)
        {
            var url = BuildUrl(path, query);
            return Send<T>(UnityWebRequest.Get(url));
        }

        public Task<T> Post<T>(string path, object body = null)
        {
            var url = BuildUrl(path);
            var req = new UnityWebRequest(url, "POST");
            if (body != null)
            {
                var json = JsonUtility.ToJson(body);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            return Send<T>(req);
        }

        public Task<T> PostJson<T>(string path, string json)
        {
            var url = BuildUrl(path);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return Send<T>(req);
        }

        public Task<T> Put<T>(string path, object body = null)
        {
            var url = BuildUrl(path);
            var req = new UnityWebRequest(url, "PUT");
            if (body != null)
            {
                var json = JsonUtility.ToJson(body);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            return Send<T>(req);
        }

        public Task<T> PutJson<T>(string path, string json)
        {
            var url = BuildUrl(path);
            var req = new UnityWebRequest(url, "PUT");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return Send<T>(req);
        }

        public Task<AsobiResponse> Delete(string path, object body = null)
        {
            var url = BuildUrl(path);
            var req = new UnityWebRequest(url, "DELETE");
            if (body != null)
            {
                var json = JsonUtility.ToJson(body);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            return Send<AsobiResponse>(req);
        }

        async Task<T> Send<T>(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(SessionToken))
                req.SetRequestHeader("Authorization", $"Bearer {SessionToken}");

            var op = req.SendWebRequest();

            while (!op.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.ConnectionError)
                throw new AsobiException(-1, $"Connection error: {req.error}");

            var responseText = req.downloadHandler?.text;

            if (req.responseCode >= 400)
            {
                AsobiError error = null;
                try { error = JsonUtility.FromJson<AsobiError>(responseText); } catch { }
                throw new AsobiException(
                    (int)req.responseCode,
                    error?.error ?? $"HTTP {req.responseCode}",
                    error
                );
            }

            if (string.IsNullOrEmpty(responseText))
                return default;

            return JsonUtility.FromJson<T>(responseText);
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
