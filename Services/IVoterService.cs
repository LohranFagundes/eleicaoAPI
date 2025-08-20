using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface IVoterService
{
    Task<PagedResult<VoterResponseDto>> GetVotersAsync(int page, int limit, bool? isActive = null, bool? isVerified = null);
    Task<VoterResponseDto?> GetVoterByIdAsync(int id);
    Task<VoterResponseDto?> GetVoterByEmailAsync(string email);
    Task<VoterResponseDto?> GetVoterByCpfAsync(string cpf);
    Task<VoterResponseDto> CreateVoterAsync(CreateVoterDto createDto, int createdBy);
    Task<VoterResponseDto?> UpdateVoterAsync(int id, UpdateVoterDto updateDto, int updatedBy);
    Task<bool> DeleteVoterAsync(int id);
    Task<bool> VerifyVoterEmailAsync(string verificationToken);
    Task<bool> SendVerificationEmailAsync(int voterId);
    Task<VoterStatisticsDto> GetVoterStatisticsAsync();
    Task<bool> ChangePasswordAsync(int voterId, string currentPassword, string newPassword);
    Task<bool> ResetPasswordAsync(string email, string newPassword);
}