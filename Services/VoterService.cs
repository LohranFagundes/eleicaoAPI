using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class VoterService : IVoterService
{
    private readonly IRepository<Voter> _voterRepository;
    private readonly IAuditService _auditService;

    public VoterService(IRepository<Voter> voterRepository, IAuditService auditService)
    {
        _voterRepository = voterRepository;
        _auditService = auditService;
    }

    public async Task<PagedResult<VoterResponseDto>> GetVotersAsync(int page, int limit, bool? isActive = null, bool? isVerified = null)
    {
        var query = _voterRepository.GetQueryable()
            .Include(v => v.Votes)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(v => v.IsActive == isActive.Value);

        if (isVerified.HasValue)
            query = query.Where(v => v.IsVerified == isVerified.Value);

        query = query.OrderBy(v => v.Name);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var mappedItems = items.Select(MapToResponseDto).ToList();

        return new PagedResult<VoterResponseDto>
        {
            Items = mappedItems,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / limit),
            CurrentPage = page,
            HasNextPage = page * limit < totalItems,
            HasPreviousPage = page > 1
        };
    }

    public async Task<VoterResponseDto?> GetVoterByIdAsync(int id)
    {
        var voter = await _voterRepository.GetQueryable()
            .Include(v => v.Votes)
            .FirstOrDefaultAsync(v => v.Id == id);

        return voter != null ? MapToResponseDto(voter) : null;
    }

    public async Task<VoterResponseDto?> GetVoterByEmailAsync(string email)
    {
        var voter = await _voterRepository.GetQueryable()
            .Include(v => v.Votes)
            .FirstOrDefaultAsync(v => v.Email == email);

        return voter != null ? MapToResponseDto(voter) : null;
    }

    public async Task<VoterResponseDto?> GetVoterByCpfAsync(string cpf)
    {
        var voter = await _voterRepository.GetQueryable()
            .Include(v => v.Votes)
            .FirstOrDefaultAsync(v => v.Cpf == cpf);

        return voter != null ? MapToResponseDto(voter) : null;
    }

    public async Task<VoterResponseDto> CreateVoterAsync(CreateVoterDto createDto, int createdBy)
    {
        // Check if email already exists
        var existingVoterByEmail = await _voterRepository.FirstOrDefaultAsync(v => v.Email == createDto.Email);
        if (existingVoterByEmail != null)
            throw new ArgumentException("Email already exists");

        // Check if CPF already exists
        var existingVoterByCpf = await _voterRepository.FirstOrDefaultAsync(v => v.Cpf == createDto.Cpf);
        if (existingVoterByCpf != null)
            throw new ArgumentException("CPF already exists");

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(createDto.Password);

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString();

        var voter = new Voter
        {
            Name = createDto.Name,
            Email = createDto.Email,
            Password = hashedPassword,
            Cpf = createDto.Cpf,
            BirthDate = createDto.BirthDate,
            Phone = createDto.Phone,
            VoteWeight = createDto.VoteWeight,
            IsActive = true,
            IsVerified = false,
            VerificationToken = verificationToken,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _voterRepository.AddAsync(voter);
        await _auditService.LogAsync(createdBy, "admin", "create", "voters", voter.Id);

        var createdVoter = await GetVoterByIdAsync(voter.Id);
        return createdVoter!;
    }

    public async Task<VoterResponseDto?> UpdateVoterAsync(int id, UpdateVoterDto updateDto, int updatedBy)
    {
        var voter = await _voterRepository.GetByIdAsync(id);
        if (voter == null) return null;

        if (!string.IsNullOrEmpty(updateDto.Name))
            voter.Name = updateDto.Name;

        if (!string.IsNullOrEmpty(updateDto.Email))
        {
            // Check if new email already exists
            var existingVoter = await _voterRepository.FirstOrDefaultAsync(v => v.Email == updateDto.Email && v.Id != id);
            if (existingVoter != null)
                throw new ArgumentException("Email already exists");
            
            voter.Email = updateDto.Email;
            voter.IsVerified = false; // Reset verification if email changed
            voter.VerificationToken = Guid.NewGuid().ToString();
        }

        if (!string.IsNullOrEmpty(updateDto.Password))
        {
            voter.Password = BCrypt.Net.BCrypt.HashPassword(updateDto.Password);
        }

        if (!string.IsNullOrEmpty(updateDto.Cpf))
        {
            // Check if new CPF already exists
            var existingVoter = await _voterRepository.FirstOrDefaultAsync(v => v.Cpf == updateDto.Cpf && v.Id != id);
            if (existingVoter != null)
                throw new ArgumentException("CPF already exists");
            
            voter.Cpf = updateDto.Cpf;
        }

        if (updateDto.BirthDate.HasValue)
            voter.BirthDate = updateDto.BirthDate.Value;

        if (updateDto.Phone != null)
            voter.Phone = updateDto.Phone;

        if (updateDto.VoteWeight.HasValue)
            voter.VoteWeight = updateDto.VoteWeight.Value;

        if (updateDto.IsActive.HasValue)
            voter.IsActive = updateDto.IsActive.Value;

        if (updateDto.IsVerified.HasValue)
            voter.IsVerified = updateDto.IsVerified.Value;

        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(updatedBy, "admin", "update", "voters", voter.Id);

        return await GetVoterByIdAsync(id);
    }

    public async Task<bool> DeleteVoterAsync(int id)
    {
        var voter = await _voterRepository.GetByIdAsync(id);
        if (voter == null) return false;

        await _voterRepository.DeleteAsync(voter);
        return true;
    }

    public async Task<bool> VerifyVoterEmailAsync(string verificationToken)
    {
        var voter = await _voterRepository.FirstOrDefaultAsync(v => v.VerificationToken == verificationToken);
        if (voter == null) return false;

        voter.IsVerified = true;
        voter.EmailVerifiedAt = DateTime.UtcNow;
        voter.VerificationToken = null;
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(voter.Id, "voter", "email_verified", "voters", voter.Id);

        return true;
    }

    public async Task<bool> SendVerificationEmailAsync(int voterId)
    {
        var voter = await _voterRepository.GetByIdAsync(voterId);
        if (voter == null) return false;

        // Generate new verification token
        voter.VerificationToken = Guid.NewGuid().ToString();
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);

        // TODO: Implement actual email sending
        // For now, just log the action
        await _auditService.LogAsync(voterId, "voter", "verification_email_sent", "voters", voterId);

        return true;
    }

    public async Task<VoterStatisticsDto> GetVoterStatisticsAsync()
    {
        var voters = await _voterRepository.GetQueryable()
            .Include(v => v.Votes)
            .ToListAsync();

        var totalVoters = voters.Count;
        var activeVoters = voters.Count(v => v.IsActive);
        var verifiedVoters = voters.Count(v => v.IsVerified);
        var votersWhoVoted = voters.Count(v => v.Votes.Any());

        return new VoterStatisticsDto
        {
            TotalVoters = totalVoters,
            ActiveVoters = activeVoters,
            VerifiedVoters = verifiedVoters,
            VotersWhoVoted = votersWhoVoted,
            VotingPercentage = totalVoters > 0 ? (decimal)votersWhoVoted / totalVoters * 100 : 0
        };
    }

    public async Task<bool> ChangePasswordAsync(int voterId, string currentPassword, string newPassword)
    {
        var voter = await _voterRepository.GetByIdAsync(voterId);
        if (voter == null) return false;

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, voter.Password))
            return false;

        // Update password
        voter.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(voterId, "voter", "password_changed", "voters", voterId);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(string email, string newPassword)
    {
        var voter = await _voterRepository.FirstOrDefaultAsync(v => v.Email == email);
        if (voter == null) return false;

        voter.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(voter.Id, "system", "password_reset", "voters", voter.Id);

        return true;
    }

    private static VoterResponseDto MapToResponseDto(Voter voter)
    {
        return new VoterResponseDto
        {
            Id = voter.Id,
            Name = voter.Name,
            Email = voter.Email,
            Cpf = voter.Cpf,
            BirthDate = voter.BirthDate,
            Phone = voter.Phone,
            VoteWeight = voter.VoteWeight,
            IsActive = voter.IsActive,
            IsVerified = voter.IsVerified,
            EmailVerifiedAt = voter.EmailVerifiedAt,
            LastLoginAt = voter.LastLoginAt,
            LastLoginIp = voter.LastLoginIp,
            TotalVotes = voter.Votes?.Count ?? 0,
            CreatedAt = voter.CreatedAt,
            UpdatedAt = voter.UpdatedAt
        };
    }
}