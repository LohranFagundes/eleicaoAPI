using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotingController : ControllerBase
{
    private readonly IVotingService _votingService;
    private readonly IAuditService _auditService;
    private readonly ILogger<VotingController> _logger;

    public VotingController(IVotingService votingService, IAuditService auditService, ILogger<VotingController> logger)
    {
        _votingService = votingService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginVoter([FromBody] VotingLoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Dados de login inválidos", ModelState));
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _votingService.LoginVoterAsync(loginDto, ipAddress, userAgent);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during voter login");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("election/{electionId}/status")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> GetElectionStatus(int electionId)
    {
        try
        {
            var voterId = GetCurrentVoterId();
            if (!voterId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Voter não autenticado"));
            }

            var result = await _votingService.GetElectionStatusAsync(electionId, voterId.Value);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting election status");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpPost("cast-vote")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> CastVote([FromBody] VotingCastVoteDto voteDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Dados do voto inválidos", ModelState));
        }

        try
        {
            var voterId = GetCurrentVoterId();
            if (!voterId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Voter não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _votingService.CastVoteAsync(voteDto, voterId.Value, ipAddress, userAgent);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("receipt/{receiptToken}")]
    public async Task<IActionResult> GetVoteReceipt(string receiptToken)
    {
        try
        {
            var result = await _votingService.GetVoteReceiptAsync(receiptToken);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return NotFound(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vote receipt");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("has-voted/{electionId}")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> HasVoterVoted(int electionId)
    {
        try
        {
            var voterId = GetCurrentVoterId();
            if (!voterId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Voter não autenticado"));
            }

            var result = await _votingService.HasVoterVotedAsync(voterId.Value, electionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if voter has voted");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpPost("seal")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SealElection([FromBody] ElectionSealDto sealDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Dados de lacre inválidos", ModelState));
        }

        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            // DEPRECATED: Use /api/election/{id}/seal endpoint instead
            return BadRequest(ApiResponse<object>.ErrorResult("Endpoint depreciado. Use /api/election/{id}/seal"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sealing election");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpPost("zero-report/{electionId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GenerateZeroReport(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var result = await _votingService.GenerateZeroReportAsync(electionId, adminId.Value, ipAddress);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating zero report");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("integrity-report/{electionId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ValidateElectionIntegrity(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (adminId.HasValue)
            {
                await _auditService.LogAsync(adminId.Value, "admin", "validate_integrity", "election", electionId,
                    "Requested integrity validation report");
            }

            var result = await _votingService.ValidateElectionIntegrityAsync(electionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating election integrity");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("can-vote/{electionId}")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> CanVoteInElection(int electionId)
    {
        try
        {
            var voterId = GetCurrentVoterId();
            if (!voterId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Voter não autenticado"));
            }

            var result = await _votingService.CanVoteInElectionAsync(voterId.Value, electionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if voter can vote");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private int? GetCurrentVoterId()
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != "voter") return null;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var voterId) ? voterId : null;
    }
}