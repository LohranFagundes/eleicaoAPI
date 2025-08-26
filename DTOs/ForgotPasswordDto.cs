using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordWithTokenDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class PasswordResetRequestResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime? TokenExpiry { get; set; }
    public bool Success { get; set; }
}