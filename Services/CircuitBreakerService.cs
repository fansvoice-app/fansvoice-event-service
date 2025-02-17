using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FansVoice.EventService.Interfaces;
using FansVoice.EventService.DTOs;

namespace FansVoice.EventService.Services
{
    public class CircuitBreakerService : ICircuitBreakerService
    {
        private readonly ILogger<CircuitBreakerService> _logger;
        private readonly ICacheService _cache;
        private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;

        private class CircuitBreakerState
        {
            public string Status { get; set; } = "Closed";
            public int FailureCount { get; set; }
            public DateTime? LastFailureTime { get; set; }
            public DateTime? LastSuccessTime { get; set; }
            public TimeSpan DurationOfBreak { get; set; }
            public int ExceptionsAllowedBeforeBreaking { get; set; }
        }

        public CircuitBreakerService(ILogger<CircuitBreakerService> logger, ICacheService cache)
        {
            _logger = logger;
            _cache = cache;
            _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
        }

        public async Task<T> ExecuteAsync<T>(string operationKey, Func<Task<T>> operation)
        {
            var circuitBreaker = _circuitBreakers.GetOrAdd(operationKey, key => new CircuitBreakerState
            {
                Status = "Closed",
                FailureCount = 0,
                DurationOfBreak = TimeSpan.FromSeconds(30),
                ExceptionsAllowedBeforeBreaking = 3
            });

            if (circuitBreaker.Status == "Open")
            {
                if (circuitBreaker.LastFailureTime.HasValue &&
                    DateTime.UtcNow - circuitBreaker.LastFailureTime.Value > circuitBreaker.DurationOfBreak)
                {
                    // Half-open state
                    circuitBreaker.Status = "HalfOpen";
                    _logger.LogInformation("Circuit breaker for {OperationKey} entering half-open state", operationKey);
                }
                else
                {
                    throw new InvalidOperationException($"Circuit breaker is open for operation: {operationKey}");
                }
            }

            try
            {
                var result = await operation();

                // Success - reset the circuit breaker
                if (circuitBreaker.Status == "HalfOpen")
                {
                    _logger.LogInformation("Circuit breaker for {OperationKey} closing after successful half-open operation", operationKey);
                }

                circuitBreaker.Status = "Closed";
                circuitBreaker.FailureCount = 0;
                circuitBreaker.LastSuccessTime = DateTime.UtcNow;

                return result;
            }
            catch (Exception ex)
            {
                await HandleFailureAsync(operationKey, circuitBreaker, ex);
                throw;
            }
        }

        private async Task HandleFailureAsync(string operationKey, CircuitBreakerState circuitBreaker, Exception ex)
        {
            circuitBreaker.FailureCount++;
            circuitBreaker.LastFailureTime = DateTime.UtcNow;

            if (circuitBreaker.Status == "HalfOpen" ||
                circuitBreaker.FailureCount >= circuitBreaker.ExceptionsAllowedBeforeBreaking)
            {
                circuitBreaker.Status = "Open";
                _logger.LogWarning(ex, "Circuit breaker for {OperationKey} opened after {FailureCount} failures",
                    operationKey, circuitBreaker.FailureCount);

                // Notify monitoring system
                await _cache.SetHashAsync("circuit_breaker:failures", operationKey, new
                {
                    LastFailure = DateTime.UtcNow,
                    Exception = ex.Message,
                    FailureCount = circuitBreaker.FailureCount
                });
            }
            else
            {
                _logger.LogWarning(ex, "Operation {OperationKey} failed {FailureCount} time(s)",
                    operationKey, circuitBreaker.FailureCount);
            }
        }

        public async Task<bool> IsCircuitBreakerOpenAsync(string operationKey)
        {
            if (_circuitBreakers.TryGetValue(operationKey, out var circuitBreaker))
            {
                if (circuitBreaker.Status == "Open")
                {
                    if (circuitBreaker.LastFailureTime.HasValue &&
                        DateTime.UtcNow - circuitBreaker.LastFailureTime.Value > circuitBreaker.DurationOfBreak)
                    {
                        return false; // Circuit breaker will enter half-open state on next execution
                    }
                    return true;
                }
            }
            return false;
        }

        public async Task<CircuitBreakerMetricsDto> GetMetricsAsync(string operationKey)
        {
            if (_circuitBreakers.TryGetValue(operationKey, out var circuitBreaker))
            {
                return new CircuitBreakerMetricsDto
                {
                    OperationKey = operationKey,
                    Status = circuitBreaker.Status,
                    FailureCount = circuitBreaker.FailureCount,
                    LastFailureTime = circuitBreaker.LastFailureTime,
                    LastSuccessTime = circuitBreaker.LastSuccessTime,
                    DurationOfBreak = circuitBreaker.DurationOfBreak,
                    ExceptionsAllowedBeforeBreaking = circuitBreaker.ExceptionsAllowedBeforeBreaking
                };
            }

            return null;
        }

        public async Task ResetAsync(string operationKey)
        {
            if (_circuitBreakers.TryGetValue(operationKey, out var circuitBreaker))
            {
                circuitBreaker.Status = "Closed";
                circuitBreaker.FailureCount = 0;
                circuitBreaker.LastFailureTime = null;
                _logger.LogInformation("Circuit breaker for {OperationKey} has been reset", operationKey);
            }
        }

        public async Task SetCircuitBreakerPolicyAsync(string operationKey, int exceptionsAllowedBeforeBreaking, TimeSpan durationOfBreak)
        {
            var circuitBreaker = _circuitBreakers.GetOrAdd(operationKey, key => new CircuitBreakerState());

            circuitBreaker.ExceptionsAllowedBeforeBreaking = exceptionsAllowedBeforeBreaking;
            circuitBreaker.DurationOfBreak = durationOfBreak;

            _logger.LogInformation(
                "Circuit breaker policy set for {OperationKey}: Exceptions allowed: {ExceptionsAllowed}, Duration: {Duration}",
                operationKey, exceptionsAllowedBeforeBreaking, durationOfBreak);
        }
    }
}