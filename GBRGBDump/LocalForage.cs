using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public interface ILocalForage<T>
    {
        Task InitializeAsync();
        Task<List<string>> KeysAsync();
        Task<string> DriverAsync();
        Task<T> SetItemAsync(string key, T value);
        Task<T> GetItemAsync(string key);
        Task RemoveItemAsync(string key);
    }

    public class LocalForage<T> : ILocalForage<T>
    {
        private readonly Dictionary<string, T> _storage = new Dictionary<string, T>();

        public Task InitializeAsync()
        {
            // Simulate the ready and dummy item creation/removal
            return Task.CompletedTask;
        }

        public Task<List<string>> KeysAsync()
        {
            return Task.FromResult(new List<string>(_storage.Keys));
        }

        public Task<string> DriverAsync()
        {
            // Simulate returning the storage type
            return Task.FromResult("memory");
        }

        public Task<T> SetItemAsync(string key, T value)
        {
            _storage[key] = value;
            return Task.FromResult(value);
        }

        public Task<T> GetItemAsync(string key)
        {
            _storage.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task RemoveItemAsync(string key)
        {
            _storage.Remove(key);
            return Task.CompletedTask;
        }
    }

    public static class LocalForageFactory
    {
        public static ILocalForage<T> CreateInstance<T>(string name, string storeName)
        {
            // Here you could customize the instance creation based on parameters if needed
            return new LocalForage<T>();
        }
    }


}
