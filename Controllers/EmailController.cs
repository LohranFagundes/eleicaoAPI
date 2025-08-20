using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailService emailService, IAuditService auditService, ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailDto emailDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid email data", ModelState));
        }

        try
        {
            var result = await _emailService.SendEmailAsync(emailDto);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "send_email", "email", null,
                    $"Sent individual email to {emailDto.ToEmail} with subject: {emailDto.Subject}");
            }

            if (result.Success)
            {
                return Ok(ApiResponse<EmailResponseDto>.SuccessResult(result, "Email sent successfully"));
            }
            else
            {
                return BadRequest(ApiResponse<EmailResponseDto>.ErrorResult(result.Message, result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", emailDto.ToEmail);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to send email: {ex.Message}"));
        }
    }

    [HttpPost("send-bulk")]
    public async Task<IActionResult> SendBulkEmail([FromBody] BulkEmailDto bulkEmailDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid bulk email data", ModelState));
        }

        try
        {
            // Validate email configuration before sending bulk emails
            var configValid = await _emailService.ValidateEmailConfigurationAsync();
            if (!configValid)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Email configuration is invalid. Please check SMTP settings."));
            }

            var result = await _emailService.SendBulkEmailAsync(bulkEmailDto);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "send_bulk_email", "bulk_email", null,
                    $"Sent bulk email with subject: {bulkEmailDto.Subject}. Targets: {result.TotalTargets}, Success: {result.SuccessfulSends}, Failed: {result.FailedSends}");
            }

            if (result.Success)
            {
                return Ok(ApiResponse<BulkEmailResponseDto>.SuccessResult(result, "Bulk email sent successfully"));
            }
            else
            {
                return Ok(ApiResponse<BulkEmailResponseDto>.SuccessResult(result, "Bulk email completed with some failures"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk email");
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to send bulk email: {ex.Message}"));
        }
    }

    [HttpPost("send-template")]
    public async Task<IActionResult> SendTemplateEmail([FromBody] SendTemplateEmailDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid template email data", ModelState));
        }

        try
        {
            var result = await _emailService.SendTemplateEmailAsync(dto.ToEmail, dto.ToName, dto.Template);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "send_template_email", "email", null,
                    $"Sent template email '{dto.Template.TemplateName}' to {dto.ToEmail}");
            }

            if (result.Success)
            {
                return Ok(ApiResponse<EmailResponseDto>.SuccessResult(result, "Template email sent successfully"));
            }
            else
            {
                return BadRequest(ApiResponse<EmailResponseDto>.ErrorResult(result.Message, result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send template email to {Email}", dto.ToEmail);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to send template email: {ex.Message}"));
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetEmailHistory([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int limit = 50)
    {
        try
        {
            if (limit > 200)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Limit cannot exceed 200"));
            }

            var history = await _emailService.GetEmailHistoryAsync(startDate, endDate, limit);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view_email_history", "email_history", null,
                    $"Viewed email history from {startDate} to {endDate}");
            }

            return Ok(ApiResponse<IEnumerable<EmailStatusDto>>.SuccessResult(history));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get email history");
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to get email history: {ex.Message}"));
        }
    }

    [HttpGet("status/{emailId}")]
    public async Task<IActionResult> GetEmailStatus(string emailId)
    {
        try
        {
            var status = await _emailService.GetEmailStatusAsync(emailId);

            if (status == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Email not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view_email_status", "email", null,
                    $"Viewed status for email ID: {emailId}");
            }

            return Ok(ApiResponse<EmailStatusDto>.SuccessResult(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get email status for {EmailId}", emailId);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to get email status: {ex.Message}"));
        }
    }

    [HttpPost("validate-config")]
    public async Task<IActionResult> ValidateEmailConfiguration()
    {
        try
        {
            var isValid = await _emailService.ValidateEmailConfigurationAsync();

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "validate_email_config", "email_config", null,
                    $"Email configuration validation result: {(isValid ? "Valid" : "Invalid")}");
            }

            var message = isValid ? "Email configuration is valid" : "Email configuration is invalid";
            return Ok(ApiResponse<object>.SuccessResult(new { IsValid = isValid }, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate email configuration");
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to validate email configuration: {ex.Message}"));
        }
    }

    [HttpPost("test")]
    public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid test email data", ModelState));
        }

        try
        {
            var testEmailDto = new SendEmailDto
            {
                ToEmail = dto.ToEmail,
                ToName = dto.ToName ?? "Test User",
                Subject = "Test Email from Election System",
                Body = $"<h2>Test Email</h2><p>This is a test email sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</p><p>If you received this email, the email configuration is working correctly.</p>",
                IsHtml = true
            };

            var result = await _emailService.SendEmailAsync(testEmailDto);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "send_test_email", "email", null,
                    $"Sent test email to {dto.ToEmail}");
            }

            if (result.Success)
            {
                return Ok(ApiResponse<EmailResponseDto>.SuccessResult(result, "Test email sent successfully"));
            }
            else
            {
                return BadRequest(ApiResponse<EmailResponseDto>.ErrorResult(result.Message, result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}", dto.ToEmail);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to send test email: {ex.Message}"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class SendTemplateEmailDto
{
    [Required]
    [EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    public string? ToName { get; set; }

    [Required]
    public EmailTemplateDto Template { get; set; } = new();
}

public class SendTestEmailDto
{
    [Required]
    [EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    public string? ToName { get; set; }
}