using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class SystemSealService : ISystemSealService
{
    private readonly IRepository<SystemSeal> _systemSealRepository;
    private readonly IRepository<Election> _electionRepository;
    private readonly IRepository<Voter> _voterRepository;
    private readonly IRepository<Candidate> _candidateRepository;
    private readonly ISecureVoteRepository _secureVoteRepository;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SystemSealService> _logger;

    public SystemSealService(
        IRepository<SystemSeal> systemSealRepository,
        IRepository<Election> electionRepository,
        IRepository<Voter> voterRepository,
        IRepository<Candidate> candidateRepository,
        ISecureVoteRepository secureVoteRepository,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SystemSealService> logger)
    {
        _systemSealRepository = systemSealRepository;
        _electionRepository = electionRepository;
        _voterRepository = voterRepository;
        _candidateRepository = candidateRepository;
        _secureVoteRepository = secureVoteRepository;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<SystemSeal> GenerateSystemSealAsync(int electionId, int adminId)
    {
        var election = await _electionRepository.GetByIdAsync(electionId);
        if (election == null)
        {
            throw new ArgumentException($"Election with ID {electionId} not found.");
        }

        // Get counts
        var totalRegisteredVoters = await _voterRepository.GetQueryable().CountAsync();
        var totalCandidates = await _candidateRepository.GetQueryable().CountAsync();
        var totalVotes = await _secureVoteRepository.GetVotesByElectionAsync(electionId);
        var totalVoteCount = totalVotes.Count();

        var systemData = new
        {
            ElectionId = electionId,
            ElectionTitle = election.Title,
            TotalRegisteredVoters = totalRegisteredVoters,
            TotalCandidates = totalCandidates,
            TotalVotesCast = totalVoteCount,
            Timestamp = DateTime.UtcNow
        };

        var systemDataJson = JsonSerializer.Serialize(systemData);
        var sealHash = ComputeHash(systemDataJson);

        var systemSeal = new SystemSeal
        {
            ElectionId = electionId,
            SealHash = sealHash,
            SealType = "election_seal",
            SealedAt = DateTime.UtcNow,
            SealedBy = adminId,
            SystemData = systemDataJson,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent()
        };

        await _systemSealRepository.AddAsync(systemSeal);

        await _auditService.LogAsync(adminId, "admin", "generate_system_seal", "election", electionId,
            $"System seal generated for election {electionId} with hash {sealHash}. Data: {systemDataJson}");

        return systemSeal;
    }

    public async Task<bool> ValidateSystemSealAsync(int electionId, string sealHash)
    {
        var latestSeal = await GetLatestSystemSealAsync(electionId);
        if (latestSeal == null)
        {
            _logger.LogWarning("No system seal found for election {ElectionId} to validate.", electionId);
            return false;
        }

        var currentSystemData = new
        {
            ElectionId = electionId,
            ElectionTitle = (await _electionRepository.GetByIdAsync(electionId))?.Title,
            TotalRegisteredVoters = await _voterRepository.GetQueryable().CountAsync(),
            TotalCandidates = await _candidateRepository.GetQueryable().CountAsync(),
            TotalVotesCast = (await _secureVoteRepository.GetVotesByElectionAsync(electionId)).Count(),
            Timestamp = DateTime.UtcNow // This timestamp will be different, so we need to exclude it from hash comparison
        };

        // Re-serialize the stored system data to ensure consistent hashing
        var storedSystemData = JsonSerializer.Deserialize<dynamic>(latestSeal.SystemData);
        var storedSystemDataForHash = new
        {
            ElectionId = (int)storedSystemData.ElectionId,
            ElectionTitle = (string)storedSystemData.ElectionTitle,
            TotalRegisteredVoters = (int)storedSystemData.TotalRegisteredVoters,
            TotalCandidates = (int)storedSystemData.TotalCandidates,
            TotalVotesCast = (int)storedSystemData.TotalVotesCast,
            Timestamp = (DateTime)storedSystemData.Timestamp // Use the original timestamp for hash comparison
        };

        var currentSystemDataForHash = new
        {
            ElectionId = currentSystemData.ElectionId,
            ElectionTitle = currentSystemData.ElectionTitle,
            TotalRegisteredVoters = currentSystemData.TotalRegisteredVoters,
            TotalCandidates = currentSystemData.TotalCandidates,
            TotalVotesCast = currentSystemData.TotalVotesCast,
            Timestamp = storedSystemDataForHash.Timestamp // Use the original timestamp for hash comparison
        };

        var currentHash = ComputeHash(JsonSerializer.Serialize(currentSystemDataForHash));

        var isValid = string.Equals(latestSeal.SealHash, currentHash, StringComparison.OrdinalIgnoreCase);

        await _auditService.LogAsync(null, "system", "validate_system_seal", "election", electionId,
            $"System seal validation for election {electionId}. Provided hash: {sealHash}, Latest hash: {latestSeal.SealHash}, Current calculated hash: {currentHash}. Is Valid: {isValid}");

        return isValid;
    }

    public async Task<SystemSeal?> GetLatestSystemSealAsync(int electionId)
    {
        return await _systemSealRepository.GetQueryable()
            .Where(s => s.ElectionId == electionId)
            .OrderByDescending(s => s.SealedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<SystemSealVerificationDto> VerifySystemSealAsync(int electionId, string providedSealHash)
    {
        var latestSeal = await GetLatestSystemSealAsync(electionId);
        var verificationResult = new SystemSealVerificationDto
        {
            ElectionId = electionId,
            ProvidedSealHash = providedSealHash
        };

        if (latestSeal == null)
        {
            verificationResult.IsValid = false;
            verificationResult.Message = "No system seal found for this election.";
            return verificationResult;
        }

        verificationResult.StoredSealHash = latestSeal.SealHash;
        verificationResult.SealedAt = latestSeal.SealedAt;
        verificationResult.SystemData = JsonSerializer.Deserialize<object>(latestSeal.SystemData);

        var currentSystemData = new
        {
            ElectionId = electionId,
            ElectionTitle = (await _electionRepository.GetByIdAsync(electionId))?.Title,
            TotalRegisteredVoters = await _voterRepository.GetQueryable().CountAsync(),
            TotalCandidates = await _candidateRepository.GetQueryable().CountAsync(),
            TotalVotesCast = (await _secureVoteRepository.GetVotesByElectionAsync(electionId)).Count(),
            Timestamp = latestSeal.SealedAt // Use the original timestamp for hash calculation
        };

        var currentCalculatedHash = ComputeHash(JsonSerializer.Serialize(currentSystemData));
        verificationResult.CurrentCalculatedHash = currentCalculatedHash;

        verificationResult.IsValid = string.Equals(providedSealHash, latestSeal.SealHash, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(latestSeal.SealHash, currentCalculatedHash, StringComparison.OrdinalIgnoreCase);

        if (verificationResult.IsValid)
        {
            verificationResult.Message = "System seal is valid and matches current system state.";
        }
        else
        {
            verificationResult.Message = "System seal is invalid or does not match current system state.";
            if (!string.Equals(providedSealHash, latestSeal.SealHash, StringComparison.OrdinalIgnoreCase))
            {
                verificationResult.Message += " Provided hash does not match stored hash.";
            }
            else if (!string.Equals(latestSeal.SealHash, currentCalculatedHash, StringComparison.OrdinalIgnoreCase))
            {
                verificationResult.Message += " Stored hash does not match current calculated hash (data may have changed).";
            }
        }

        await _auditService.LogAsync(null, "system", "verify_system_seal", "election", electionId,
            $"System seal verification for election {electionId}. Is Valid: {verificationResult.IsValid}. Message: {verificationResult.Message}");

        return verificationResult;
    }

    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? 
               httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
    }

    private string? GetUserAgent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }
}
