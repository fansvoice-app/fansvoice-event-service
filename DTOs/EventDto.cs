using System;

namespace FansVoice.EventService.DTOs
{
    public class EventDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Guid TeamId { get; set; }
        public string EventType { get; set; }
        public string Status { get; set; }
        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }
        public bool IsChantEvent { get; set; }
        public Guid? ChantSessionId { get; set; }
        public string? Location { get; set; }
        public string? StreamUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateEventDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public Guid TeamId { get; set; }
        public string EventType { get; set; }
        public int MaxParticipants { get; set; }
        public bool IsChantEvent { get; set; }
        public string? Location { get; set; }
        public string? StreamUrl { get; set; }
    }

    public class UpdateEventDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Status { get; set; }
        public int? MaxParticipants { get; set; }
        public string? Location { get; set; }
        public string? StreamUrl { get; set; }
    }

    public class EventParticipationDto
    {
        public Guid EventId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public string Status { get; set; }
    }

    public class EventStatusUpdateDto
    {
        public string Status { get; set; }
        public string? Reason { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class EventSearchDto
    {
        public Guid? TeamId { get; set; }
        public string? EventType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public bool? IsChantEvent { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}