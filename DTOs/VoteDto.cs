using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CastVoteDto
{
    [Required]
    public int ElectionId { get; set; }

    [Required]
    public int PositionId { get; set; }

    public int? CandidateId { get; set; }

    [Required]
    [StringLength(20)]
    public string VoteType { get; set; } = "candidate"; // candidate, blank, null

    public string? Justification { get; set; }
}

public class VoteResponseDto
{
    public int Id { get; set; }
    public string VoteType { get; set; } = string.Empty;
    public string VoteHash { get; set; } = string.Empty;
    public decimal VoteWeight { get; set; }
    public DateTime VotedAt { get; set; }
    public string? VoterIp { get; set; }
    public string? Justification { get; set; }
    public int VoterId { get; set; }
    public string VoterName { get; set; } = string.Empty;
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int? CandidateId { get; set; }
    public string? CandidateName { get; set; }
}

public class VoteResultDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public int ValidVotes { get; set; }
    public int BlankVotes { get; set; }
    public int NullVotes { get; set; }
    public IEnumerable<CandidateVoteResultDto> CandidateResults { get; set; } = new List<CandidateVoteResultDto>();
}

public class CandidateVoteResultDto
{
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? CandidateNumber { get; set; }
    public int TotalVotes { get; set; }
    public decimal VotePercentage { get; set; }
    public decimal WeightedVotes { get; set; }
}

public class VoteValidationDto
{
    public bool CanVote { get; set; }
    public string? Reason { get; set; }
    public int RemainingVotes { get; set; }
    public DateTime? ElectionStartTime { get; set; }
    public DateTime? ElectionEndTime { get; set; }
}

public class BulkVoteResultDto
{
    public int SuccessfulVotes { get; set; }
    public int FailedVotes { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();
}