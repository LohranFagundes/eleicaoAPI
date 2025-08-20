using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class SystemSeal : BaseEntity
{
    [Required]
    [StringLength(128)]
    public string SealHash { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string SealType { get; set; } = string.Empty; // "election_seal", "report_seal"

    [Required]
    public int ElectionId { get; set; }

    [Required]
    public DateTime SealedAt { get; set; }

    [Required]
    public int SealedBy { get; set; }

    public string SystemData { get; set; } = string.Empty;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public bool IsValid { get; set; } = true;

    // Navigation properties
    public Election Election { get; set; } = null!;
    public Admin Admin { get; set; } = null!;
}