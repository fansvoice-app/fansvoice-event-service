using System;
using System.Threading.Tasks;

namespace FansVoice.EventService.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task<bool> RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        Task<bool> SetHashAsync<T>(string key, string field, T value) where T : class;
        Task<T?> GetHashAsync<T>(string key, string field) where T : class;
        Task<bool> RemoveHashAsync(string key, string field);
        Task<bool> HashExistsAsync(string key, string field);
        Task<long> IncrementAsync(string key);
        Task<long> DecrementAsync(string key);
        Task<bool> LockAsync(string key, TimeSpan expiryTime);
        Task<bool> UnlockAsync(string key);
        Task<bool> RefreshExpirationAsync(string key, TimeSpan? expiration = null);
    }
}