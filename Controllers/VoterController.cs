using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VoterController : ControllerBase
{
    private readonly IVoterService _voterService;
    private readonly IAuditService _auditService;

    public VoterController(IVoterService voterService, IAuditService auditService)
    {
        _voterService = voterService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVoters([FromQuery] int page = 1, [FromQuery] int limit = 10,
        [FromQuery] bool? isActive = null, [FromQuery] bool? isVerified = null)
    {
        try
        {
            var result = await _voterService.GetVotersAsync(page, limit, isActive, isVerified);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "list", "voters");
            }

            return Ok(ApiResponse<PagedResult<VoterResponseDto>>.SuccessResult(result));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch voters"));
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVoter(int id)
    {
        try
        {
            var voter = await _voterService.GetVoterByIdAsync(id);
            
            if (voter == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "voters", id);
            }

            return Ok(ApiResponse<VoterResponseDto>.SuccessResult(voter));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch voter"));
        }
    }

    [HttpGet("profile")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var voter = await _voterService.GetVoterByIdAsync(userId.Value);
            
            if (voter == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter profile not found"));
            }

            return Ok(ApiResponse<VoterResponseDto>.SuccessResult(voter));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch voter profile"));
        }
    }

    [HttpGet("statistics")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetVoterStatistics()
    {
        try
        {
            var statistics = await _voterService.GetVoterStatisticsAsync();
            return Ok(ApiResponse<VoterStatisticsDto>.SuccessResult(statistics));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch voter statistics"));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateVoter([FromBody] CreateVoterDto createDto)
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

            var voter = await _voterService.CreateVoterAsync(createDto, userId.Value);
            
            return CreatedAtAction(
                nameof(GetVoter), 
                new { id = voter.Id }, 
                ApiResponse<VoterResponseDto>.SuccessResult(voter, "Voter created successfully"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to create voter"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateVoter(int id, [FromBody] UpdateVoterDto updateDto)
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

            var voter = await _voterService.UpdateVoterAsync(id, updateDto, userId.Value);
            
            if (voter == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter not found"));
            }

            return Ok(ApiResponse<VoterResponseDto>.SuccessResult(voter, "Voter updated successfully"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update voter"));
        }
    }

    [HttpPut("profile")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateVoterDto updateDto)
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

            // Voters can only update certain fields
            var restrictedUpdateDto = new UpdateVoterDto
            {
                Name = updateDto.Name,
                Phone = updateDto.Phone,
                // Don't allow voters to change email, CPF, vote weight, active status, etc.
            };

            var voter = await _voterService.UpdateVoterAsync(userId.Value, restrictedUpdateDto, userId.Value);
            
            if (voter == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter profile not found"));
            }

            return Ok(ApiResponse<VoterResponseDto>.SuccessResult(voter, "Profile updated successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update profile"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteVoter(int id)
    {
        try
        {
            var result = await _voterService.DeleteVoterAsync(id);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "delete", "voters", id);
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Voter deleted successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to delete voter"));
        }
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VoterVerificationDto verificationDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid verification token"));
        }

        try
        {
            var result = await _voterService.VerifyVoterEmailAsync(verificationDto.VerificationToken);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid or expired verification token"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Email verified successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to verify email"));
        }
    }

    [HttpPost("{id}/send-verification")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SendVerificationEmail(int id)
    {
        try
        {
            var result = await _voterService.SendVerificationEmailAsync(id);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Voter not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Verification email sent successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to send verification email"));
        }
    }

    [HttpPost("change-password")]
    [Authorize(Roles = "voter")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
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

            var result = await _voterService.ChangePasswordAsync(userId.Value, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Current password is incorrect"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Password changed successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to change password"));
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var result = await _voterService.RequestPasswordResetAsync(forgotPasswordDto.Email);
            
            // Always return success to prevent email enumeration attacks
            return Ok(ApiResponse<PasswordResetRequestResponseDto>.SuccessResult(
                new PasswordResetRequestResponseDto
                {
                    Success = true,
                    Message = "Se o email existir em nossa base de dados, você receberá um link para redefinir sua senha.",
                    TokenExpiry = DateTime.UtcNow.AddMinutes(30)
                }));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to process password reset request"));
        }
    }

    [HttpPost("reset-password-with-token")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordWithToken([FromBody] ResetPasswordWithTokenDto resetDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var result = await _voterService.ResetPasswordWithTokenAsync(resetDto.Token, resetDto.NewPassword);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Token inválido ou expirado. Solicite um novo link de redefinição."));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Senha redefinida com sucesso"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to reset password"));
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [Obsolete("Use forgot-password and reset-password-with-token instead")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var result = await _voterService.ResetPasswordAsync(resetPasswordDto.Email, resetPasswordDto.NewPassword);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Email not found"));
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Password reset successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to reset password"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}