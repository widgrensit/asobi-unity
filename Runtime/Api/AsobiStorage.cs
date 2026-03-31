using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiStorage
    {
        readonly AsobiClient _client;
        internal AsobiStorage(AsobiClient client) => _client = client;

        // --- Cloud Saves ---

        public Task<CloudSaveListResponse> ListSavesAsync()
        {
            return _client.Http.Get<CloudSaveListResponse>("/api/v1/saves");
        }

        public async Task<CloudSave> GetSaveAsync(string slot)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/saves/{slot}");
            return JsonHelper.ParseCloudSave(raw);
        }

        public async Task<CloudSave> PutSaveAsync(string slot, string dataJson, int? version = null)
        {
            string body;
            if (version.HasValue)
                body = $"{{\"data\":{dataJson},\"version\":{version.Value}}}";
            else
                body = $"{{\"data\":{dataJson}}}";
            var raw = await _client.Http.PutRaw($"/api/v1/saves/{slot}", body);
            return JsonHelper.ParseCloudSave(raw);
        }

        // --- Generic Storage ---

        public async Task<StorageListResponse> ListStorageAsync(string collection, int limit = 50)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            var raw = await _client.Http.GetRaw($"/api/v1/storage/{collection}", query);
            return JsonHelper.ParseStorageList(raw);
        }

        public async Task<StorageObject> GetStorageAsync(string collection, string key)
        {
            var raw = await _client.Http.GetRaw($"/api/v1/storage/{collection}/{key}");
            return JsonHelper.ParseStorageObject(raw);
        }

        public async Task<StorageObject> PutStorageAsync(string collection, string key, string valueJson,
            string readPerm = "owner", string writePerm = "owner")
        {
            var body = $"{{\"value\":{valueJson},\"read_perm\":\"{EscapeJson(readPerm)}\",\"write_perm\":\"{EscapeJson(writePerm)}\"}}";
            var raw = await _client.Http.PutRaw($"/api/v1/storage/{collection}/{key}", body);
            return JsonHelper.ParseStorageObject(raw);
        }

        public Task<AsobiResponse> DeleteStorageAsync(string collection, string key)
        {
            return _client.Http.Delete($"/api/v1/storage/{collection}/{key}");
        }

        static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
