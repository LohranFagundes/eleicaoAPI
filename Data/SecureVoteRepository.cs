using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Data;

public class SecureVoteRepository : ISecureVoteRepository
{
    private readonly ElectionDbContext _context;
    private readonly ILogger<SecureVoteRepository> _logger;

    public SecureVoteRepository(ElectionDbContext context, ILogger<SecureVoteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SecureVote> CreateVoteAsync(SecureVote vote)
    {
        try
        {
            _context.SecureVotes.Add(vote);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Secure vote created with ID: {VoteId}", vote.VoteId);
            return vote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create secure vote");
            throw;
        }
    }

    public async Task<bool> HasVoterVotedAsync(int voterId, int electionId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.VoterId == voterId && v.ElectionId == electionId && v.IsValid)
                .AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if voter has voted");
            throw;
        }
    }

    public async Task<IEnumerable<SecureVote>> GetVotesByElectionAsync(int electionId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.ElectionId == electionId && v.IsValid)
                .Include(v => v.Election)
                .Include(v => v.Position)
                .OrderBy(v => v.VotedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get votes by election");
            throw;
        }
    }

    public async Task<IEnumerable<SecureVote>> GetVotesByPositionAsync(int electionId, int positionId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.ElectionId == electionId && v.PositionId == positionId && v.IsValid)
                .Include(v => v.Election)
                .Include(v => v.Position)
                .OrderBy(v => v.VotedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get votes by position");
            throw;
        }
    }

    public async Task<int> CountVotesByElectionAsync(int electionId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.ElectionId == electionId && v.IsValid)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count votes by election");
            throw;
        }
    }

    public async Task<int> CountVotesByPositionAsync(int electionId, int positionId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.ElectionId == electionId && v.PositionId == positionId && v.IsValid)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count votes by position");
            throw;
        }
    }

    public async Task<SecureVote?> GetVoteByIdAsync(string voteId)
    {
        try
        {
            return await _context.SecureVotes
                .Where(v => v.VoteId == voteId && v.IsValid)
                .Include(v => v.Voter)
                .Include(v => v.Election)
                .Include(v => v.Position)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vote by ID");
            throw;
        }
    }

    public async Task<bool> ValidateVoteIntegrityAsync(string voteId)
    {
        try
        {
            var vote = await GetVoteByIdAsync(voteId);
            if (vote == null) return false;

            // Validar hash de criação
            var expectedCreationHash = GenerateCreationHash(vote);
            if (vote.CreationHash != expectedCreationHash)
            {
                _logger.LogWarning("Vote integrity check failed for VoteId: {VoteId} - Creation hash mismatch", voteId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate vote integrity");
            return false;
        }
    }

    private string GenerateCreationHash(SecureVote vote)
    {
        var data = $"{vote.VoteId}-{vote.VoterId}-{vote.ElectionId}-{vote.CreatedAt:yyyy-MM-dd HH:mm:ss.fff}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}