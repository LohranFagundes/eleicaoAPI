using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface IVotingService
{
    Task<ApiResponse<object>> LoginVoterAsync(VotingLoginDto loginDto, string ipAddress, string userAgent);
    Task<ApiResponse<ElectionStatusDto>> GetElectionStatusAsync(int electionId, int voterId);
    Task<ApiResponse<VoteReceiptDto>> CastVoteAsync(VotingCastVoteDto voteDto, int voterId, string ipAddress, string userAgent);
    Task<ApiResponse<VoteReceiptDto>> GetVoteReceiptAsync(string receiptToken);
    Task<ApiResponse<bool>> HasVoterVotedAsync(int voterId, int electionId);
    // DEPRECATED: Use IElectionSealService instead
    // Task<ApiResponse<SystemSealResponseDto>> SealElectionAsync(ElectionSealDto sealDto, int adminId, string ipAddress, string userAgent);
    Task<ApiResponse<ZeroReportDto>> GenerateZeroReportAsync(int electionId, int adminId, string ipAddress);
    Task<ApiResponse<IntegrityReportDto>> ValidateElectionIntegrityAsync(int electionId);
    Task<ApiResponse<bool>> CanVoteInElectionAsync(int voterId, int electionId);
    Task<ApiResponse<ElectionValidationDto>> ValidateElectionForVotingAsync(int electionId);
}