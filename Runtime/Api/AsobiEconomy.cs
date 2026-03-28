using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiEconomy
    {
        readonly AsobiClient _client;
        internal AsobiEconomy(AsobiClient client) => _client = client;

        public Task<WalletsResponse> GetWalletsAsync()
        {
            return _client.Http.Get<WalletsResponse>("/api/v1/wallets");
        }

        public Task<TransactionsResponse> GetHistoryAsync(string currency, int limit = 50)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            return _client.Http.Get<TransactionsResponse>($"/api/v1/wallets/{currency}/history", query);
        }

        public Task<StoreResponse> GetStoreAsync(string currency = null)
        {
            Dictionary<string, string> query = null;
            if (currency != null)
                query = new Dictionary<string, string> { { "currency", currency } };
            return _client.Http.Get<StoreResponse>("/api/v1/store", query);
        }

        public Task<PurchaseResponse> PurchaseAsync(string listingId)
        {
            var req = new PurchaseRequest { listing_id = listingId };
            return _client.Http.Post<PurchaseResponse>("/api/v1/store/purchase", req);
        }
    }
}
