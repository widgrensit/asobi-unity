using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiInventory
    {
        readonly AsobiClient _client;
        internal AsobiInventory(AsobiClient client) => _client = client;

        public Task<InventoryResponse> ListAsync(int? limit = null)
        {
            Dictionary<string, string> query = null;
            if (limit.HasValue)
                query = new Dictionary<string, string> { { "limit", limit.Value.ToString() } };
            return _client.Http.Get<InventoryResponse>("/api/v1/inventory", query);
        }

        public Task<ConsumeResponse> ConsumeAsync(string itemId, int quantity = 1)
        {
            var req = new ConsumeRequest { item_id = itemId, quantity = quantity };
            return _client.Http.Post<ConsumeResponse>("/api/v1/inventory/consume", req);
        }
    }
}
