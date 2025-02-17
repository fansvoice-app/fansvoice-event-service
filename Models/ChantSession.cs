using System;
using System.ComponentModel.DataAnnotations;

namespace FansVoice.EventService.Models
{
    public class ChantSession
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid EventId { get; set; }

        [Required]
        [StringLength(100)]
        public required string ChantName { get; set; }

        [Required]
        public Guid TeamId { get; set; }

        [StringLength(100)]
        public required string TeamName { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public string Status { get; set; } = "Preparing"; // Preparing, Active, Completed, Cancelled

        public int ParticipantCount { get; set; }
        public int MaxParticipants { get; set; } = 1000;

        public string? AudioUrl { get; set; }
        public string? LyricsUrl { get; set; }

        public int DurationInSeconds { get; set; }
        public double CurrentPosition { get; set; } // Şarkının anlık pozisyonu (saniye)
        public bool IsLooping { get; set; }
        public int LoopCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Guid CreatedBy { get; set; }
        public bool IsActive { get; set; } = true;

        // Performans metrikleri
        public double AverageLatency { get; set; }
        public int DisconnectionCount { get; set; }
        public int ReconnectionCount { get; set; }
        public int TotalUniqueParticipants { get; set; }
        public int PeakConcurrentUsers { get; set; }
    }
}