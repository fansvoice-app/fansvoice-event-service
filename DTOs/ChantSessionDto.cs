using System;

namespace FansVoice.EventService.DTOs
{
    public class ChantSessionDto
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public string ChantName { get; set; }
        public Guid TeamId { get; set; }
        public string TeamName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
        public int ParticipantCount { get; set; }
        public int MaxParticipants { get; set; }
        public string? AudioUrl { get; set; }
        public string? LyricsUrl { get; set; }
        public int DurationInSeconds { get; set; }
        public double CurrentPosition { get; set; }
        public bool IsLooping { get; set; }
        public int LoopCount { get; set; }
        public double AverageLatency { get; set; }
        public int DisconnectionCount { get; set; }
        public int ReconnectionCount { get; set; }
        public int TotalUniqueParticipants { get; set; }
        public int PeakConcurrentUsers { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateChantSessionDto
    {
        public Guid EventId { get; set; }
        public string ChantName { get; set; }
        public Guid TeamId { get; set; }
        public string TeamName { get; set; }
        public string? AudioUrl { get; set; }
        public string? LyricsUrl { get; set; }
        public int DurationInSeconds { get; set; }
        public bool IsLooping { get; set; }
        public int MaxParticipants { get; set; }
    }

    public class UpdateChantSessionDto
    {
        public string? ChantName { get; set; }
        public string? Status { get; set; }
        public string? AudioUrl { get; set; }
        public string? LyricsUrl { get; set; }
        public bool? IsLooping { get; set; }
        public int? MaxParticipants { get; set; }
    }

    public class ChantSessionStatusUpdateDto
    {
        public string Status { get; set; }
        public double CurrentPosition { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ChantParticipantDto
    {
        public Guid UserId { get; set; }
        public string ConnectionId { get; set; }
        public DateTime JoinedAt { get; set; }
        public string LastKnownLatency { get; set; }
        public int DisconnectionCount { get; set; }
        public int ReconnectionCount { get; set; }
        public double TotalActiveMinutes { get; set; }
    }

    public class ChantSessionMetricsDto
    {
        public Guid SessionId { get; set; }
        public double AverageLatency { get; set; }
        public int TotalParticipants { get; set; }
        public int ActiveParticipants { get; set; }
        public int DisconnectionCount { get; set; }
        public int ReconnectionCount { get; set; }
        public double SessionDuration { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}