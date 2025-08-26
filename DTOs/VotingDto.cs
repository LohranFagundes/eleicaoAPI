using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class VotingLoginDto
{
    [Required]
    [StringLength(14)]
    public string Cpf { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public int ElectionId { get; set; }
}

public class VotingCastVoteDto
{
    [Required]
    public int ElectionId { get; set; }

    [Required]
    public int PositionId { get; set; }

    public int? CandidateId { get; set; } // null for blank vote

    public bool IsBlankVote { get; set; } = false;

    public bool IsNullVote { get; set; } = false;

    public string? Justification { get; set; }
}

public class VotingCastMultipleVotesDto
{
    [Required]
    public int ElectionId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Deve votar em pelo menos um cargo")]
    public List<VoteForPositionDto> Votes { get; set; } = new();

    public string? Justification { get; set; }
}

public class VoteForPositionDto
{
    [Required]
    public int PositionId { get; set; }

    public int? CandidateId { get; set; } // null for blank vote

    public bool IsBlankVote { get; set; } = false;

    public bool IsNullVote { get; set; } = false;
}

public class VoteReceiptDto
{
    public string ReceiptToken { get; set; } = string.Empty;
    public string VoteHash { get; set; } = string.Empty;
    public DateTime VotedAt { get; set; }
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public List<VoteDetailDto> VoteDetails { get; set; } = new();
    public string VoterName { get; set; } = string.Empty;
    public string VoterCpf { get; set; } = string.Empty;
}

public class VoteDetailDto
{
    public string PositionName { get; set; } = string.Empty;
    public string? CandidateName { get; set; }
    public string? CandidateNumber { get; set; }
    public bool IsBlankVote { get; set; }
    public bool IsNullVote { get; set; }
}

public class ElectionSealDto
{
    [Required]
    public int ElectionId { get; set; }

    public string? AdminPassword { get; set; } // Optional additional verification
}

public class SystemSealResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SealHash { get; set; } = string.Empty;
    public DateTime SealedAt { get; set; }
    public string SystemData { get; set; } = string.Empty;
}

public class ZeroReportDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string ReportHash { get; set; } = string.Empty;
    public int TotalRegisteredVoters { get; set; }
    public int TotalCandidates { get; set; }
    public int TotalPositions { get; set; }
    public int TotalVotes { get; set; }
    public List<ZeroReportPositionDto> Positions { get; set; } = new();
}

public class ZeroReportPositionDto
{
    public string PositionName { get; set; } = string.Empty;
    public int TotalCandidates { get; set; }
    public int TotalVotes { get; set; } = 0;
    public List<ZeroReportCandidateDto> Candidates { get; set; } = new();
}

public class ZeroReportCandidateDto
{
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateNumber { get; set; } = string.Empty;
    public int VoteCount { get; set; } = 0;
}

public class ElectionStatusDto
{
    public int ElectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsSealed { get; set; }
    public DateTime? SealedAt { get; set; }
    public bool CanVote { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<PositionSummaryDto> Positions { get; set; } = new();
}

public class PositionSummaryDto
{
    public int PositionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxCandidates { get; set; }
    public List<CandidateSummaryDto> Candidates { get; set; } = new();
}

public class CandidateSummaryDto
{
    public int CandidateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Party { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Biography { get; set; }
}

public class IntegrityReportDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public string OriginalSealHash { get; set; } = string.Empty;
    public string CurrentSystemHash { get; set; } = string.Empty;
    public bool IntegrityValid { get; set; }
    public DateTime ReportGeneratedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ValidationDetails { get; set; } = new();
}

public class ElectionCountingReportDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string ReportHash { get; set; } = string.Empty;
    public string SystemSealHash { get; set; } = string.Empty;
    public int TotalVoters { get; set; }
    public int TotalVotes { get; set; }
    public List<CountingPositionDto> Positions { get; set; } = new();
}

public class CountingPositionDto
{
    public string PositionName { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public int BlankVotes { get; set; }
    public int NullVotes { get; set; }
    public List<CountingCandidateDto> Candidates { get; set; } = new();
}

public class CountingCandidateDto
{
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateNumber { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public decimal Percentage { get; set; }
}

public class ElectionValidationDto
{
    public string Status { get; set; } = string.Empty;
    public bool IsSealed { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsInVotingPeriod { get; set; }
    public bool IsActive { get; set; }
    public bool IsValid { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
}