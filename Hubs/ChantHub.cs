using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using FansVoice.EventService.Interfaces;
using FansVoice.EventService.DTOs;

namespace FansVoice.EventService.Hubs
{
    [Authorize]
    public class ChantHub : Hub
    {
        private readonly IChantService _chantService;
        private readonly ILogger<ChantHub> _logger;

        public ChantHub(IChantService chantService, ILogger<ChantHub> logger)
        {
            _chantService = chantService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                _logger.LogInformation("User connected: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                await _chantService.HandleUserDisconnectionAsync(userId, Context.ConnectionId);
                _logger.LogInformation("User disconnected: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                throw;
            }
        }

        public async Task JoinSession(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                var participant = await _chantService.JoinChantSessionAsync(userId, sessionId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");

                await Clients.Group($"session_{sessionId}").SendAsync("UserJoined", new
                {
                    UserId = userId,
                    ConnectionId = Context.ConnectionId,
                    JoinedAt = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task LeaveSession(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                await _chantService.LeaveChantSessionAsync(userId, sessionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");

                await Clients.Group($"session_{sessionId}").SendAsync("UserLeft", new
                {
                    UserId = userId,
                    ConnectionId = Context.ConnectionId,
                    LeftAt = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task UpdatePosition(Guid sessionId, double position)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                if (!await _chantService.HasUserPermissionAsync(userId, sessionId, "control"))
                {
                    throw new UnauthorizedAccessException("User does not have permission to control the session");
                }

                var session = await _chantService.UpdateSessionPositionAsync(sessionId, position);
                if (session != null)
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("PositionUpdated", new
                    {
                        Position = position,
                        LoopCount = session.LoopCount,
                        UpdatedBy = userId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating position for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task UpdateLatency(Guid sessionId, string latency)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                await _chantService.UpdateUserLatencyAsync(userId, sessionId, latency);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating latency for user {UserId} in session {SessionId}",
                    Context.User?.FindFirst("sub")?.Value, sessionId);
                throw;
            }
        }

        public async Task SendMessage(Guid sessionId, string message)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                if (await _chantService.IsUserInSessionAsync(userId, sessionId))
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", new
                    {
                        UserId = userId,
                        Message = message,
                        SentAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task StartChant(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                var session = await _chantService.StartChantSessionAsync(sessionId, userId);
                if (session != null)
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("ChantStarted", new
                    {
                        SessionId = sessionId,
                        StartedBy = userId,
                        StartedAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chant in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task PauseChant(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                var session = await _chantService.PauseChantSessionAsync(sessionId, userId);
                if (session != null)
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("ChantPaused", new
                    {
                        SessionId = sessionId,
                        PausedBy = userId,
                        PausedAt = DateTime.UtcNow,
                        Position = session.CurrentPosition
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing chant in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task ResumeChant(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                var session = await _chantService.ResumeChantSessionAsync(sessionId, userId);
                if (session != null)
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("ChantResumed", new
                    {
                        SessionId = sessionId,
                        ResumedBy = userId,
                        ResumedAt = DateTime.UtcNow,
                        Position = session.CurrentPosition
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming chant in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task StopChant(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                var session = await _chantService.StopChantSessionAsync(sessionId, userId);
                if (session != null)
                {
                    await Clients.Group($"session_{sessionId}").SendAsync("ChantStopped", new
                    {
                        SessionId = sessionId,
                        StoppedBy = userId,
                        StoppedAt = DateTime.UtcNow,
                        FinalPosition = session.CurrentPosition,
                        TotalDuration = session.DurationInSeconds,
                        LoopCount = session.LoopCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping chant in session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task RequestSync(Guid sessionId)
        {
            try
            {
                var userId = Guid.Parse(Context.User?.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());

                if (await _chantService.IsUserInSessionAsync(userId, sessionId))
                {
                    var session = await _chantService.GetChantSessionByIdAsync(sessionId);
                    if (session != null)
                    {
                        await Clients.Caller.SendAsync("SyncResponse", new
                        {
                            Position = session.CurrentPosition,
                            Status = session.Status,
                            LoopCount = session.LoopCount,
                            ParticipantCount = session.ParticipantCount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling sync request for session {SessionId}", sessionId);
                throw;
            }
        }
    }
}