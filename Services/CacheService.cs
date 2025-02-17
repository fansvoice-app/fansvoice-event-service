using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using FansVoice.EventService.Interfaces;

namespace FansVoice.EventService.Services
{
    public class CacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CacheService> _logger;
        private readonly IDatabase _db;

        public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
        {
            _redis = redis;
            _logger = logger;
            _db = redis.GetDatabase();
        }

        public async Task<T> GetAsync<T>(string key)
        {
            try
            {
                var value = await _db.StringGetAsync(key);
                if (!value.HasValue)
                    return default;

                return JsonSerializer.Deserialize<T>(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                return await _db.StringSetAsync(key, serializedValue, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                return await _db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _db.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence for key: {Key}", key);
                throw;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            try
            {
                var value = await GetAsync<T>(key);
                if (value != null)
                    return value;

                value = await factory();
                if (value != null)
                    await SetAsync(key, value, expiry);

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSet for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> SetHashAsync<T>(string key, string hashField, T value)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value);
                return await _db.HashSetAsync(key, hashField, serializedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting hash value for key: {Key}, field: {Field}", key, hashField);
                throw;
            }
        }

        public async Task<T> GetHashAsync<T>(string key, string hashField)
        {
            try
            {
                var value = await _db.HashGetAsync(key, hashField);
                if (!value.HasValue)
                    return default;

                return JsonSerializer.Deserialize<T>(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hash value for key: {Key}, field: {Field}", key, hashField);
                throw;
            }
        }

        public async Task<bool> RemoveHashAsync(string key, string hashField)
        {
            try
            {
                return await _db.HashDeleteAsync(key, hashField);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing hash field for key: {Key}, field: {Field}", key, hashField);
                throw;
            }
        }

        public async Task<bool> HashExistsAsync(string key, string hashField)
        {
            try
            {
                return await _db.HashExistsAsync(key, hashField);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking hash existence for key: {Key}, field: {Field}", key, hashField);
                throw;
            }
        }

        public async Task<long> IncrementAsync(string key, long value = 1)
        {
            try
            {
                return await _db.StringIncrementAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing value for key: {Key}", key);
                throw;
            }
        }

        public async Task<long> DecrementAsync(string key, long value = 1)
        {
            try
            {
                return await _db.StringDecrementAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing value for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> LockAsync(string key, string token, TimeSpan expiry)
        {
            try
            {
                return await _db.LockTakeAsync(key, token, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring lock for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> UnlockAsync(string key, string token)
        {
            try
            {
                return await _db.LockReleaseAsync(key, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lock for key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> RefreshExpirationAsync(string key, TimeSpan expiry)
        {
            try
            {
                return await _db.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing expiration for key: {Key}", key);
                throw;
            }
        }
    }
}