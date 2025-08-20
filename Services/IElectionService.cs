using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public interface IElectionService
{
    Task<PagedResult<ElectionResponseDto>> GetElectionsAsync(int page, int limit, string? status = null, string? type = null);
    Task<ElectionResponseDto?> GetElectionByIdAsync(int id);
    Task<ElectionResponseDto> CreateElectionAsync(CreateElectionDto dto, int createdBy);
    Task<ElectionResponseDto?> UpdateElectionAsync(int id, UpdateElectionDto dto, int updatedBy);
    Task<bool> DeleteElectionAsync(int id);
    Task<bool> UpdateElectionStatusAsync(int id, string status);
    Task<bool> SealElectionAsync(int id);
    Task<bool> IsElectionActiveAsync(int id);
    Task<bool> CanVoteInElectionAsync(int id);
    Task<IEnumerable<Election>> GetActiveElectionsAsync();
    Task<IEnumerable<Election>> GetScheduledElectionsAsync();
    Task<IEnumerable<Election>> GetCompletedElectionsAsync();
}