using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/voting-test")]
[Authorize(Roles = "admin")]
public class VotingTestController : ControllerBase
{
    private readonly IVotingService _votingService;
    private readonly IElectionService _electionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<VotingTestController> _logger;

    public VotingTestController(
        IVotingService votingService,
        IElectionService electionService,
        IAuditService auditService,
        ILogger<VotingTestController> logger)
    {
        _votingService = votingService;
        _electionService = electionService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("election/{electionId}/multiple-positions")]
    public async Task<IActionResult> TestMultiplePositions(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var hasMultipleResult = await _votingService.HasMultiplePositionsAsync(electionId);
            if (!hasMultipleResult.Success)
            {
                return BadRequest(hasMultipleResult);
            }

            var election = await _electionService.GetElectionByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Eleição não encontrada"));
            }

            await _auditService.LogAsync(adminId.Value, "admin", "test_multiple_positions", "election", electionId,
                $"Test executed for multiple positions detection");

            return Ok(ApiResponse<object>.SuccessResult(new
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                HasMultiplePositions = hasMultipleResult.Data,
                Message = hasMultipleResult.Data 
                    ? "Esta eleição possui múltiplos cargos - votação múltipla será obrigatória"
                    : "Esta eleição possui apenas um cargo - votação simples permitida",
                RequiredVotingMethod = hasMultipleResult.Data ? "cast-multiple-votes" : "cast-vote"
            }, "Teste de múltiplos cargos executado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing multiple positions for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpPost("election/{electionId}/validate-votes")]
    public async Task<IActionResult> TestValidateAllPositions(int electionId, [FromBody] List<VoteForPositionDto> votes)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var validationResult = await _votingService.ValidateAllPositionsVotedAsync(electionId, votes);
            
            var election = await _electionService.GetElectionByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Eleição não encontrada"));
            }

            await _auditService.LogAsync(adminId.Value, "admin", "test_validate_votes", "election", electionId,
                $"Test executed for vote validation - {votes.Count} positions provided");

            return Ok(ApiResponse<object>.SuccessResult(new
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                PositionsProvided = votes.Count,
                ValidationResult = validationResult.Success ? "VÁLIDO" : "INVÁLIDO",
                ValidationMessage = validationResult.Message,
                ProvidedVotes = votes.Select(v => new { v.PositionId, v.CandidateId, v.IsBlankVote, v.IsNullVote }).ToList()
            }, "Teste de validação de votos executado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing vote validation for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("election/{electionId}/counting-report")]
    public async Task<IActionResult> TestCountingReport(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var reportResult = await _votingService.GenerateElectionCountingReportAsync(electionId, adminId.Value, ipAddress);
            
            if (!reportResult.Success)
            {
                return BadRequest(reportResult);
            }

            await _auditService.LogAsync(adminId.Value, "admin", "test_counting_report", "election", electionId,
                $"Test counting report generated");

            return Ok(ApiResponse<ElectionCountingReportDto>.SuccessResult(reportResult.Data, "Relatório de contabilização de teste gerado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing counting report for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpGet("election/{electionId}/system-integrity")]
    public async Task<IActionResult> TestSystemIntegrity(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            // Test multiple positions
            var hasMultipleResult = await _votingService.HasMultiplePositionsAsync(electionId);
            
            // Test election validation
            var validationResult = await _votingService.ValidateElectionForVotingAsync(electionId);
            
            // Test integrity
            var integrityResult = await _votingService.ValidateElectionIntegrityAsync(electionId);

            var election = await _electionService.GetElectionByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Eleição não encontrada"));
            }

            await _auditService.LogAsync(adminId.Value, "admin", "test_system_integrity", "election", electionId,
                $"Complete system integrity test executed");

            return Ok(ApiResponse<object>.SuccessResult(new
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                SystemIntegrityTests = new
                {
                    MultiplePositionsDetection = new
                    {
                        Success = hasMultipleResult.Success,
                        HasMultiplePositions = hasMultipleResult.Data,
                        Message = hasMultipleResult.Message
                    },
                    ElectionValidation = new
                    {
                        Success = validationResult.Success,
                        IsValid = validationResult.Data?.IsValid ?? false,
                        ValidationMessage = validationResult.Data?.ValidationMessage,
                        ValidationErrors = validationResult.Data?.ValidationErrors ?? new List<string>()
                    },
                    IntegrityValidation = new
                    {
                        Success = integrityResult.Success,
                        IntegrityValid = integrityResult.Data?.IntegrityValid ?? false,
                        Message = integrityResult.Data?.Message,
                        OriginalSealHash = integrityResult.Data?.OriginalSealHash,
                        CurrentSystemHash = integrityResult.Data?.CurrentSystemHash
                    }
                },
                OverallStatus = (hasMultipleResult.Success && validationResult.Success && integrityResult.Success) ? "PASSED" : "FAILED",
                TestedAt = DateTime.UtcNow
            }, "Teste de integridade do sistema executado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing system integrity for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}