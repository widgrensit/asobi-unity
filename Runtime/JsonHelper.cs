using System;
using UnityEngine;

namespace Asobi
{
    internal static class JsonHelper
    {
        internal static CloudSave ParseCloudSave(string json)
        {
            var save = JsonUtility.FromJson<CloudSave>(json);
            save.data = ExtractJsonField(json, "data");
            return save;
        }

        internal static StorageObject ParseStorageObject(string json)
        {
            var obj = JsonUtility.FromJson<StorageObject>(json);
            obj.value = ExtractJsonField(json, "value");
            return obj;
        }

        internal static StorageListResponse ParseStorageList(string json)
        {
            var objectsJson = ExtractJsonField(json, "objects");
            if (string.IsNullOrEmpty(objectsJson))
                return new StorageListResponse { objects = Array.Empty<StorageObject>() };

            var items = SplitJsonArray(objectsJson);
            var objects = new StorageObject[items.Length];
            for (int i = 0; i < items.Length; i++)
                objects[i] = ParseStorageObject(items[i]);
            return new StorageListResponse { objects = objects };
        }

        internal static MatchRecord ParseMatchRecord(string json)
        {
            var record = JsonUtility.FromJson<MatchRecord>(json);
            record.players = ExtractJsonField(json, "players");
            record.result = ExtractJsonField(json, "result");
            record.metadata = ExtractJsonField(json, "metadata");
            return record;
        }

        internal static MatchListResponse ParseMatchList(string json)
        {
            var matchesJson = ExtractJsonField(json, "matches");
            if (string.IsNullOrEmpty(matchesJson))
                return new MatchListResponse { matches = Array.Empty<MatchRecord>() };

            var items = SplitJsonArray(matchesJson);
            var matches = new MatchRecord[items.Length];
            for (int i = 0; i < items.Length; i++)
                matches[i] = ParseMatchRecord(items[i]);
            return new MatchListResponse { matches = matches };
        }

        internal static Notification ParseNotification(string json)
        {
            var notif = JsonUtility.FromJson<Notification>(json);
            notif.content = ExtractJsonField(json, "content");
            return notif;
        }

        internal static NotificationListResponse ParseNotificationList(string json)
        {
            var notificationsJson = ExtractJsonField(json, "notifications");
            if (string.IsNullOrEmpty(notificationsJson))
                return new NotificationListResponse { notifications = Array.Empty<Notification>() };

            var items = SplitJsonArray(notificationsJson);
            var notifications = new Notification[items.Length];
            for (int i = 0; i < items.Length; i++)
                notifications[i] = ParseNotification(items[i]);
            return new NotificationListResponse { notifications = notifications };
        }

        internal static string ExtractJsonField(string json, string fieldName)
        {
            var searchKey = $"\"{fieldName}\":";
            int keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIdx < 0)
                return null;

            int valueStart = keyIdx + searchKey.Length;
            while (valueStart < json.Length && json[valueStart] == ' ')
                valueStart++;

            if (valueStart >= json.Length)
                return null;

            char startChar = json[valueStart];

            if (startChar == '{' || startChar == '[')
                return ExtractBracketedValue(json, valueStart, startChar);

            if (startChar == '"')
                return ExtractStringValue(json, valueStart);

            int endIdx = valueStart;
            while (endIdx < json.Length && json[endIdx] != ',' && json[endIdx] != '}' && json[endIdx] != ']')
                endIdx++;
            return json.Substring(valueStart, endIdx - valueStart).Trim();
        }

        static string ExtractBracketedValue(string json, int start, char openChar)
        {
            char closeChar = openChar == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;

                if (c == openChar) depth++;
                else if (c == closeChar)
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        static string ExtractStringValue(string json, int start)
        {
            bool escaped = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                    return json.Substring(start, i - start + 1);
            }
            return null;
        }

        internal static string[] SplitJsonArray(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson) || arrayJson.Length < 2)
                return Array.Empty<string>();

            var inner = arrayJson.Substring(1, arrayJson.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner))
                return Array.Empty<string>();

            var results = new System.Collections.Generic.List<string>();
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int itemStart = 0;

            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\' && inString) { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    results.Add(inner.Substring(itemStart, i - itemStart).Trim());
                    itemStart = i + 1;
                }
            }
            var last = inner.Substring(itemStart).Trim();
            if (!string.IsNullOrEmpty(last))
                results.Add(last);

            return results.ToArray();
        }
    }
}
