using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class ElectionService : IElectionService
{
    private readonly IRepository<Election> _electionRepository;
    private readonly IAuditService _auditService;

    public ElectionService(
        IRepository<Election> electionRepository,
        IAuditService auditService)
    {
        _electionRepository = electionRepository;
        _auditService = auditService;
    }

    public async Task<PagedResult<ElectionResponseDto>> GetElectionsAsync(int page, int limit, string? status = null, string? type = null)
    {
        var query = _electionRepository.GetQueryable()
            .Include(e => e.Company)
            .Where(e => (status == null || (status == "sealed" ? e.IsSealed : !e.IsSealed)) && (type == null || e.ElectionType == type))
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync();
        var elections = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var electionDtos = elections.Select(MapToElectionResponse);

        return new PagedResult<ElectionResponseDto>
        {
            Items = electionDtos,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / limit),
            CurrentPage = page,
            HasNextPage = page * limit < totalCount,
            HasPreviousPage = page > 1
        };
    }

    public async Task<ElectionResponseDto?> GetElectionByIdAsync(int id)
    {
        var election = await _electionRepository.GetQueryable()
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.Id == id);
        return election == null ? null : MapToElectionResponse(election);
    }

    public async Task<ElectionResponseDto> CreateElectionAsync(CreateElectionDto dto, int createdBy)
    {
        var election = new Election
        {
            Title = dto.Title,
            Description = dto.Description,
            ElectionType = dto.ElectionType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Timezone = dto.Timezone,
            AllowBlankVotes = dto.AllowBlankVotes,
            AllowNullVotes = dto.AllowNullVotes,
            RequireJustification = dto.RequireJustification,
            MaxVotesPerVoter = dto.MaxVotesPerVoter,
            VotingMethod = dto.VotingMethod,
            ResultsVisibility = dto.ResultsVisibility,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CompanyId = dto.CompanyId,
            IsSealed = false
        };

        var createdElection = await _electionRepository.AddAsync(election);
        
        await _auditService.LogAsync(createdBy, "admin", "create", "elections", createdElection.Id);
        
        return MapToElectionResponse(createdElection);
    }

    public async Task<ElectionResponseDto?> UpdateElectionAsync(int id, UpdateElectionDto dto, int updatedBy)
    {
        var election = await _electionRepository.GetByIdAsync(id);
        if (election == null) return null;

        if (election.IsSealed)
            throw new InvalidOperationException("Cannot modify sealed elections");

        if (dto.Title != null) election.Title = dto.Title;
        if (dto.Description != null) election.Description = dto.Description;
        if (dto.ElectionType != null) election.ElectionType = dto.ElectionType;
        if (dto.StartDate.HasValue) election.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) election.EndDate = dto.EndDate.Value;
        if (dto.Timezone != null) election.Timezone = dto.Timezone;
        if (dto.AllowBlankVotes.HasValue) election.AllowBlankVotes = dto.AllowBlankVotes.Value;
        if (dto.AllowNullVotes.HasValue) election.AllowNullVotes = dto.AllowNullVotes.Value;
        if (dto.RequireJustification.HasValue) election.RequireJustification = dto.RequireJustification.Value;
        if (dto.MaxVotesPerVoter.HasValue) election.MaxVotesPerVoter = dto.MaxVotesPerVoter.Value;
        if (dto.VotingMethod != null) election.VotingMethod = dto.VotingMethod;
        if (dto.ResultsVisibility != null) election.ResultsVisibility = dto.ResultsVisibility;
        if (dto.CompanyId.HasValue) election.CompanyId = dto.CompanyId.Value;
        election.UpdatedBy = updatedBy;

        await _electionRepository.UpdateAsync(election);
        
        await _auditService.LogAsync(updatedBy, "admin", "update", "elections", id);
        
        return MapToElectionResponse(election);
    }

    public async Task<bool> DeleteElectionAsync(int id)
    {
        var election = await _electionRepository.GetByIdAsync(id);
        if (election == null) return false;

        await _electionRepository.DeleteAsync(election);
        return true;
    }

    public async Task<bool> UpdateElectionStatusAsync(int id, string status)
    {
        var election = await _electionRepository.GetByIdAsync(id);
        if (election == null) return false;

        var validStatuses = new[] { "draft", "active", "completed", "cancelled" };
        if (!validStatuses.Contains(status.ToLower()))
            return false;

        var now = DateTime.UtcNow;

        // Update the status field directly
        election.Status = status.ToLower();
        
        // Additional logic based on status
        switch (status.ToLower())
        {
            case "completed":
                // Seal the election when marked as completed
                election.IsSealed = true;
                election.SealedAt = now;
                break;

            case "active":
                // Ensure election is not sealed when activated
                election.IsSealed = false;
                break;

            case "draft":
            case "cancelled":
                // Keep existing dates and seal status
                break;
        }

        election.UpdatedAt = now;
        await _electionRepository.UpdateAsync(election);
        
        return true;
    }

    public async Task<bool> SealElectionAsync(int id)
    {
        var election = await _electionRepository.GetByIdAsync(id);
        if (election == null) return false;

        election.IsSealed = true;
        election.SealedAt = DateTime.UtcNow;
        await _electionRepository.UpdateAsync(election);
        
        return true;
    }

    public async Task<bool> IsElectionActiveAsync(int id)
    {
        var election = await _electionRepository.GetByIdAsync(id);
        return election != null && 
               !election.IsSealed && 
               election.StartDate <= DateTime.UtcNow && 
               election.EndDate >= DateTime.UtcNow;
    }

    public async Task<bool> CanVoteInElectionAsync(int id)
    {
        return await IsElectionActiveAsync(id);
    }

    public async Task<IEnumerable<Election>> GetActiveElectionsAsync()
    {
        var now = DateTime.UtcNow;
        return await _electionRepository.FindAsync(e => 
            !e.IsSealed && 
            e.StartDate <= now && 
            e.EndDate >= now);
    }

    public async Task<IEnumerable<Election>> GetScheduledElectionsAsync()
    {
        var now = DateTime.UtcNow;
        return await _electionRepository.FindAsync(e => 
            !e.IsSealed && 
            e.StartDate > now);
    }

    public async Task<IEnumerable<Election>> GetCompletedElectionsAsync()
    {
        return await _electionRepository.FindAsync(e => e.IsSealed);
    }

    private static ElectionResponseDto MapToElectionResponse(Election election)
    {
        return new ElectionResponseDto
        {
            Id = election.Id,
            Title = election.Title,
            Description = election.Description,
            ElectionType = election.ElectionType,
            Status = election.IsSealed ? "sealed" : "open",
            StartDate = election.StartDate,
            EndDate = election.EndDate,
            Timezone = election.Timezone,
            AllowBlankVotes = election.AllowBlankVotes,
            AllowNullVotes = election.AllowNullVotes,
            RequireJustification = election.RequireJustification,
            MaxVotesPerVoter = election.MaxVotesPerVoter,
            VotingMethod = election.VotingMethod,
            ResultsVisibility = election.ResultsVisibility,
            CreatedBy = election.CreatedBy,
            UpdatedBy = election.UpdatedBy,
            CreatedAt = election.CreatedAt,
            UpdatedAt = election.UpdatedAt,
            CompanyId = election.CompanyId,
            CompanyName = election.Company?.NomeFantasia,
            CompanyCnpj = election.Company?.Cnpj
        };
    }
}