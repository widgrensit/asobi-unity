using System;

namespace Asobi
{
    [Serializable]
    public class AppleIAPRequest
    {
        public string signed_transaction;
    }

    [Serializable]
    public class GoogleIAPRequest
    {
        public string product_id;
        public string purchase_token;
    }

    [Serializable]
    public class IAPResult
    {
        public string product_id;
        public string transaction_id;
        public string order_id;
        public bool valid;
    }
}
