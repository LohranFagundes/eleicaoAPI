using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class SystemSealController : ControllerBase
{
    private readonly ISystemSealService _systemSealService;
    private readonly IAuditService _auditService;

    public SystemSealController(ISystemSealService systemSealService, IAuditService auditService)
    {
        _systemSealService = systemSealService;
        _auditService = auditService;
    }

    [HttpPost("generate/{electionId}")]
    public async Task<IActionResult> GenerateSystemSeal(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin user not found."));
            }

            var seal = await _systemSealService.GenerateSystemSealAsync(electionId, adminId.Value);
            return Ok(ApiResponse<SystemSeal>.SuccessResult(seal, "System seal generated successfully."));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to generate system seal: {ex.Message}"));
        }
    }

    [HttpGet("latest/{electionId}")]
    public async Task<IActionResult> GetLatestSystemSeal(int electionId)
    {
        try
        {
            var seal = await _systemSealService.GetLatestSystemSealAsync(electionId);
            if (seal == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("No system seal found for this election."));
            }
            return Ok(ApiResponse<SystemSeal>.SuccessResult(seal));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to retrieve system seal: {ex.Message}"));
        }
    }

    [HttpPost("verify/{electionId}")]
    public async Task<IActionResult> VerifySystemSeal(int electionId, [FromBody] SystemSealVerificationRequestDto request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ProvidedSealHash))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Provided seal hash is required."));
            }

            var verificationResult = await _systemSealService.VerifySystemSealAsync(electionId, request.ProvidedSealHash);
            return Ok(ApiResponse<SystemSealVerificationDto>.SuccessResult(verificationResult));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to verify system seal: {ex.Message}"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
