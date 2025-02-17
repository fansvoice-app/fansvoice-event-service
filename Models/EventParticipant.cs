using System;
using System.ComponentModel.DataAnnotations;

namespace FansVoice.EventService.Models
{
    public class EventParticipant
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid EventId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }

        public string Status { get; set; } = "Active"; // Active, Left, Kicked, Banned
        public string? LeaveReason { get; set; }

        // Chant spesifik alanlar
        public bool IsInChantSession { get; set; }
        public string? LastKnownLatency { get; set; }
        public DateTime? LastPingTime { get; set; }
        public string? ConnectionId { get; set; }
        public int DisconnectionCount { get; set; }
        public int ReconnectionCount { get; set; }
        public double TotalActiveMinutes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}