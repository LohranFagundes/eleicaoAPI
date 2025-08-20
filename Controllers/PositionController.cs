using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PositionController : ControllerBase
{
    private readonly IPositionService _positionService;
    private readonly IAuditService _auditService;

    public PositionController(IPositionService positionService, IAuditService auditService)
    {
        _positionService = positionService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPositions([FromQuery] int page = 1, [FromQuery] int limit = 10,
        [FromQuery] int? electionId = null, [FromQuery] bool? isActive = null)
    {
        try
        {
            var result = await _positionService.GetPositionsAsync(page, limit, electionId, isActive);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "list", "positions");
            }

            return Ok(ApiResponse<PagedResult<PositionResponseDto>>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            // Log the actual error for debugging
            Console.WriteLine($"Error in GetPositions: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch positions: {ex.Message}"));
        }
    }


    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPosition(int id)
    {
        try
        {
            var position = await _positionService.GetPositionByIdAsync(id);
            
            if (position == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Position not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "positions", id);
            }

            return Ok(ApiResponse<PositionResponseDto>.SuccessResult(position));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch position"));
        }
    }

    [HttpGet("{id}/with-candidates")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPositionWithCandidates(int id)
    {
        try
        {
            var position = await _positionService.GetPositionWithCandidatesAsync(id);
            
            if (position == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Position not found"));
            }

            return Ok(ApiResponse<PositionWithCandidatesDto>.SuccessResult(position));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch position with candidates"));
        }
    }

    [HttpGet("election/{electionId}")]
    public async Task<IActionResult> GetPositionsByElection(int electionId)
    {
        try
        {
            var positions = await _positionService.GetPositionsByElectionAsync(electionId);
            return Ok(ApiResponse<IEnumerable<PositionResponseDto>>.SuccessResult(positions));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch positions for election"));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionDto createDto)
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

            var position = await _positionService.CreatePositionAsync(createDto, userId.Value);
            
            return CreatedAtAction(
                nameof(GetPosition), 
                new { id = position.Id }, 
                ApiResponse<PositionResponseDto>.SuccessResult(position, "Position created successfully"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to create position"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpdatePositionDto updateDto)
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

            var position = await _positionService.UpdatePositionAsync(id, updateDto, userId.Value);
            
            if (position == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Position not found"));
            }

            return Ok(ApiResponse<PositionResponseDto>.SuccessResult(position, "Position updated successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update position"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeletePosition(int id)
    {
        try
        {
            var result = await _positionService.DeletePositionAsync(id);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Position not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "delete", "positions", id);
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Position deleted successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to delete position"));
        }
    }

    [HttpPut("election/{electionId}/reorder")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ReorderPositions(int electionId, [FromBody] Dictionary<int, int> positionOrders)
    {
        try
        {
            var result = await _positionService.ReorderPositionsAsync(electionId, positionOrders);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Failed to reorder positions"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "reorder", "positions", electionId, "Positions reordered");
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Positions reordered successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to reorder positions"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}