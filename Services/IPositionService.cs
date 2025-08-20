using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface IPositionService
{
    Task<PagedResult<PositionResponseDto>> GetPositionsAsync(int page, int limit, int? electionId = null, bool? isActive = null);
    Task<PositionResponseDto?> GetPositionByIdAsync(int id);
    Task<PositionWithCandidatesDto?> GetPositionWithCandidatesAsync(int id);
    Task<PositionResponseDto> CreatePositionAsync(CreatePositionDto createDto, int createdBy);
    Task<PositionResponseDto?> UpdatePositionAsync(int id, UpdatePositionDto updateDto, int updatedBy);
    Task<bool> DeletePositionAsync(int id);
    Task<IEnumerable<PositionResponseDto>> GetPositionsByElectionAsync(int electionId);
    Task<bool> ReorderPositionsAsync(int electionId, Dictionary<int, int> positionOrders);
}