using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface IVoteCountingService
{
    Task<VoteCountResultDto> CountVotesAsync(int electionId);
    Task<PositionVoteCountDto> CountVotesByPositionAsync(int electionId, int positionId);
    Task<bool> ValidateAllVotesIntegrityAsync(int electionId);
    Task<ElectionResultsDto> GenerateElectionResultsAsync(int electionId);
    Task<List<VoteAuditDto>> GetVoteAuditTrailAsync(int electionId, bool includeDecryptedData = false);
}

public class VoteCountResultDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public int TotalValidVotes { get; set; }
    public int TotalBlankVotes { get; set; }
    public int TotalNullVotes { get; set; }
    public DateTime CountedAt { get; set; }
    public List<PositionVoteCountDto> PositionResults { get; set; } = new();
}

public class PositionVoteCountDto
{
    public int PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public int TotalBlankVotes { get; set; }
    public int TotalNullVotes { get; set; }
    public List<CandidateVoteCountDto> CandidateResults { get; set; } = new();
}

public class CandidateVoteCountDto
{
    public int? CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public string? CandidateNumber { get; set; }
    public int VoteCount { get; set; }
    public decimal VotePercentage { get; set; }
    public decimal WeightedVotes { get; set; }
}

public class ElectionResultsDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CountedAt { get; set; }
    public string ResultHash { get; set; } = string.Empty;
    public VoteCountResultDto VoteCounts { get; set; } = new();
    public List<PositionResultDto> Results { get; set; } = new();
}

public class PositionResultDto
{
    public int PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public List<CandidateResultDto> Winners { get; set; } = new();
    public List<CandidateVoteCountDto> AllResults { get; set; } = new();
}

public class CandidateResultDto
{
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateNumber { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public decimal VotePercentage { get; set; }
    public bool IsWinner { get; set; }
}

public class VoteAuditDto
{
    public string VoteId { get; set; } = string.Empty;
    public int VoterId { get; set; }
    public string VoterName { get; set; } = string.Empty;
    public int ElectionId { get; set; }
    public int PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public DateTime VotedAt { get; set; }
    public string VoteType { get; set; } = string.Empty;
    public string VoteHash { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public bool IntegrityValid { get; set; }
    
    // Dados descriptografados (apenas para auditoria autorizada)
    public int? DecryptedCandidateId { get; set; }
    public string? DecryptedCandidateName { get; set; }
    public string? DecryptedJustification { get; set; }
}