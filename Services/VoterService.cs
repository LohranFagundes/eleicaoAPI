using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class VoterService : IVoterService
{
    private readonly IRepository<Voter> _voterRepository;
    private readonly IRepository<Election> _electionRepository;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public VoterService(IRepository<Voter> voterRepository, IRepository<Election> electionRepository, 
        IAuditService auditService, IEmailService emailService, IConfiguration configuration)
    {
        _voterRepository = voterRepository;
        _electionRepository = electionRepository;
        _auditService = auditService;
        _emailService = emailService;
        _configuration = configuration;
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
        // Verificar se existem eleições ativas que impedem a operação
        if (await HasActiveElectionsAsync())
        {
            // Log com email instead of voter ID since we haven't found the voter yet
            await _auditService.LogAsync(0, "system", "password_reset_blocked", "voters", null, 
                $"Password reset blocked due to active election for email: {email}");
            throw new InvalidOperationException("Reset de senha não é permitido durante eleições ativas por motivos de segurança. Use a funcionalidade de alteração de senha se souber a senha atual.");
        }

        var voter = await _voterRepository.FirstOrDefaultAsync(v => v.Email == email);
        if (voter == null) return false;

        voter.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(voter.Id, "system", "password_reset", "voters", voter.Id);

        return true;
    }

    /// <summary>
    /// Verifica se existem eleições ativas/seladas que impedem alterações de senha
    /// </summary>
    private async Task<bool> HasActiveElectionsAsync()
    {
        var now = DateTime.UtcNow;
        var activeElections = await _electionRepository.GetQueryable()
            .Where(e => e.IsSealed && 
                       e.StartDate <= now && 
                       e.EndDate >= now &&
                       (e.Status == "active" || e.Status == "completed"))
            .AnyAsync();

        return activeElections;
    }

    public async Task<bool> RequestPasswordResetAsync(string email)
    {
        var voter = await _voterRepository.FirstOrDefaultAsync(v => v.Email == email);
        if (voter == null) return false; // Don't reveal if email exists

        // Generate secure reset token
        var resetToken = GenerateSecureToken();
        var tokenExpiry = DateTime.UtcNow.AddMinutes(30); // 30 minutes expiry

        voter.PasswordResetToken = resetToken;
        voter.PasswordResetTokenExpiry = tokenExpiry;
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);

        // Send password reset email
        try
        {
            var voterFrontendUrl = _configuration["VOTER_FRONTEND_URL"] 
                                ?? _configuration["FrontendUrls:VoterFrontendUrl"] 
                                ?? "http://localhost:5112";
            var resetLink = $"{voterFrontendUrl}/reset-password?token={resetToken}";
            
            // Log temporário para verificar a URL gerada
            await _auditService.LogAsync(voter.Id, "system", "password_reset_url_generated", "voters", voter.Id,
                $"Password reset URL generated: {resetLink} | Frontend URL config: {voterFrontendUrl}");
                
            var emailBody = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Redefinição de Senha</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;"">
    <div style=""background-color: #f8f9fa; padding: 30px; border-radius: 10px; border: 1px solid #dee2e6;"">
        <h2 style=""color: #007bff; text-align: center; margin-bottom: 30px;"">🔒 Redefinição de Senha</h2>
        <h3 style=""color: #495057;"">Olá {voter.Name},</h3>
        
        <p>Você solicitou a redefinição da sua senha no <strong>Sistema de Eleições</strong>.</p>
        
        <p>Para criar uma nova senha, <strong>copie e cole o link completo abaixo</strong> no seu navegador:</p>
        
        <div style=""background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #007bff;"">
            <p style=""margin: 0; font-family: monospace; font-size: 14px; word-break: break-all;"">
                <strong>Link:</strong> {resetLink}
            </p>
        </div>
        
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{resetLink}"" 
               style=""display: inline-block; background-color: #007bff; color: white; padding: 12px 25px; 
                      text-decoration: none; border-radius: 5px; font-weight: bold; font-size: 16px;"">
                🔗 Redefinir Senha
            </a>
        </div>
        
        <div style=""background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;"">
            <p style=""margin: 0; color: #856404;"">
                ⚠️ <strong>Importante:</strong> Este link expira em <strong>30 minutos</strong> por segurança.
            </p>
        </div>
        
        <p>Se você não solicitou esta alteração, pode ignorar este email com segurança.</p>
        
        <hr style=""border: none; border-top: 1px solid #dee2e6; margin: 30px 0;"">
        
        <p style=""font-size: 12px; color: #6c757d; text-align: center;"">
            Sistema de Eleições - Mensagem automática<br>
            Este é um email automático, não responda a esta mensagem.
        </p>
    </div>
</body>
</html>";

            // Check if we're in development environment to avoid Office365 link protection
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            
            var emailDto = new SendEmailDto
            {
                ToEmail = voter.Email,
                ToName = voter.Name,
                Subject = "Redefinição de Senha - Sistema de Eleições",
                Body = emailBody,
                IsHtml = !isDevelopment // Use plain text in development to avoid link redirection
            };

            // If development, add a plain text alternative
            if (isDevelopment)
            {
                emailDto.Body = $@"REDEFINIÇÃO DE SENHA - Sistema de Eleições

Olá {voter.Name},

Você solicitou a redefinição da sua senha no Sistema de Eleições.

Para criar uma nova senha, copie e cole o link completo abaixo no seu navegador:

{resetLink}

IMPORTANTE: Este link expira em 30 minutos por segurança.

Se você não solicitou esta alteração, pode ignorar este email com segurança.

---
Sistema de Eleições - Mensagem automática
Este é um email automático, não responda a esta mensagem.";
            }

            await _emailService.SendEmailAsync(emailDto);
            await _auditService.LogAsync(voter.Id, "system", "password_reset_requested", "voters", voter.Id,
                $"Password reset token generated and email sent to {voter.Email}");

            return true;
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(voter.Id, "system", "password_reset_email_failed", "voters", voter.Id,
                $"Failed to send password reset email: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            return false;

        var voter = await _voterRepository.FirstOrDefaultAsync(v => 
            v.PasswordResetToken == token && 
            v.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (voter == null) return false; // Token invalid or expired

        // Verificar se existem eleições ativas que impedem a operação
        if (await HasActiveElectionsAsync())
        {
            await _auditService.LogAsync(voter.Id, "system", "password_reset_blocked", "voters", voter.Id, 
                "Password reset with token blocked due to active election");
            throw new InvalidOperationException("Reset de senha não é permitido durante eleições ativas por motivos de segurança.");
        }

        // Reset password
        voter.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        voter.PasswordResetToken = null; // Clear the token
        voter.PasswordResetTokenExpiry = null;
        voter.UpdatedAt = DateTime.UtcNow;

        await _voterRepository.UpdateAsync(voter);
        await _auditService.LogAsync(voter.Id, "system", "password_reset_with_token", "voters", voter.Id,
            "Password successfully reset using secure token");

        return true;
    }

    private static string GenerateSecureToken()
    {
        // Generate a cryptographically secure random token
        var randomBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes).Replace("/", "-").Replace("+", "_").Replace("=", "");
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