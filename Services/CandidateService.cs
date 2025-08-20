using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class CandidateService : ICandidateService
{
    private readonly IRepository<Candidate> _candidateRepository;
    private readonly IRepository<Position> _positionRepository;
    private readonly IAuditService _auditService;

    public CandidateService(
        IRepository<Candidate> candidateRepository,
        IRepository<Position> positionRepository,
        IAuditService auditService)
    {
        _candidateRepository = candidateRepository;
        _positionRepository = positionRepository;
        _auditService = auditService;
    }

    public async Task<PagedResult<CandidateResponseDto>> GetCandidatesAsync(int page, int limit, int? positionId = null, bool? isActive = null)
    {
        var query = _candidateRepository.GetQueryable()
            .Include(c => c.Position)
            .Include(c => c.Votes)
            .AsQueryable();

        if (positionId.HasValue)
            query = query.Where(c => c.PositionId == positionId.Value);

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        query = query.OrderBy(c => c.PositionId).ThenBy(c => c.OrderPosition);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var mappedItems = items.Select(MapToResponseDto).ToList();

        return new PagedResult<CandidateResponseDto>
        {
            Items = mappedItems,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / limit),
            CurrentPage = page,
            HasNextPage = page * limit < totalItems,
            HasPreviousPage = page > 1
        };
    }

    public async Task<CandidateResponseDto?> GetCandidateByIdAsync(int id)
    {
        var candidate = await _candidateRepository.GetQueryable()
            .Include(c => c.Position)
            .Include(c => c.Votes)
            .FirstOrDefaultAsync(c => c.Id == id);

        return candidate != null ? MapToResponseDto(candidate) : null;
    }

    public async Task<Candidate?> GetCandidateModelByIdAsync(int id)
    {
        return await _candidateRepository.GetQueryable()
            .Include(c => c.Position)
            .Include(c => c.Votes)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CandidateResponseDto> CreateCandidateAsync(CreateCandidateDto createDto, int createdBy)
    {
        var position = await _positionRepository.GetByIdAsync(createDto.PositionId);
        if (position == null)
            throw new ArgumentException("Position not found");

        var candidateCount = await _candidateRepository.GetQueryable()
            .CountAsync(c => c.PositionId == createDto.PositionId && c.IsActive);

        if (candidateCount >= position.MaxCandidates)
            throw new InvalidOperationException($"Maximum number of candidates ({position.MaxCandidates}) reached for this position");

        var candidate = new Candidate
        {
            Name = createDto.Name,
            Number = createDto.Number,
            Description = createDto.Description,
            Biography = createDto.Biography,
            PhotoUrl = createDto.PhotoUrl,
            OrderPosition = createDto.OrderPosition,
            PositionId = createDto.PositionId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _candidateRepository.AddAsync(candidate);
        await _auditService.LogAsync(createdBy, "admin", "create", "candidates", candidate.Id);

        var createdCandidate = await GetCandidateByIdAsync(candidate.Id);
        return createdCandidate!;
    }

    public async Task<CandidateResponseDto?> UpdateCandidateAsync(int id, UpdateCandidateDto updateDto, int updatedBy)
    {
        var candidate = await _candidateRepository.GetByIdAsync(id);
        if (candidate == null) return null;

        if (!string.IsNullOrEmpty(updateDto.Name))
            candidate.Name = updateDto.Name;

        if (updateDto.Number != null)
            candidate.Number = updateDto.Number;

        if (updateDto.Description != null)
            candidate.Description = updateDto.Description;

        if (updateDto.Biography != null)
            candidate.Biography = updateDto.Biography;

        if (updateDto.PhotoUrl != null)
            candidate.PhotoUrl = updateDto.PhotoUrl;

        // BLOB Photo Support - Sistema Híbrido
        if (updateDto.PhotoData != null)
            candidate.PhotoData = updateDto.PhotoData;

        if (updateDto.PhotoMimeType != null)
            candidate.PhotoMimeType = updateDto.PhotoMimeType;

        if (updateDto.PhotoFileName != null)
            candidate.PhotoFileName = updateDto.PhotoFileName;

        if (updateDto.OrderPosition.HasValue)
            candidate.OrderPosition = updateDto.OrderPosition.Value;

        if (updateDto.IsActive.HasValue)
            candidate.IsActive = updateDto.IsActive.Value;

        candidate.UpdatedAt = DateTime.UtcNow;

        await _candidateRepository.UpdateAsync(candidate);
        await _auditService.LogAsync(updatedBy, "admin", "update", "candidates", candidate.Id);

        return await GetCandidateByIdAsync(id);
    }

    public async Task<bool> DeleteCandidateAsync(int id)
    {
        var candidate = await _candidateRepository.GetByIdAsync(id);
        if (candidate == null) return false;

        await _candidateRepository.DeleteAsync(candidate);
        return true;
    }

    public async Task<IEnumerable<CandidateResponseDto>> GetCandidatesByPositionAsync(int positionId)
    {
        var candidates = await _candidateRepository.GetQueryable()
            .Include(c => c.Position)
            .Include(c => c.Votes)
            .Where(c => c.PositionId == positionId && c.IsActive)
            .OrderBy(c => c.OrderPosition)
            .ToListAsync();

        return candidates.Select(MapToResponseDto);
    }

    public async Task<IEnumerable<CandidateWithVotesDto>> GetCandidatesWithVotesAsync(int positionId)
    {
        var candidates = await _candidateRepository.GetQueryable()
            .Include(c => c.Position)
            .Include(c => c.Votes)
            .Where(c => c.PositionId == positionId && c.IsActive)
            .ToListAsync();

        var totalVotes = candidates.Sum(c => c.Votes.Count);

        return candidates.Select(c => new CandidateWithVotesDto
        {
            Id = c.Id,
            Name = c.Name,
            Number = c.Number,
            Description = c.Description,
            PhotoUrl = c.PhotoUrl,
            PositionId = c.PositionId,
            PositionTitle = c.Position?.Title ?? "",
            TotalVotes = c.Votes.Count,
            VotePercentage = totalVotes > 0 ? (decimal)c.Votes.Count / totalVotes * 100 : 0
        }).OrderByDescending(c => c.TotalVotes);
    }

    public async Task<bool> ReorderCandidatesAsync(int positionId, Dictionary<int, int> candidateOrders)
    {
        var candidates = await _candidateRepository.GetQueryable()
            .Where(c => c.PositionId == positionId)
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            if (candidateOrders.ContainsKey(candidate.Id))
            {
                candidate.OrderPosition = candidateOrders[candidate.Id];
                candidate.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _candidateRepository.UpdateRangeAsync(candidates);
        return true;
    }

    private static CandidateResponseDto MapToResponseDto(Candidate candidate)
    {
        // Determine photo storage type
        var hasPhotoFile = candidate.HasPhotoFile;
        var hasPhotoBlob = candidate.HasPhotoBlob;
        var photoStorageType = (hasPhotoFile, hasPhotoBlob) switch
        {
            (true, true) => "both",
            (true, false) => "file",
            (false, true) => "blob",
            (false, false) => "none"
        };

        return new CandidateResponseDto
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Number = candidate.Number,
            Description = candidate.Description,
            Biography = candidate.Biography,
            PhotoUrl = candidate.PhotoUrl,
            
            // Hybrid Photo Storage Information
            HasPhoto = candidate.HasPhoto,
            HasPhotoFile = hasPhotoFile,
            HasPhotoBlob = hasPhotoBlob,
            PhotoStorageType = photoStorageType,
            PhotoMimeType = candidate.PhotoMimeType,
            PhotoFileName = candidate.PhotoFileName,
            
            OrderPosition = candidate.OrderPosition,
            IsActive = candidate.IsActive,
            PositionId = candidate.PositionId,
            PositionTitle = candidate.Position?.Title ?? "",
            VotesCount = candidate.Votes?.Count ?? 0,
            CreatedAt = candidate.CreatedAt,
            UpdatedAt = candidate.UpdatedAt
        };
    }
}