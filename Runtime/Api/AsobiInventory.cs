using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiInventory
    {
        readonly AsobiClient _client;
        internal AsobiInventory(AsobiClient client) => _client = client;

        public Task<InventoryResponse> ListAsync()
        {
            return _client.Http.Get<InventoryResponse>("/api/v1/inventory");
        }

        public Task<AsobiResponse> ConsumeAsync(string itemId, int quantity = 1)
        {
            var req = new ConsumeRequest { item_id = itemId, quantity = quantity };
            return _client.Http.Post<AsobiResponse>("/api/v1/inventory/consume", req);
        }
    }
}
