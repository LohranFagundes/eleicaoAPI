using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class Voter : BaseEntity
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

    [Required]
    [StringLength(14)]
    public string Cpf { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    public decimal VoteWeight { get; set; } = 1.0m;

    public bool IsActive { get; set; } = true;

    public bool IsVerified { get; set; } = false;

    public DateTime? EmailVerifiedAt { get; set; }

    [StringLength(255)]
    public string? VerificationToken { get; set; }

    [StringLength(255)]
    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    public DateTime? LastLoginAt { get; set; }

    [StringLength(45)]
    public string? LastLoginIp { get; set; }

    // Navigation properties
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}