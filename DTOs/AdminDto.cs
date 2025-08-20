using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CreateAdminDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [StringLength(50)]
    public string Role { get; set; } = "admin";

    public string? Permissions { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSuper { get; set; } = false;

}

public class UpdateAdminDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(128)]
    public string? Password { get; set; }

    [StringLength(50)]
    public string? Role { get; set; }

    public string? Permissions { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsSuper { get; set; }

}

public class AdminResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Permissions { get; set; }
    public bool IsActive { get; set; }
    public bool IsSuper { get; set; }
    public bool IsMaster { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminStatisticsDto
{
    public int TotalAdmins { get; set; }
    public int ActiveAdmins { get; set; }
    public int SuperAdmins { get; set; }
    public int RecentLogins { get; set; }
}