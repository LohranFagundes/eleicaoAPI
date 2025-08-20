using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class VoteReceipt : BaseEntity
{
    [Required]
    public int VoterId { get; set; }

    [Required]
    public int ElectionId { get; set; }

    [Required]
    [StringLength(64)]
    public string ReceiptToken { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string VoteHash { get; set; } = string.Empty;

    public DateTime VotedAt { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public string VoteData { get; set; } = string.Empty;

    public bool IsValid { get; set; } = true;

    // Navigation properties
    public Voter Voter { get; set; } = null!;
    public Election Election { get; set; } = null!;
}