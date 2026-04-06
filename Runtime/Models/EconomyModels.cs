using System;

namespace Asobi
{
    [Serializable]
    public class Wallet
    {
        public string currency;
        public long balance;
    }

    [Serializable]
    public class WalletsResponse
    {
        public Wallet[] wallets;
    }

    [Serializable]
    public class Transaction
    {
        public string id;
        public string wallet_id;
        public long amount;
        public long balance_after;
        public string reason;
        public string reference_type;
        public string reference_id;
        public string inserted_at;
    }

    [Serializable]
    public class TransactionsResponse
    {
        public Transaction[] transactions;
    }

    [Serializable]
    public class StoreListing
    {
        public string id;
        public string item_def_id;
        public string currency;
        public long price;
        public bool active;
        public string valid_from;
        public string valid_until;
    }

    [Serializable]
    public class StoreResponse
    {
        public StoreListing[] listings;
    }

    [Serializable]
    public class PurchaseRequest
    {
        public string listing_id;
    }

    [Serializable]
    public class PurchaseResponse
    {
        public bool success;
        public PlayerItem item;
    }

    [Serializable]
    public class PlayerItem
    {
        public string id;
        public string item_def_id;
        public string player_id;
        public int quantity;
        public string metadata;
        public string acquired_at;
        public string updated_at;
    }

    [Serializable]
    public class InventoryResponse
    {
        public PlayerItem[] items;
    }

    [Serializable]
    public class ConsumeRequest
    {
        public string item_id;
        public int quantity;
    }

    [Serializable]
    public class ConsumeResponse
    {
        public bool success;
        public int remaining_quantity;
    }
}
