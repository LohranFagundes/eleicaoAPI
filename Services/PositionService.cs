using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class PositionService : IPositionService
{
    private readonly IRepository<Position> _positionRepository;
    private readonly IRepository<Election> _electionRepository;
    private readonly IAuditService _auditService;

    public PositionService(
        IRepository<Position> positionRepository,
        IRepository<Election> electionRepository,
        IAuditService auditService)
    {
        _positionRepository = positionRepository;
        _electionRepository = electionRepository;
        _auditService = auditService;
    }

    public async Task<PagedResult<PositionResponseDto>> GetPositionsAsync(int page, int limit, int? electionId = null, bool? isActive = null)
    {
        try
        {
            var query = _positionRepository.GetQueryable()
                .Include(p => p.Election)
                .Include(p => p.Candidates)
                .Where(p => p.Id > 0) // Filter out invalid records
                .AsQueryable();

            if (electionId.HasValue)
                query = query.Where(p => p.ElectionId == electionId.Value);

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            query = query.OrderBy(p => p.ElectionId).ThenBy(p => p.OrderPosition);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var mappedItems = new List<PositionResponseDto>();
            foreach (var item in items)
            {
                try
                {
                    var mapped = MapToResponseDto(item);
                    mappedItems.Add(mapped);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error mapping position ID {item.Id}: {ex.Message}", ex);
                }
            }

            return new PagedResult<PositionResponseDto>
            {
                Items = mappedItems,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling((double)totalItems / limit),
                CurrentPage = page,
                HasNextPage = page * limit < totalItems,
                HasPreviousPage = page > 1
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error in GetPositionsAsync: {ex.Message}", ex);
        }
    }

    public async Task<PositionResponseDto?> GetPositionByIdAsync(int id)
    {
        var position = await _positionRepository.GetQueryable()
            .Include(p => p.Election)
            .Include(p => p.Candidates)
            .FirstOrDefaultAsync(p => p.Id == id);

        return position != null ? MapToResponseDto(position) : null;
    }

    public async Task<PositionWithCandidatesDto?> GetPositionWithCandidatesAsync(int id)
    {
        var position = await _positionRepository.GetQueryable()
            .Include(p => p.Election)
            .Include(p => p.Candidates.Where(c => c.IsActive))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (position == null) return null;

        return new PositionWithCandidatesDto
        {
            Id = position.Id,
            Title = position.Title,
            Description = position.Description,
            MaxCandidates = position.MaxCandidates ?? 10,
            MaxVotesPerVoter = position.MaxVotesPerVoter ?? 1,
            AllowBlankVotes = position.AllowBlankVotes,
            AllowNullVotes = position.AllowNullVotes,
            OrderPosition = position.OrderPosition ?? 1,
            IsActive = position.IsActive,
            ElectionId = position.ElectionId ?? 1,
            Candidates = position.Candidates.Select(c => new CandidateResponseDto
            {
                Id = c.Id,
                Name = c.Name,
                Number = c.Number,
                Description = c.Description,
                Biography = c.Biography,
                PhotoUrl = c.PhotoUrl,
                OrderPosition = c.OrderPosition,
                IsActive = c.IsActive,
                PositionId = c.PositionId,
                PositionTitle = position.Title,
                VotesCount = c.Votes?.Count ?? 0,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).OrderBy(c => c.OrderPosition)
        };
    }

    public async Task<PositionResponseDto> CreatePositionAsync(CreatePositionDto createDto, int createdBy)
    {
        var election = await _electionRepository.GetByIdAsync(createDto.ElectionId);
        if (election == null)
            throw new ArgumentException("Election not found");

        var position = new Position
        {
            Title = createDto.Title,
            Description = createDto.Description,
            MaxCandidates = createDto.MaxCandidates,
            MaxVotesPerVoter = createDto.MaxVotesPerVoter,
            AllowBlankVotes = createDto.AllowBlankVotes,
            AllowNullVotes = createDto.AllowNullVotes,
            OrderPosition = createDto.OrderPosition,
            ElectionId = createDto.ElectionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _positionRepository.AddAsync(position);
        await _auditService.LogAsync(createdBy, "admin", "create", "positions", position.Id);

        var createdPosition = await GetPositionByIdAsync(position.Id);
        return createdPosition!;
    }

    public async Task<PositionResponseDto?> UpdatePositionAsync(int id, UpdatePositionDto updateDto, int updatedBy)
    {
        var position = await _positionRepository.GetByIdAsync(id);
        if (position == null) return null;

        if (!string.IsNullOrEmpty(updateDto.Title))
            position.Title = updateDto.Title;

        if (updateDto.Description != null)
            position.Description = updateDto.Description;

        if (updateDto.MaxCandidates.HasValue)
            position.MaxCandidates = updateDto.MaxCandidates.Value;

        if (updateDto.MaxVotesPerVoter.HasValue)
            position.MaxVotesPerVoter = updateDto.MaxVotesPerVoter.Value;

        if (updateDto.AllowBlankVotes.HasValue)
            position.AllowBlankVotes = updateDto.AllowBlankVotes.Value;

        if (updateDto.AllowNullVotes.HasValue)
            position.AllowNullVotes = updateDto.AllowNullVotes.Value;

        if (updateDto.OrderPosition.HasValue)
            position.OrderPosition = updateDto.OrderPosition.Value;

        if (updateDto.IsActive.HasValue)
            position.IsActive = updateDto.IsActive.Value;

        position.UpdatedAt = DateTime.UtcNow;

        await _positionRepository.UpdateAsync(position);
        await _auditService.LogAsync(updatedBy, "admin", "update", "positions", position.Id);

        return await GetPositionByIdAsync(id);
    }

    public async Task<bool> DeletePositionAsync(int id)
    {
        var position = await _positionRepository.GetByIdAsync(id);
        if (position == null) return false;

        await _positionRepository.DeleteAsync(position);
        return true;
    }

    public async Task<IEnumerable<PositionResponseDto>> GetPositionsByElectionAsync(int electionId)
    {
        var positions = await _positionRepository.GetQueryable()
            .Include(p => p.Election)
            .Include(p => p.Candidates)
            .Where(p => p.ElectionId == electionId && p.IsActive)
            .OrderBy(p => p.OrderPosition)
            .ToListAsync();

        return positions.Select(MapToResponseDto);
    }

    public async Task<bool> ReorderPositionsAsync(int electionId, Dictionary<int, int> positionOrders)
    {
        var positions = await _positionRepository.GetQueryable()
            .Where(p => p.ElectionId == electionId)
            .ToListAsync();

        foreach (var position in positions)
        {
            if (positionOrders.ContainsKey(position.Id))
            {
                position.OrderPosition = positionOrders[position.Id];
                position.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _positionRepository.UpdateRangeAsync(positions);
        return true;
    }

    private static PositionResponseDto MapToResponseDto(Position position)
    {
        // Handle nullable Int32 fields properly
        return new PositionResponseDto
        {
            Id = position.Id,
            Title = position.Title ?? "",
            Description = position.Description ?? "",
            MaxCandidates = position.MaxCandidates ?? 10,
            MaxVotesPerVoter = position.MaxVotesPerVoter ?? 1,
            AllowBlankVotes = position.AllowBlankVotes,
            AllowNullVotes = position.AllowNullVotes,
            OrderPosition = position.OrderPosition ?? 1,
            IsActive = position.IsActive,
            ElectionId = position.ElectionId ?? 1,
            ElectionTitle = position.Election?.Title ?? "",
            CandidatesCount = position.Candidates?.Count ?? 0,
            CreatedAt = position.CreatedAt == default ? DateTime.UtcNow : position.CreatedAt,
            UpdatedAt = position.UpdatedAt == default ? DateTime.UtcNow : position.UpdatedAt
        };
    }
}