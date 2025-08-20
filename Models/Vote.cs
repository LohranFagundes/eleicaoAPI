using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class Vote : BaseEntity
{
    [StringLength(20)]
    public string VoteType { get; set; } = "candidate"; // candidate, blank, null

    [StringLength(50)]
    public string VoteHash { get; set; } = string.Empty;

    public decimal VoteWeight { get; set; } = 1.0m;

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    [StringLength(45)]
    public string? VoterIp { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public bool IsBlankVote { get; set; } = false;

    public bool IsNullVote { get; set; } = false;

    public string? Justification { get; set; }

    // Foreign Keys
    [Required]
    public int VoterId { get; set; }

    [Required]
    public int ElectionId { get; set; }

    [Required]
    public int PositionId { get; set; }

    public int? CandidateId { get; set; }

    // Navigation properties
    public Voter Voter { get; set; } = null!;
    public Election Election { get; set; } = null!;
    public Position Position { get; set; } = null!;
    public Candidate? Candidate { get; set; }
}