using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class ZeroReport : BaseEntity
{
    [Required]
    public int ElectionId { get; set; }

    [Required]
    public DateTime GeneratedAt { get; set; }

    [Required]
    public int GeneratedBy { get; set; }

    public string ReportData { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string ReportHash { get; set; } = string.Empty;

    public int TotalRegisteredVoters { get; set; }

    public int TotalCandidates { get; set; }

    public int TotalPositions { get; set; }

    public int TotalVotes { get; set; } = 0; // Should be 0 for zero report

    [StringLength(45)]
    public string? IpAddress { get; set; }

    // Navigation properties
    public Election Election { get; set; } = null!;
    public Admin Admin { get; set; } = null!;
}