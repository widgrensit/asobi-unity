using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiIAP
    {
        readonly AsobiClient _client;
        internal AsobiIAP(AsobiClient client) => _client = client;

        public Task<IAPResult> VerifyAppleAsync(string signedTransaction)
        {
            var req = new AppleIAPRequest { signed_transaction = signedTransaction };
            return _client.Http.Post<IAPResult>("/api/v1/iap/apple", req);
        }

        public Task<IAPResult> VerifyGoogleAsync(string productId, string purchaseToken)
        {
            var req = new GoogleIAPRequest { product_id = productId, purchase_token = purchaseToken };
            return _client.Http.Post<IAPResult>("/api/v1/iap/google", req);
        }
    }
}
