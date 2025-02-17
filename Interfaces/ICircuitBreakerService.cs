using System;
using System.Threading.Tasks;

namespace FansVoice.EventService.Interfaces
{
    public interface ICircuitBreakerService
    {
        Task<T> ExecuteAsync<T>(string operationKey, Func<Task<T>> operation);
        Task<bool> IsCircuitBreakerOpenAsync(string operationKey);
        Task<CircuitBreakerMetrics> GetMetricsAsync(string operationKey);
        Task ResetAsync(string operationKey);
        Task SetCircuitBreakerPolicyAsync(string operationKey, int exceptionsAllowedBeforeBreaking, TimeSpan durationOfBreak);
    }

    public class CircuitBreakerMetrics
    {
        public string OperationKey { get; set; }
        public string Status { get; set; } // Open, Closed, HalfOpen
        public int FailureCount { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public TimeSpan DurationOfBreak { get; set; }
        public int ExceptionsAllowedBeforeBreaking { get; set; }
    }
}
