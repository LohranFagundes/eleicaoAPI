namespace ElectionApi.Net.Services;

public interface IVoteCryptographyService
{
    Task<string> EncryptVoteDataAsync(VoteEncryptionData voteData, string electionSealHash);
    Task<VoteEncryptionData> DecryptVoteDataAsync(string encryptedData, string electionSealHash);
    string GenerateVoteHash(string voteId, int voterId, int candidateId, DateTime votedAt);
    string GenerateVoteSignature(string voteHash, string encryptedData);
    string GenerateCreationHash(string voteId, int voterId, int electionId, DateTime createdAt);
    string GenerateDeviceFingerprint(string userAgent, string ipAddress, Dictionary<string, string>? additionalData = null);
    bool ValidateVoteIntegrity(string encryptedData, string voteHash, string signature);
    Task<string> EncryptJustificationAsync(string? justification);
    Task<string?> DecryptJustificationAsync(string? encryptedJustification);
}

public class VoteEncryptionData
{
    public int? CandidateId { get; set; }
    public string? CandidateName { get; set; }
    public string? CandidateNumber { get; set; }
    public bool IsBlankVote { get; set; }
    public bool IsNullVote { get; set; }
    public DateTime EncryptedAt { get; set; }
    public string VoteId { get; set; } = string.Empty;
}