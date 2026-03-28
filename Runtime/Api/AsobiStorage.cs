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

        public Task<CloudSave> GetSaveAsync(string slot)
        {
            return _client.Http.Get<CloudSave>($"/api/v1/saves/{slot}");
        }

        public Task<CloudSave> PutSaveAsync(string slot, string data, int? version = null)
        {
            var req = new CloudSavePutRequest { data = data };
            if (version.HasValue) req.version = version.Value;
            return _client.Http.Put<CloudSave>($"/api/v1/saves/{slot}", req);
        }

        // --- Generic Storage ---

        public Task<StorageListResponse> ListStorageAsync(string collection, int limit = 50)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            return _client.Http.Get<StorageListResponse>($"/api/v1/storage/{collection}", query);
        }

        public Task<StorageObject> GetStorageAsync(string collection, string key)
        {
            return _client.Http.Get<StorageObject>($"/api/v1/storage/{collection}/{key}");
        }

        public Task<StorageObject> PutStorageAsync(string collection, string key, string value,
            string readPerm = "owner", string writePerm = "owner")
        {
            var req = new StoragePutRequest
            {
                value = value,
                read_perm = readPerm,
                write_perm = writePerm
            };
            return _client.Http.Put<StorageObject>($"/api/v1/storage/{collection}/{key}", req);
        }

        public Task<AsobiResponse> DeleteStorageAsync(string collection, string key)
        {
            return _client.Http.Delete($"/api/v1/storage/{collection}/{key}");
        }
    }
}
