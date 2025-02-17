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
    public class ChantService : IChantService
    {
        private readonly EventDbContext _context;
        private readonly IMessageBusService _messageBus;
        private readonly ICacheService _cache;
        private readonly ILogger<ChantService> _logger;
        private readonly ICircuitBreakerService _circuitBreaker;

        public ChantService(
            EventDbContext context,
            IMessageBusService messageBus,
            ICacheService cache,
            ILogger<ChantService> logger,
            ICircuitBreakerService circuitBreaker)
        {
            _context = context;
            _messageBus = messageBus;
            _cache = cache;
            _logger = logger;
            _circuitBreaker = circuitBreaker;
        }

        // Chant session management
        public async Task<ChantSessionDto> CreateChantSessionAsync(CreateChantSessionDto createDto, Guid createdBy)
        {
            var session = new ChantSession
            {
                EventId = createDto.EventId,
                ChantName = createDto.ChantName,
                TeamId = createDto.TeamId,
                TeamName = createDto.TeamName,
                AudioUrl = createDto.AudioUrl,
                LyricsUrl = createDto.LyricsUrl,
                DurationInSeconds = createDto.DurationInSeconds,
                IsLooping = createDto.IsLooping,
                MaxParticipants = createDto.MaxParticipants,
                Status = "Preparing",
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            try
            {
                await _context.ChantSessions.AddAsync(session);
                await _context.SaveChangesAsync();

                // Update related event
                var @event = await _context.Events.FindAsync(createDto.EventId);
                if (@event != null)
                {
                    @event.ChantSessionId = session.Id;
                    @event.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants", new { Action = "created", Session = sessionDto });

                _logger.LogInformation("Chant session created successfully: {SessionId}", session.Id);
                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chant session");
                throw;
            }
        }

        public async Task<ChantSessionDto> GetChantSessionByIdAsync(Guid id)
        {
            var cacheKey = $"chant:session:{id}";

            try
            {
                return await _cache.GetOrSetAsync(cacheKey, async () =>
                {
                    var session = await _context.ChantSessions
                        .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

                    return session != null ? MapToChantSessionDto(session) : null;
                }, TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chant session: {SessionId}", id);
                throw;
            }
        }

        public async Task<ChantSessionDto> UpdateChantSessionAsync(Guid id, UpdateChantSessionDto updateDto)
        {
            var session = await _context.ChantSessions.FindAsync(id);
            if (session == null) return null;

            try
            {
                if (updateDto.ChantName != null)
                    session.ChantName = updateDto.ChantName;
                if (updateDto.Status != null)
                    session.Status = updateDto.Status;
                if (updateDto.AudioUrl != null)
                    session.AudioUrl = updateDto.AudioUrl;
                if (updateDto.LyricsUrl != null)
                    session.LyricsUrl = updateDto.LyricsUrl;
                if (updateDto.IsLooping.HasValue)
                    session.IsLooping = updateDto.IsLooping.Value;
                if (updateDto.MaxParticipants.HasValue)
                    session.MaxParticipants = updateDto.MaxParticipants.Value;

                session.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{id}");

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants", new { Action = "updated", Session = sessionDto });

                _logger.LogInformation("Chant session updated successfully: {SessionId}", id);
                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chant session: {SessionId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteChantSessionAsync(Guid id)
        {
            var session = await _context.ChantSessions.FindAsync(id);
            if (session == null) return false;

            try
            {
                session.IsActive = false;
                session.UpdatedAt = DateTime.UtcNow;

                // Update related event
                var @event = await _context.Events.FirstOrDefaultAsync(e => e.ChantSessionId == id);
                if (@event != null)
                {
                    @event.ChantSessionId = null;
                    @event.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{id}");

                await _messageBus.PublishAsync("chants", new { Action = "deleted", SessionId = id });

                _logger.LogInformation("Chant session deleted successfully: {SessionId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chant session: {SessionId}", id);
                throw;
            }
        }

        // Chant session control
        public async Task<ChantSessionDto> StartChantSessionAsync(Guid sessionId, Guid userId)
        {
            var session = await _context.ChantSessions.FindAsync(sessionId);
            if (session == null) return null;

            try
            {
                if (!await HasUserPermissionAsync(userId, sessionId, "start"))
                    throw new UnauthorizedAccessException("User does not have permission to start the session");

                session.Status = "Active";
                session.StartTime = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;
                session.CurrentPosition = 0;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{sessionId}");

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants.control", new
                {
                    Action = "started",
                    Session = sessionDto,
                    StartedBy = userId
                });

                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chant session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<ChantSessionDto> PauseChantSessionAsync(Guid sessionId, Guid userId)
        {
            var session = await _context.ChantSessions.FindAsync(sessionId);
            if (session == null) return null;

            try
            {
                if (!await HasUserPermissionAsync(userId, sessionId, "control"))
                    throw new UnauthorizedAccessException("User does not have permission to control the session");

                session.Status = "Paused";
                session.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{sessionId}");

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants.control", new
                {
                    Action = "paused",
                    Session = sessionDto,
                    PausedBy = userId
                });

                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing chant session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<ChantSessionDto> ResumeChantSessionAsync(Guid sessionId, Guid userId)
        {
            var session = await _context.ChantSessions.FindAsync(sessionId);
            if (session == null) return null;

            try
            {
                if (!await HasUserPermissionAsync(userId, sessionId, "control"))
                    throw new UnauthorizedAccessException("User does not have permission to control the session");

                session.Status = "Active";
                session.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{sessionId}");

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants.control", new
                {
                    Action = "resumed",
                    Session = sessionDto,
                    ResumedBy = userId
                });

                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming chant session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<ChantSessionDto> StopChantSessionAsync(Guid sessionId, Guid userId)
        {
            var session = await _context.ChantSessions.FindAsync(sessionId);
            if (session == null) return null;

            try
            {
                if (!await HasUserPermissionAsync(userId, sessionId, "control"))
                    throw new UnauthorizedAccessException("User does not have permission to control the session");

                session.Status = "Completed";
                session.EndTime = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _cache.RemoveAsync($"chant:session:{sessionId}");

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants.control", new
                {
                    Action = "stopped",
                    Session = sessionDto,
                    StoppedBy = userId
                });

                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping chant session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<ChantSessionDto> UpdateSessionPositionAsync(Guid sessionId, double position)
        {
            var session = await _context.ChantSessions.FindAsync(sessionId);
            if (session == null) return null;

            try
            {
                session.CurrentPosition = position;
                session.UpdatedAt = DateTime.UtcNow;

                if (session.IsLooping && position >= session.DurationInSeconds)
                {
                    session.LoopCount++;
                    session.CurrentPosition = 0;
                }

                await _context.SaveChangesAsync();

                var sessionDto = MapToChantSessionDto(session);
                await _messageBus.PublishAsync("chants.position", new
                {
                    SessionId = sessionId,
                    Position = position,
                    LoopCount = session.LoopCount
                });

                return sessionDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session position: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<ChantParticipantDto> JoinChantSessionAsync(Guid userId, Guid sessionId, string connectionId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session == null) throw new KeyNotFoundException("Session not found");

                if (!await CanUserJoinSessionAsync(userId, sessionId))
                    throw new InvalidOperationException("User cannot join this session");

                var participant = new EventParticipant
                {
                    EventId = session.EventId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow,
                    Status = "Active",
                    IsInChantSession = true,
                    ConnectionId = connectionId
                };

                await _context.EventParticipants.AddAsync(participant);
                session.ParticipantCount++;
                session.TotalUniqueParticipants++;

                if (session.ParticipantCount > session.PeakConcurrentUsers)
                    session.PeakConcurrentUsers = session.ParticipantCount;

                await _context.SaveChangesAsync();

                await _messageBus.PublishAsync("chants.participants", new
                {
                    Action = "joined",
                    SessionId = sessionId,
                    UserId = userId,
                    ConnectionId = connectionId
                });

                await transaction.CommitAsync();

                return new ChantParticipantDto
                {
                    UserId = userId,
                    ConnectionId = connectionId,
                    JoinedAt = participant.JoinedAt,
                    LastKnownLatency = "0",
                    DisconnectionCount = 0,
                    ReconnectionCount = 0,
                    TotalActiveMinutes = 0
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error joining chant session: {SessionId} for user: {UserId}", sessionId, userId);
                throw;
            }
        }

        public async Task<bool> LeaveChantSessionAsync(Guid userId, Guid sessionId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var participant = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.UserId == userId &&
                                            p.EventId == sessionId &&
                                            p.IsInChantSession &&
                                            p.Status == "Active");

                if (participant == null) return false;

                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.ParticipantCount--;
                    participant.Status = "Left";
                    participant.LeftAt = DateTime.UtcNow;
                    participant.IsInChantSession = false;
                    participant.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    await _messageBus.PublishAsync("chants.participants", new
                    {
                        Action = "left",
                        SessionId = sessionId,
                        UserId = userId,
                        ConnectionId = participant.ConnectionId
                    });

                    await transaction.CommitAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error leaving chant session: {SessionId} for user: {UserId}", sessionId, userId);
                throw;
            }
        }

        public async Task<List<ChantParticipantDto>> GetSessionParticipantsAsync(Guid sessionId)
        {
            try
            {
                var participants = await _context.EventParticipants
                    .Where(p => p.EventId == sessionId &&
                               p.IsInChantSession &&
                               p.Status == "Active")
                    .OrderBy(p => p.JoinedAt)
                    .ToListAsync();

                return participants.Select(p => new ChantParticipantDto
                {
                    UserId = p.UserId,
                    ConnectionId = p.ConnectionId,
                    JoinedAt = p.JoinedAt,
                    LastKnownLatency = p.LastKnownLatency,
                    DisconnectionCount = p.DisconnectionCount,
                    ReconnectionCount = p.ReconnectionCount,
                    TotalActiveMinutes = p.TotalActiveMinutes
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session participants: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> HandleUserDisconnectionAsync(Guid userId, string connectionId)
        {
            try
            {
                var participant = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.UserId == userId &&
                                            p.ConnectionId == connectionId &&
                                            p.IsInChantSession &&
                                            p.Status == "Active");

                if (participant == null) return false;

                participant.DisconnectionCount++;
                participant.UpdatedAt = DateTime.UtcNow;

                var session = await _context.ChantSessions.FindAsync(participant.EventId);
                if (session != null)
                {
                    session.DisconnectionCount++;
                    session.ParticipantCount--;
                }

                await _context.SaveChangesAsync();

                await _messageBus.PublishAsync("chants.participants", new
                {
                    Action = "disconnected",
                    SessionId = participant.EventId,
                    UserId = userId,
                    ConnectionId = connectionId
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user disconnection: {UserId}, {ConnectionId}", userId, connectionId);
                throw;
            }
        }

        public async Task<bool> UpdateUserLatencyAsync(Guid userId, Guid sessionId, string latency)
        {
            try
            {
                var participant = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.UserId == userId &&
                                            p.EventId == sessionId &&
                                            p.IsInChantSession &&
                                            p.Status == "Active");

                if (participant == null) return false;

                participant.LastKnownLatency = latency;
                participant.LastPingTime = DateTime.UtcNow;
                participant.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Update session average latency
                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session != null)
                {
                    var avgLatency = await _context.EventParticipants
                        .Where(p => p.EventId == sessionId &&
                                  p.IsInChantSession &&
                                  p.Status == "Active" &&
                                  p.LastKnownLatency != null)
                        .AverageAsync(p => double.Parse(p.LastKnownLatency));

                    session.AverageLatency = avgLatency;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user latency: {UserId}, {SessionId}", userId, sessionId);
                throw;
            }
        }

        public async Task<List<ChantSessionDto>> GetActiveChantSessionsAsync()
        {
            try
            {
                var sessions = await _context.ChantSessions
                    .Where(s => s.IsActive && s.Status == "Active")
                    .OrderByDescending(s => s.ParticipantCount)
                    .ToListAsync();

                return sessions.Select(MapToChantSessionDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active chant sessions");
                throw;
            }
        }

        public async Task<List<ChantSessionDto>> GetChantSessionsByTeamAsync(Guid teamId)
        {
            try
            {
                var sessions = await _context.ChantSessions
                    .Where(s => s.TeamId == teamId && s.IsActive)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();

                return sessions.Select(MapToChantSessionDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chant sessions for team: {TeamId}", teamId);
                throw;
            }
        }

        public async Task<ChantSessionDto> GetUserCurrentSessionAsync(Guid userId)
        {
            try
            {
                var participant = await _context.EventParticipants
                    .FirstOrDefaultAsync(p => p.UserId == userId &&
                                            p.IsInChantSession &&
                                            p.Status == "Active");

                if (participant == null) return null;

                var session = await _context.ChantSessions
                    .FirstOrDefaultAsync(s => s.EventId == participant.EventId && s.IsActive);

                return session != null ? MapToChantSessionDto(session) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<ChantSessionMetricsDto> GetSessionMetricsAsync(Guid sessionId)
        {
            try
            {
                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session == null) return null;

                var activeParticipants = await _context.EventParticipants
                    .CountAsync(p => p.EventId == sessionId &&
                                   p.IsInChantSession &&
                                   p.Status == "Active");

                return new ChantSessionMetricsDto
                {
                    SessionId = sessionId,
                    AverageLatency = session.AverageLatency,
                    TotalParticipants = session.TotalUniqueParticipants,
                    ActiveParticipants = activeParticipants,
                    DisconnectionCount = session.DisconnectionCount,
                    ReconnectionCount = session.ReconnectionCount,
                    SessionDuration = (session.EndTime ?? DateTime.UtcNow).Subtract(session.StartTime).TotalMinutes,
                    LastUpdateTime = session.UpdatedAt ?? session.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session metrics: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<Dictionary<string, double>> GetTeamChantMetricsAsync(Guid teamId)
        {
            try
            {
                var metrics = new Dictionary<string, double>();
                var sessions = await _context.ChantSessions
                    .Where(s => s.TeamId == teamId && s.IsActive)
                    .ToListAsync();

                metrics["TotalSessions"] = sessions.Count;
                metrics["TotalParticipants"] = sessions.Sum(s => s.TotalUniqueParticipants);
                metrics["AverageParticipantsPerSession"] = sessions.Any() ? sessions.Average(s => s.ParticipantCount) : 0;
                metrics["AverageSessionDuration"] = sessions.Any() ? sessions.Average(s => s.DurationInSeconds) : 0;
                metrics["TotalDisconnections"] = sessions.Sum(s => s.DisconnectionCount);
                metrics["AverageLatency"] = sessions.Any() ? sessions.Average(s => s.AverageLatency) : 0;

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team chant metrics: {TeamId}", teamId);
                throw;
            }
        }

        public async Task<List<ChantSessionDto>> GetMostActiveChantSessionsAsync(int count)
        {
            try
            {
                var sessions = await _context.ChantSessions
                    .Where(s => s.IsActive && s.Status == "Active")
                    .OrderByDescending(s => s.ParticipantCount)
                    .ThenByDescending(s => s.StartTime)
                    .Take(count)
                    .ToListAsync();

                return sessions.Select(MapToChantSessionDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most active chant sessions");
                throw;
            }
        }

        public async Task<List<ChantParticipantDto>> GetTopContributorsAsync(Guid sessionId, int count)
        {
            try
            {
                var participants = await _context.EventParticipants
                    .Where(p => p.EventId == sessionId && p.IsInChantSession)
                    .OrderByDescending(p => p.TotalActiveMinutes)
                    .ThenBy(p => p.DisconnectionCount)
                    .Take(count)
                    .ToListAsync();

                return participants.Select(p => new ChantParticipantDto
                {
                    UserId = p.UserId,
                    ConnectionId = p.ConnectionId,
                    JoinedAt = p.JoinedAt,
                    LastKnownLatency = p.LastKnownLatency,
                    DisconnectionCount = p.DisconnectionCount,
                    ReconnectionCount = p.ReconnectionCount,
                    TotalActiveMinutes = p.TotalActiveMinutes
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top contributors for session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> IsSessionFullAsync(Guid sessionId)
        {
            try
            {
                var session = await _context.ChantSessions.FindAsync(sessionId);
                return session != null && session.ParticipantCount >= session.MaxParticipants;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if session is full: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> CanUserJoinSessionAsync(Guid userId, Guid sessionId)
        {
            try
            {
                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session == null || !session.IsActive) return false;

                if (session.Status != "Preparing" && session.Status != "Active")
                    return false;

                if (session.ParticipantCount >= session.MaxParticipants)
                    return false;

                var existingParticipation = await _context.EventParticipants
                    .AnyAsync(p => p.EventId == sessionId &&
                                 p.UserId == userId &&
                                 p.IsInChantSession &&
                                 p.Status == "Active");

                return !existingParticipation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user can join session: {SessionId}, {UserId}", sessionId, userId);
                throw;
            }
        }

        public async Task<bool> IsUserInSessionAsync(Guid userId, Guid sessionId)
        {
            try
            {
                return await _context.EventParticipants
                    .AnyAsync(p => p.EventId == sessionId &&
                                 p.UserId == userId &&
                                 p.IsInChantSession &&
                                 p.Status == "Active");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user is in session: {SessionId}, {UserId}", sessionId, userId);
                throw;
            }
        }

        public async Task<bool> HasUserPermissionAsync(Guid userId, Guid sessionId, string permission)
        {
            try
            {
                var session = await _context.ChantSessions.FindAsync(sessionId);
                if (session == null) return false;

                // Şimdilik sadece session'ı oluşturan kullanıcı kontrol edebilir
                return session.CreatedBy == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user permission: {SessionId}, {UserId}, {Permission}",
                    sessionId, userId, permission);
                throw;
            }
        }

        private static ChantSessionDto MapToChantSessionDto(ChantSession session)
        {
            return new ChantSessionDto
            {
                Id = session.Id,
                EventId = session.EventId,
                ChantName = session.ChantName,
                TeamId = session.TeamId,
                TeamName = session.TeamName,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Status = session.Status,
                ParticipantCount = session.ParticipantCount,
                MaxParticipants = session.MaxParticipants,
                AudioUrl = session.AudioUrl,
                LyricsUrl = session.LyricsUrl,
                DurationInSeconds = session.DurationInSeconds,
                CurrentPosition = session.CurrentPosition,
                IsLooping = session.IsLooping,
                LoopCount = session.LoopCount,
                AverageLatency = session.AverageLatency,
                DisconnectionCount = session.DisconnectionCount,
                ReconnectionCount = session.ReconnectionCount,
                TotalUniqueParticipants = session.TotalUniqueParticipants,
                PeakConcurrentUsers = session.PeakConcurrentUsers,
                IsActive = session.IsActive
            };
        }
    }
}