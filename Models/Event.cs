using System;
using System.ComponentModel.DataAnnotations;

namespace FansVoice.EventService.Models
{
    public class Event
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Title { get; set; }

        [Required]
        [StringLength(500)]
        public required string Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Required]
        public Guid TeamId { get; set; }

        [Required]
        public string EventType { get; set; } // Match, Chant, Training, etc.

        public string Status { get; set; } = "Scheduled"; // Scheduled, Active, Completed, Cancelled

        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }

        public bool IsChantEvent { get; set; }
        public Guid? ChantSessionId { get; set; }

        public string? Location { get; set; }
        public string? StreamUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Guid CreatedBy { get; set; }
        public bool IsActive { get; set; } = true;
    }
}