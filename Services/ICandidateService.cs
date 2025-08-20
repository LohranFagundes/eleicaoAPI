using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public interface ICandidateService
{
    Task<PagedResult<CandidateResponseDto>> GetCandidatesAsync(int page, int limit, int? positionId = null, bool? isActive = null);
    Task<CandidateResponseDto?> GetCandidateByIdAsync(int id);
    Task<Candidate?> GetCandidateModelByIdAsync(int id);
    Task<CandidateResponseDto> CreateCandidateAsync(CreateCandidateDto createDto, int createdBy);
    Task<CandidateResponseDto?> UpdateCandidateAsync(int id, UpdateCandidateDto updateDto, int updatedBy);
    Task<bool> DeleteCandidateAsync(int id);
    Task<IEnumerable<CandidateResponseDto>> GetCandidatesByPositionAsync(int positionId);
    Task<IEnumerable<CandidateWithVotesDto>> GetCandidatesWithVotesAsync(int positionId);
    Task<bool> ReorderCandidatesAsync(int positionId, Dictionary<int, int> candidateOrders);
}