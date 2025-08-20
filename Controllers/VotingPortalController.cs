using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/voting-portal")]
public class VotingPortalController : ControllerBase
{
    private readonly IRepository<Models.Election> _electionRepository;
    private readonly IRepository<Models.Candidate> _candidateRepository;
    private readonly IRepository<Models.Position> _positionRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<VotingPortalController> _logger;
    private readonly IDateTimeService _dateTimeService;

    public VotingPortalController(
        IRepository<Models.Election> electionRepository,
        IRepository<Models.Candidate> candidateRepository,
        IRepository<Models.Position> positionRepository,
        IAuditService auditService,
        ILogger<VotingPortalController> logger,
        IDateTimeService dateTimeService)
    {
        _electionRepository = electionRepository;
        _candidateRepository = candidateRepository;
        _positionRepository = positionRepository;
        _auditService = auditService;
        _logger = logger;
        _dateTimeService = dateTimeService;
    }

    /// <summary>
    /// Obtém candidatos simplificados para o portal de votação
    /// </summary>
    [HttpGet("elections/{electionId}/candidates")]
    public async Task<ActionResult<ApiResponse<VotingPortalElectionDto>>> GetVotingCandidates(int electionId)
    {
        try
        {
            _logger.LogInformation("Getting voting candidates for election {ElectionId}", electionId);

            // Primeiro validar se a eleição está apta para votação
            var validationResult = await ValidateElectionForVotingInternal(electionId);
            if (!validationResult.IsValid)
            {
                return BadRequest(ApiResponse<VotingPortalElectionDto>.ErrorResult(validationResult.ValidationMessage ?? "Election not available for voting"));
            }

            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                .ThenInclude(p => p.Candidates.Where(c => c.IsActive))
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                await _auditService.LogAsync(null, "system", "get_voting_candidates_not_found", 
                    "voting_portal", electionId, $"Election {electionId} not found for voting portal");
                return NotFound(ApiResponse<VotingPortalElectionDto>.ErrorResult("Election not found"));
            }

            var votingElection = new VotingPortalElectionDto
            {
                Id = election.Id,
                Title = election.Title,
                Description = election.Description,
                AllowBlankVotes = election.AllowBlankVotes,
                AllowNullVotes = election.AllowNullVotes,
                RequireJustification = election.RequireJustification,
                MaxVotesPerVoter = election.MaxVotesPerVoter,
                VotingMethod = election.VotingMethod,
                Positions = election.Positions
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.OrderPosition)
                    .Select(p => new VotingPortalPositionDto
                    {
                        Id = p.Id,
                        Name = p.Title,
                        Description = p.Description,
                        MaxVotes = p.MaxVotesPerVoter ?? 1,
                        OrderPosition = p.OrderPosition ?? 1,
                        Candidates = p.Candidates
                            .Where(c => c.IsActive)
                            .OrderBy(c => c.OrderPosition)
                            .ThenBy(c => c.Number)
                            .Select(c => new VotingPortalCandidateDto
                            {
                                Id = c.Id,
                                Name = c.Name,
                                Number = c.Number,
                                Party = c.Party,
                                Description = c.Description,
                                PhotoUrl = c.PhotoUrl,
                                PhotoBase64 = c.PhotoData != null ? Convert.ToBase64String(c.PhotoData) : null,
                                PositionId = c.PositionId,
                                PositionName = p.Title,
                                OrderPosition = c.OrderPosition
                            }).ToList()
                    }).ToList()
            };

            await _auditService.LogAsync(null, "system", "get_voting_candidates_success", 
                "voting_portal", electionId, $"Voting candidates retrieved for election {electionId}");

            _logger.LogInformation("Successfully retrieved {CandidateCount} candidates for election {ElectionId}", 
                votingElection.Positions.SelectMany(p => p.Candidates).Count(), electionId);

            return Ok(ApiResponse<VotingPortalElectionDto>.SuccessResult(votingElection, "Voting candidates retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting voting candidates for election {ElectionId}", electionId);
            await _auditService.LogAsync(null, "system", "get_voting_candidates_error", 
                "voting_portal", electionId, $"Error getting voting candidates: {ex.Message}");
            return StatusCode(500, ApiResponse<VotingPortalElectionDto>.ErrorResult("Internal server error while getting voting candidates"));
        }
    }

    /// <summary>
    /// Valida se uma eleição está apta para receber votos
    /// </summary>
    [HttpGet("elections/{electionId}/validate")]
    public async Task<ActionResult<ApiResponse<ElectionValidationDto>>> ValidateElectionForVoting(int electionId)
    {
        try
        {
            _logger.LogInformation("Validating election {ElectionId} for voting", electionId);

            var election = await _electionRepository.GetByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<ElectionValidationDto>.ErrorResult("Election not found"));
            }

            var validation = await ValidateElectionForVotingInternal(electionId);

            await _auditService.LogAsync(null, "system", "validate_election_for_voting", 
                "voting_portal", electionId, $"Election validation result: {validation.IsValid}");

            return Ok(ApiResponse<ElectionValidationDto>.SuccessResult(validation, "Election validation completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating election {ElectionId} for voting", electionId);
            await _auditService.LogAsync(null, "system", "validate_election_error", 
                "voting_portal", electionId, $"Error validating election: {ex.Message}");
            return StatusCode(500, ApiResponse<ElectionValidationDto>.ErrorResult("Internal server error while validating election"));
        }
    }

    /// <summary>
    /// Validação interna de eleição para votação
    /// </summary>
    private async Task<ElectionValidationDto> ValidateElectionForVotingInternal(int electionId)
    {
        var validation = new ElectionValidationDto();
        var errors = new List<string>();

        var election = await _electionRepository.GetByIdAsync(electionId);
        if (election == null)
        {
            validation.IsValid = false;
            validation.ValidationMessage = "Election not found";
            validation.ValidationErrors.Add("Election does not exist");
            return validation;
        }

        var currentTime = _dateTimeService.UtcNow;
        
        // Converter datas para UTC para comparação
        var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(election.StartDate, TimeZoneInfo.FindSystemTimeZoneById(election.Timezone));
        var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(election.EndDate, TimeZoneInfo.FindSystemTimeZoneById(election.Timezone));

        validation.Status = election.IsSealed ? "sealed" : "open";
        validation.IsSealed = election.IsSealed;
        validation.StartDate = election.StartDate;
        validation.EndDate = election.EndDate;
        validation.IsInVotingPeriod = currentTime >= startDateUtc && currentTime <= endDateUtc;
        validation.IsActive = !election.IsSealed;

        // Regra 1: Eleição deve estar lacrada (sealed)
        if (!election.IsSealed)
        {
            errors.Add("Election must be sealed before voting can begin");
        }

        // Regra 2: Eleição deve estar ativa
        if (election.IsSealed)
        {
            errors.Add("Election is sealed and cannot accept votes");
        }

        // Regra 3: Deve estar dentro do período de votação
        if (currentTime < startDateUtc)
        {
            errors.Add($"Voting has not started yet. Starts at: {election.StartDate:yyyy-MM-dd HH:mm:ss} {election.Timezone}");
        }
        else if (currentTime > endDateUtc)
        {
            errors.Add($"Voting has ended. Ended at: {election.EndDate:yyyy-MM-dd HH:mm:ss} {election.Timezone}");
        }

        validation.ValidationErrors = errors;
        validation.IsValid = errors.Count == 0;
        validation.ValidationMessage = validation.IsValid 
            ? "Election is valid for voting" 
            : string.Join("; ", errors);

        return validation;
    }

    /// <summary>
    /// Obtém apenas lista de candidatos de uma posição específica
    /// </summary>
    [HttpGet("positions/{positionId}/candidates")]
    public async Task<ActionResult<ApiResponse<List<VotingPortalCandidateDto>>>> GetCandidatesByPosition(int positionId)
    {
        try
        {
            _logger.LogInformation("Getting candidates for position {PositionId}", positionId);

            var position = await _positionRepository.GetQueryable()
                .Include(p => p.Candidates.Where(c => c.IsActive))
                .Include(p => p.Election)
                .FirstOrDefaultAsync(p => p.Id == positionId);

            if (position == null)
            {
                return NotFound(ApiResponse<List<VotingPortalCandidateDto>>.ErrorResult("Position not found"));
            }

            // Validar se a eleição da posição está apta para votação
            var validationResult = await ValidateElectionForVotingInternal(position.ElectionId ?? 1);
            if (!validationResult.IsValid)
            {
                return BadRequest(ApiResponse<List<VotingPortalCandidateDto>>.ErrorResult(validationResult.ValidationMessage ?? "Election not available for voting"));
            }

            var candidates = position.Candidates
                .Where(c => c.IsActive)
                .OrderBy(c => c.OrderPosition)
                .ThenBy(c => c.Number)
                .Select(c => new VotingPortalCandidateDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Number = c.Number,
                    Party = c.Party,
                    Description = c.Description,
                    PhotoUrl = c.PhotoUrl,
                    PhotoBase64 = c.PhotoData != null ? Convert.ToBase64String(c.PhotoData) : null,
                    PositionId = c.PositionId,
                    PositionName = position.Title,
                    OrderPosition = c.OrderPosition
                }).ToList();

            await _auditService.LogAsync(null, "system", "get_candidates_by_position", 
                "voting_portal", positionId, $"Retrieved {candidates.Count} candidates for position {positionId}");

            return Ok(ApiResponse<List<VotingPortalCandidateDto>>.SuccessResult(candidates, "Candidates retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting candidates for position {PositionId}", positionId);
            await _auditService.LogAsync(null, "system", "get_candidates_by_position_error", 
                "voting_portal", positionId, $"Error getting candidates: {ex.Message}");
            return StatusCode(500, ApiResponse<List<VotingPortalCandidateDto>>.ErrorResult("Internal server error while getting candidates"));
        }
    }
}