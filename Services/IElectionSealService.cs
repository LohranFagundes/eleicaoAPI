using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public interface IElectionSealService
{
    Task<ApiResponse<ElectionSealResponseDto>> SealElectionAsync(int electionId, int sealedBy);
    Task<ApiResponse<bool>> ValidateElectionSealAsync(int electionId);
    Task<ApiResponse<ElectionSealStatusResponseDto>> GetElectionSealStatusAsync(int electionId);
    Task<bool> IsElectionSealedAsync(int electionId);
}