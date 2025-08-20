using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public interface IAuthService
{
    Task<Admin?> ValidateAdminCredentialsAsync(string email, string password);
    Task<Voter?> ValidateVoterCredentialsAsync(string email, string password);
    Task<string> GenerateJwtTokenAsync(int userId, string role);
    Task<bool> ValidateTokenAsync(string token);
    Task UpdateLastLoginAsync(int userId, string userType);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}