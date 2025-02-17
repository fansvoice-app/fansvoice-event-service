using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FansVoice.EventService.DTOs;
using FansVoice.EventService.Interfaces;

namespace FansVoice.EventService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChantController : ControllerBase
    {
        private readonly IChantService _chantService;
        private readonly ILogger<ChantController> _logger;

        public ChantController(IChantService chantService, ILogger<ChantController> logger)
        {
            _chantService = chantService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ChantSessionDto>> CreateChantSession(CreateChantSessionDto createDto)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.CreateChantSessionAsync(createDto, userId);
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chant session");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChantSessionDto>> GetChantSession(Guid id)
        {
            try
            {
                var session = await _chantService.GetChantSessionByIdAsync(id);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChantSessionDto>> UpdateChantSession(Guid id, UpdateChantSessionDto updateDto)
        {
            try
            {
                var session = await _chantService.UpdateChantSessionAsync(id, updateDto);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteChantSession(Guid id)
        {
            try
            {
                var result = await _chantService.DeleteChantSessionAsync(id);
                if (!result) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/start")]
        public async Task<ActionResult<ChantSessionDto>> StartChantSession(Guid id)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.StartChantSessionAsync(id, userId);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/pause")]
        public async Task<ActionResult<ChantSessionDto>> PauseChantSession(Guid id)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.PauseChantSessionAsync(id, userId);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/resume")]
        public async Task<ActionResult<ChantSessionDto>> ResumeChantSession(Guid id)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.ResumeChantSessionAsync(id, userId);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{id}/stop")]
        public async Task<ActionResult<ChantSessionDto>> StopChantSession(Guid id)
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.StopChantSessionAsync(id, userId);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping chant session: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<ActionResult<List<ChantSessionDto>>> GetActiveChantSessions()
        {
            try
            {
                var sessions = await _chantService.GetActiveChantSessionsAsync();
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active chant sessions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("team/{teamId}")]
        public async Task<ActionResult<List<ChantSessionDto>>> GetChantSessionsByTeam(Guid teamId)
        {
            try
            {
                var sessions = await _chantService.GetChantSessionsByTeamAsync(teamId);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chant sessions for team: {TeamId}", teamId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/metrics")]
        public async Task<ActionResult<ChantSessionMetricsDto>> GetSessionMetrics(Guid id)
        {
            try
            {
                var metrics = await _chantService.GetSessionMetricsAsync(id);
                if (metrics == null) return NotFound();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session metrics: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("team/{teamId}/metrics")]
        public async Task<ActionResult<Dictionary<string, double>>> GetTeamChantMetrics(Guid teamId)
        {
            try
            {
                var metrics = await _chantService.GetTeamChantMetricsAsync(teamId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team chant metrics: {TeamId}", teamId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active/top/{count}")]
        public async Task<ActionResult<List<ChantSessionDto>>> GetMostActiveChantSessions(int count = 10)
        {
            try
            {
                if (count <= 0 || count > 100) count = 10;
                var sessions = await _chantService.GetMostActiveChantSessionsAsync(count);
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most active chant sessions");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/participants")]
        public async Task<ActionResult<List<ChantParticipantDto>>> GetSessionParticipants(Guid id)
        {
            try
            {
                var participants = await _chantService.GetSessionParticipantsAsync(id);
                return Ok(participants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session participants: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}/top-contributors/{count}")]
        public async Task<ActionResult<List<ChantParticipantDto>>> GetTopContributors(Guid id, int count = 10)
        {
            try
            {
                if (count <= 0 || count > 100) count = 10;
                var contributors = await _chantService.GetTopContributorsAsync(id, count);
                return Ok(contributors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top contributors: {SessionId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("user/current")]
        public async Task<ActionResult<ChantSessionDto>> GetUserCurrentSession()
        {
            try
            {
                var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
                var session = await _chantService.GetUserCurrentSessionAsync(userId);
                if (session == null) return NotFound();
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session for user");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}