using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ElectionController : ControllerBase
{
    private readonly IElectionService _electionService;
    private readonly IElectionSealService _electionSealService;
    private readonly IAuditService _auditService;

    public ElectionController(IElectionService electionService, IElectionSealService electionSealService, IAuditService auditService)
    {
        _electionService = electionService;
        _electionSealService = electionSealService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetElections([FromQuery] int page = 1, [FromQuery] int limit = 10, 
        [FromQuery] string? status = null, [FromQuery] string? type = null)
    {
        try
        {
            var result = await _electionService.GetElectionsAsync(page, limit, status, type);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "list", "elections");
            }

            return Ok(ApiResponse<PagedResult<ElectionResponseDto>>.SuccessResult(result));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch elections"));
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetElection(int id)
    {
        try
        {
            var election = await _electionService.GetElectionByIdAsync(id);
            
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Election not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "elections", id);
            }

            return Ok(ApiResponse<ElectionResponseDto>.SuccessResult(election));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch election"));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateElection([FromBody] CreateElectionDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var election = await _electionService.CreateElectionAsync(createDto, userId.Value);
            
            return CreatedAtAction(
                nameof(GetElection), 
                new { id = election.Id }, 
                ApiResponse<ElectionResponseDto>.SuccessResult(election, "Election created successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to create election: {ex.Message}"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateElection(int id, [FromBody] UpdateElectionDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var election = await _electionService.UpdateElectionAsync(id, updateDto, userId.Value);
            
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Election not found"));
            }

            return Ok(ApiResponse<ElectionResponseDto>.SuccessResult(election, "Election updated successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update election"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteElection(int id)
    {
        try
        {
            var result = await _electionService.DeleteElectionAsync(id);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Election not found"));
                
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "delete", "elections", id);
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Election deleted successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to delete election"));
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateElectionStatus(int id, [FromBody] UpdateElectionStatusDto statusDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var validStatuses = new[] { "draft", "active", "completed", "cancelled" };
            if (!validStatuses.Contains(statusDto.Status))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid status. Valid values: draft, active, completed, cancelled"));
            }

            var result = await _electionService.UpdateElectionStatusAsync(id, statusDto.Status);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Election not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "update_status", "elections", id, 
                    $"Status changed to {statusDto.Status}");
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Election status updated successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update election status"));
        }
    }


    [HttpGet("active")]
    public async Task<IActionResult> GetActiveElections()
    {
        try
        {
            var elections = await _electionService.GetActiveElectionsAsync();
            return Ok(ApiResponse<IEnumerable<object>>.SuccessResult(elections));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch active elections"));
        }
    }

    // Election Seal Endpoints

    [HttpPost("{id}/seal")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SealElection(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User ID not found"));
            }

            var result = await _electionSealService.SealElectionAsync(id, userId.Value);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to seal election"));
        }
    }

    [HttpGet("{id}/seal/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetElectionSealStatus(int id)
    {
        try
        {
            var result = await _electionSealService.GetElectionSealStatusAsync(id);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to get election seal status"));
        }
    }

    [HttpPost("{id}/seal/validate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ValidateElectionSeal(int id)
    {
        try
        {
            var result = await _electionSealService.ValidateElectionSealAsync(id);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to validate election seal"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}