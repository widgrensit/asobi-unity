using System;

namespace Asobi
{
    [Serializable]
    public class CloudSave
    {
        public string player_id;
        public string slot;
        public string data;
        public int version;
        public string updated_at;
    }

    [Serializable]
    public class CloudSaveListResponse
    {
        public CloudSaveSummary[] saves;
    }

    [Serializable]
    public class CloudSaveSummary
    {
        public string slot;
        public int version;
        public string updated_at;
    }

    [Serializable]
    public class CloudSavePutRequest
    {
        public string data;
        public int version;
    }

    [Serializable]
    public class StorageObject
    {
        public string collection;
        public string key;
        public string player_id;
        public string value;
        public int version;
        public string read_perm;
        public string write_perm;
        public string updated_at;
    }

    [Serializable]
    public class StorageListResponse
    {
        public StorageObject[] objects;
    }

    [Serializable]
    public class StoragePutRequest
    {
        public string value;
        public string read_perm;
        public string write_perm;
    }
}
