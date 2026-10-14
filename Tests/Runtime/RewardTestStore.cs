using System;
using System.Collections.Generic;
using System.IO;
using Dreamy.Datasave;
using Newtonsoft.Json;

namespace Dreamy.Economy.Tests
{
    // Durable JSON is separate from every service's loaded-object cache. Recreate a
    // service with the same disk dictionary, never with the previous live owners.
    internal sealed class RewardTestStore : IDatasaveService
    {
        public readonly Dictionary<string, string> Disk;
        public readonly Dictionary<string, SaveData> Cache = new();
        public Action<string> BeforeSave;
        public Action<string> AfterSave;

        public RewardTestStore(Dictionary<string, string> disk = null) => Disk = disk ?? new Dictionary<string, string>();

        public T Load<T>(string key = null) where T : SaveData, new()
        {
            T value = Disk.TryGetValue(key, out string json) ? JsonConvert.DeserializeObject<T>(json) : new T();
            value.OnAfterLoad();
            Cache[key] = value;
            return value;
        }

        public void Save<T>(T value, string key = null) where T : SaveData
        {
            BeforeSave?.Invoke(key);
            string json = JsonConvert.SerializeObject(value);
            Disk[key] = json;
            Cache[key] = value;
            AfterSave?.Invoke(key);
        }

        public void SaveAll()
        {
            foreach (KeyValuePair<string, SaveData> pair in new Dictionary<string, SaveData>(Cache))
                Save(pair.Value, pair.Key);
        }

        public bool Exists(string key) => Disk.ContainsKey(key);
        public void Delete(string key) { Disk.Remove(key); Cache.Remove(key); }
        public void DeleteAll() { Disk.Clear(); Cache.Clear(); }
        public static void Fail(string key) => throw new IOException("Injected write failure: " + key);
    }
}
