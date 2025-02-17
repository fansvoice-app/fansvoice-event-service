using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FansVoice.EventService.DTOs;

namespace FansVoice.EventService.Interfaces
{
    public interface IChantService
    {
        // Chant session management
        Task<ChantSessionDto> CreateChantSessionAsync(CreateChantSessionDto createDto, Guid createdBy);
        Task<ChantSessionDto> GetChantSessionByIdAsync(Guid id);
        Task<ChantSessionDto> UpdateChantSessionAsync(Guid id, UpdateChantSessionDto updateDto);
        Task<bool> DeleteChantSessionAsync(Guid id);

        // Chant session control
        Task<ChantSessionDto> StartChantSessionAsync(Guid sessionId, Guid userId);
        Task<ChantSessionDto> PauseChantSessionAsync(Guid sessionId, Guid userId);
        Task<ChantSessionDto> ResumeChantSessionAsync(Guid sessionId, Guid userId);
        Task<ChantSessionDto> StopChantSessionAsync(Guid sessionId, Guid userId);
        Task<ChantSessionDto> UpdateSessionPositionAsync(Guid sessionId, double position);

        // Participant management
        Task<ChantParticipantDto> JoinChantSessionAsync(Guid userId, Guid sessionId, string connectionId);
        Task<bool> LeaveChantSessionAsync(Guid userId, Guid sessionId);
        Task<List<ChantParticipantDto>> GetSessionParticipantsAsync(Guid sessionId);
        Task<bool> HandleUserDisconnectionAsync(Guid userId, string connectionId);
        Task<bool> UpdateUserLatencyAsync(Guid userId, Guid sessionId, string latency);

        // Session queries
        Task<List<ChantSessionDto>> GetActiveChantSessionsAsync();
        Task<List<ChantSessionDto>> GetChantSessionsByTeamAsync(Guid teamId);
        Task<ChantSessionDto> GetUserCurrentSessionAsync(Guid userId);

        // Metrics and statistics
        Task<ChantSessionMetricsDto> GetSessionMetricsAsync(Guid sessionId);
        Task<Dictionary<string, double>> GetTeamChantMetricsAsync(Guid teamId);
        Task<List<ChantSessionDto>> GetMostActiveChantSessionsAsync(int count);
        Task<List<ChantParticipantDto>> GetTopContributorsAsync(Guid sessionId, int count);

        // Validation and checks
        Task<bool> IsSessionFullAsync(Guid sessionId);
        Task<bool> CanUserJoinSessionAsync(Guid userId, Guid sessionId);
        Task<bool> IsUserInSessionAsync(Guid userId, Guid sessionId);
        Task<bool> HasUserPermissionAsync(Guid userId, Guid sessionId, string permission);
    }
}