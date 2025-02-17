using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FansVoice.EventService.DTOs;

namespace FansVoice.EventService.Interfaces
{
    public interface IEventService
    {
        // Event CRUD operations
        Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, Guid createdBy);
        Task<EventDto> GetEventByIdAsync(Guid id);
        Task<EventDto> UpdateEventAsync(Guid id, UpdateEventDto updateEventDto);
        Task<bool> DeleteEventAsync(Guid id);

        // Event search and filtering
        Task<(List<EventDto> Events, int TotalCount)> SearchEventsAsync(EventSearchDto searchDto);
        Task<List<EventDto>> GetUpcomingEventsByTeamAsync(Guid teamId, int count);
        Task<List<EventDto>> GetActiveEventsByTypeAsync(string eventType, int count);

        // Event participation
        Task<EventParticipationDto> JoinEventAsync(Guid eventId, Guid userId);
        Task<EventParticipationDto> LeaveEventAsync(Guid eventId, Guid userId);
        Task<List<EventParticipationDto>> GetEventParticipantsAsync(Guid eventId);
        Task<int> GetEventParticipantCountAsync(Guid eventId);

        // Event status management
        Task<EventDto> UpdateEventStatusAsync(Guid id, EventStatusUpdateDto statusDto);
        Task<bool> CancelEventAsync(Guid id, string reason);
        Task<bool> CompleteEventAsync(Guid id);

        // Event statistics and metrics
        Task<Dictionary<string, int>> GetEventStatisticsByTeamAsync(Guid teamId);
        Task<Dictionary<string, int>> GetEventStatisticsByTypeAsync(string eventType);
        Task<List<EventDto>> GetMostPopularEventsAsync(int count);

        // Event validation and checks
        Task<bool> IsEventFullAsync(Guid eventId);
        Task<bool> CanUserJoinEventAsync(Guid eventId, Guid userId);
        Task<bool> IsUserParticipatingAsync(Guid eventId, Guid userId);
    }
}