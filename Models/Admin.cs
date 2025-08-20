using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class Admin : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [StringLength(50)]
    public string Role { get; set; } = "admin";

    // Master user flag - only for system developer
    public bool IsMaster { get; set; } = false;

    public string? Permissions { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSuper { get; set; } = false;

    public DateTime? LastLoginAt { get; set; }

    [StringLength(45)]
    public string? LastLoginIp { get; set; }

}