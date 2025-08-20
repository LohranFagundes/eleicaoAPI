using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public interface ISystemSealService
{
    Task<SystemSeal> GenerateSystemSealAsync(int electionId, int adminId);
    Task<bool> ValidateSystemSealAsync(int electionId, string sealHash);
    Task<SystemSeal?> GetLatestSystemSealAsync(int electionId);
    Task<SystemSealVerificationDto> VerifySystemSealAsync(int electionId, string providedSealHash);
}