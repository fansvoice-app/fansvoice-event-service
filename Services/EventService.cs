using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FansVoice.EventService.Data;
using FansVoice.EventService.DTOs;
using FansVoice.EventService.Interfaces;
using FansVoice.EventService.Models;

namespace FansVoice.EventService.Services
{
    public class EventService : IEventService
    {
        private readonly EventDbContext _context;
        private readonly IMessageBusService _messageBus;
        private readonly ICacheService _cache;
        private readonly ILogger<EventService> _logger;
        private readonly ICircuitBreakerService _circuitBreaker;

        public EventService(
            EventDbContext context,
            IMessageBusService messageBus,
            ICacheService cache,
            ILogger<EventService> logger,
            ICircuitBreakerService circuitBreaker)
        {
            _context = context;
            _messageBus = messageBus;
            _cache = cache;
            _logger = logger;
            _circuitBreaker = circuitBreaker;
        }

        // Event CRUD operations
        public async Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, Guid createdBy)
        {
            var @event = new Event
            {
                Title = createEventDto.Title,
                Description = createEventDto.Description,
                StartTime = createEventDto.StartTime,
                EndTime = createEventDto.EndTime,
                TeamId = createEventDto.TeamId,
                EventType = createEventDto.EventType,
                MaxParticipants = createEventDto.MaxParticipants,
                IsChantEvent = createEventDto.IsChantEvent,
                Location = createEventDto.Location,
                StreamUrl = createEventDto.StreamUrl,
                CreatedBy = createdBy,
                Status = "Scheduled",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            try
            {
                await _context.Events.AddAsync(@event);
                await _context.SaveChangesAsync();

                var eventDto = MapToEventDto(@event);
                await _messageBus.PublishAsync("events", new { Action = "created", Event = eventDto });

                _logger.LogInformation("Event created successfully: {EventId}", @event.Id);
                return eventDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event");
                throw;
            }
        }

        public async Task<EventDto> GetEventByIdAsync(Guid id)
        {
            var cacheKey = $"event:{id}";

            try
            {
                return await _cache.GetOrSetAsync(cacheKey, async () =>
                {
                    var @event = await _context.Events
                        .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

                    return @event != null ? MapToEventDto(@event) : null;
                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event: {EventId}", id);
                throw;
            }
        }

        public async Task<EventDto> UpdateEventAsync(Guid id, UpdateEventDto updateEventDto)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return null;

            try
            {
                if (updateEventDto.Title != null)
                    @event.Title = updateEventDto.Title;
                if (updateEventDto.Description != null)
                    @event.Description = updateEventDto.Description;
                if (updateEventDto.StartTime.HasValue)
                    @event.StartTime = updateEventDto.StartTime.Value;
                if (updateEventDto.EndTime.HasValue)
                    @event.EndTime = updateEventDto.EndTime;
                if (updateEventDto.Status != null)
                    @event.Status = updateEventDto.Status;
                if (updateEventDto.MaxParticipants.HasValue)
                    @event.MaxParticipants = updateEventDto.MaxParticipants.Value;
                if (updateEventDto.Location != null)
                    @event.Location = updateEventDto.Location;
                if (updateEventDto.StreamUrl != null)
                    @event.StreamUrl = updateEventDto.StreamUrl;

                @event.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"event:{id}");

                var eventDto = MapToEventDto(@event);
                await _messageBus.PublishAsync("events", new { Action = "updated", Event = eventDto });

                _logger.LogInformation("Event updated successfully: {EventId}", id);
                return eventDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event: {EventId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteEventAsync(Guid id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return false;

            try
            {
                @event.IsActive = false;
                @event.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"event:{id}");

                await _messageBus.PublishAsync("events", new { Action = "deleted", EventId = id });

                _logger.LogInformation("Event deleted successfully: {EventId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event: {EventId}", id);
                throw;
            }
        }

        // Event search and filtering
        public async Task<(List<EventDto> Events, int TotalCount)> SearchEventsAsync(EventSearchDto searchDto)
        {
            try
            {
                var query = _context.Events.Where(e => e.IsActive);

                if (searchDto.TeamId.HasValue)
                    query = query.Where(e => e.TeamId == searchDto.TeamId);

                if (!string.IsNullOrEmpty(searchDto.EventType))
                    query = query.Where(e => e.EventType == searchDto.EventType);

                if (searchDto.StartDate.HasValue)
                    query = query.Where(e => e.StartTime >= searchDto.StartDate);

                if (searchDto.EndDate.HasValue)
                    query = query.Where(e => e.StartTime <= searchDto.EndDate);

                if (!string.IsNullOrEmpty(searchDto.Status))
                    query = query.Where(e => e.Status == searchDto.Status);

                if (searchDto.IsChantEvent.HasValue)
                    query = query.Where(e => e.IsChantEvent == searchDto.IsChantEvent);

                var totalCount = await query.CountAsync();

                var events = await query
                    .OrderByDescending(e => e.StartTime)
                    .Skip((searchDto.Page - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                return (events.Select(MapToEventDto).ToList(), totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching events");
                throw;
            }
        }

        public async Task<List<EventDto>> GetUpcomingEventsByTeamAsync(Guid teamId, int count)
        {
            try
            {
                var events = await _context.Events
                    .Where(e => e.TeamId == teamId &&
                           e.IsActive &&
                           e.StartTime > DateTime.UtcNow &&
                           e.Status == "Scheduled")
                    .OrderBy(e => e.StartTime)
                    .Take(count)
                    .ToListAsync();

                return events.Select(MapToEventDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming events for team: {TeamId}", teamId);
                throw;
            }
        }

        public async Task<List<EventDto>> GetActiveEventsByTypeAsync(string eventType, int count)
        {
            try
            {
                var events = await _context.Events
                    .Where(e => e.EventType == eventType &&
                           e.IsActive &&
                           e.Status == "Active")
                    .OrderByDescending(e => e.StartTime)
                    .Take(count)
                    .ToListAsync();

                return events.Select(MapToEventDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active events by type: {EventType}", eventType);
                throw;
            }
        }

        public async Task<EventParticipationDto> JoinEventAsync(Guid eventId, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var @event = await _context.Events.FindAsync(eventId);
                if (@event == null) throw new KeyNotFoundException("Event not found");

                if (!await CanUserJoinEventAsync(eventId, userId))
                    throw new InvalidOperationException("User cannot join this event");

                var participant = new EventParticipant
                {
                    EventId = eventId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow,
                    Status = "Active"
                };

                await _context.EventParticipants.AddAsync(participant);
                @event.CurrentParticipants++;
                await _context.SaveChangesAsync();

                await _messageBus.PublishAsync("events.participants", new
                {
                    Action = "joined",
                    EventId = eventId,
                    UserId = userId,
                    JoinedAt = participant.JoinedAt
                });

                await transaction.CommitAsync();

                return new EventParticipationDto
                {
                    EventId = eventId,
                    UserId = userId,
                    JoinedAt = participant.JoinedAt,
                    Status = participant.Status
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error joining event: {EventId} for user: {UserId}", eventId, userId);
                throw;
            }
        }

        public async Task<EventParticipationDto> LeaveEventAsync(Guid eventId, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var participant = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.EventId == eventId &&
                                            p.UserId == userId &&
                                            p.Status == "Active");

                if (participant == null)
                    throw new KeyNotFoundException("Active participation not found");

                participant.Status = "Left";
                participant.LeftAt = DateTime.UtcNow;
                participant.UpdatedAt = DateTime.UtcNow;

                var @event = await _context.Events.FindAsync(eventId);
                if (@event != null)
                {
                    @event.CurrentParticipants--;
                }

                await _context.SaveChangesAsync();

                await _messageBus.PublishAsync("events.participants", new
                {
                    Action = "left",
                    EventId = eventId,
                    UserId = userId,
                    LeftAt = participant.LeftAt
                });

                await transaction.CommitAsync();

                return new EventParticipationDto
                {
                    EventId = eventId,
                    UserId = userId,
                    JoinedAt = participant.JoinedAt,
                    Status = participant.Status
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error leaving event: {EventId} for user: {UserId}", eventId, userId);
                throw;
            }
        }

        public async Task<List<EventParticipationDto>> GetEventParticipantsAsync(Guid eventId)
        {
            try
            {
                var participants = await _context.EventParticipants
                    .Where(p => p.EventId == eventId && p.Status == "Active")
                    .OrderBy(p => p.JoinedAt)
                    .ToListAsync();

                return participants.Select(p => new EventParticipationDto
                {
                    EventId = p.EventId,
                    UserId = p.UserId,
                    JoinedAt = p.JoinedAt,
                    Status = p.Status
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting participants for event: {EventId}", eventId);
                throw;
            }
        }

        public async Task<int> GetEventParticipantCountAsync(Guid eventId)
        {
            try
            {
                return await _context.EventParticipants
                    .CountAsync(p => p.EventId == eventId && p.Status == "Active");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting participant count for event: {EventId}", eventId);
                throw;
            }
        }

        public async Task<EventDto> UpdateEventStatusAsync(Guid id, EventStatusUpdateDto statusDto)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return null;

            try
            {
                @event.Status = statusDto.Status;
                @event.UpdatedAt = statusDto.UpdatedAt;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"event:{id}");

                var eventDto = MapToEventDto(@event);
                await _messageBus.PublishAsync("events.status", new
                {
                    Action = "status_updated",
                    Event = eventDto,
                    Reason = statusDto.Reason
                });

                return eventDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event status: {EventId}", id);
                throw;
            }
        }

        public async Task<bool> CancelEventAsync(Guid id, string reason)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return false;

            try
            {
                @event.Status = "Cancelled";
                @event.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"event:{id}");

                await _messageBus.PublishAsync("events.status", new
                {
                    Action = "cancelled",
                    EventId = id,
                    Reason = reason
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling event: {EventId}", id);
                throw;
            }
        }

        public async Task<bool> CompleteEventAsync(Guid id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return false;

            try
            {
                @event.Status = "Completed";
                @event.EndTime = DateTime.UtcNow;
                @event.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"event:{id}");

                await _messageBus.PublishAsync("events.status", new
                {
                    Action = "completed",
                    EventId = id
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing event: {EventId}", id);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetEventStatisticsByTeamAsync(Guid teamId)
        {
            try
            {
                var stats = new Dictionary<string, int>
                {
                    ["TotalEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive),
                    ["ScheduledEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive && e.Status == "Scheduled"),
                    ["ActiveEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive && e.Status == "Active"),
                    ["CompletedEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive && e.Status == "Completed"),
                    ["CancelledEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive && e.Status == "Cancelled"),
                    ["ChantEvents"] = await _context.Events.CountAsync(e => e.TeamId == teamId && e.IsActive && e.IsChantEvent)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event statistics for team: {TeamId}", teamId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetEventStatisticsByTypeAsync(string eventType)
        {
            try
            {
                var stats = new Dictionary<string, int>
                {
                    ["TotalEvents"] = await _context.Events.CountAsync(e => e.EventType == eventType && e.IsActive),
                    ["ScheduledEvents"] = await _context.Events.CountAsync(e => e.EventType == eventType && e.IsActive && e.Status == "Scheduled"),
                    ["ActiveEvents"] = await _context.Events.CountAsync(e => e.EventType == eventType && e.IsActive && e.Status == "Active"),
                    ["CompletedEvents"] = await _context.Events.CountAsync(e => e.EventType == eventType && e.IsActive && e.Status == "Completed"),
                    ["CancelledEvents"] = await _context.Events.CountAsync(e => e.EventType == eventType && e.IsActive && e.Status == "Cancelled"),
                    ["AverageParticipants"] = (int)await _context.Events.Where(e => e.EventType == eventType && e.IsActive).AverageAsync(e => e.CurrentParticipants)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event statistics for type: {EventType}", eventType);
                throw;
            }
        }

        public async Task<List<EventDto>> GetMostPopularEventsAsync(int count)
        {
            try
            {
                var events = await _context.Events
                    .Where(e => e.IsActive && (e.Status == "Active" || e.Status == "Scheduled"))
                    .OrderByDescending(e => e.CurrentParticipants)
                    .Take(count)
                    .ToListAsync();

                return events.Select(MapToEventDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most popular events");
                throw;
            }
        }

        public async Task<bool> IsEventFullAsync(Guid eventId)
        {
            try
            {
                var @event = await _context.Events.FindAsync(eventId);
                return @event != null && @event.CurrentParticipants >= @event.MaxParticipants;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if event is full: {EventId}", eventId);
                throw;
            }
        }

        public async Task<bool> CanUserJoinEventAsync(Guid eventId, Guid userId)
        {
            try
            {
                var @event = await _context.Events.FindAsync(eventId);
                if (@event == null || !@event.IsActive) return false;

                if (@event.Status != "Scheduled" && @event.Status != "Active")
                    return false;

                if (@event.CurrentParticipants >= @event.MaxParticipants)
                    return false;

                var existingParticipation = await _context.EventParticipants
                    .AnyAsync(p => p.EventId == eventId &&
                                 p.UserId == userId &&
                                 p.Status == "Active");

                return !existingParticipation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user can join event: {EventId}, {UserId}", eventId, userId);
                throw;
            }
        }

        public async Task<bool> IsUserParticipatingAsync(Guid eventId, Guid userId)
        {
            try
            {
                return await _context.EventParticipants
                    .AnyAsync(p => p.EventId == eventId &&
                                 p.UserId == userId &&
                                 p.Status == "Active");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user is participating: {EventId}, {UserId}", eventId, userId);
                throw;
            }
        }

        private static EventDto MapToEventDto(Event @event)
        {
            return new EventDto
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartTime = @event.StartTime,
                EndTime = @event.EndTime,
                TeamId = @event.TeamId,
                EventType = @event.EventType,
                Status = @event.Status,
                MaxParticipants = @event.MaxParticipants,
                CurrentParticipants = @event.CurrentParticipants,
                IsChantEvent = @event.IsChantEvent,
                ChantSessionId = @event.ChantSessionId,
                Location = @event.Location,
                StreamUrl = @event.StreamUrl,
                CreatedAt = @event.CreatedAt,
                CreatedBy = @event.CreatedBy,
                IsActive = @event.IsActive
            };
        }
    }
}