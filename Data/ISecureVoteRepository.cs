using ElectionApi.Net.Models;

namespace ElectionApi.Net.Data;

public interface ISecureVoteRepository
{
    Task<SecureVote> CreateVoteAsync(SecureVote vote);
    Task<bool> HasVoterVotedAsync(int voterId, int electionId);
    Task<IEnumerable<SecureVote>> GetVotesByElectionAsync(int electionId);
    Task<IEnumerable<SecureVote>> GetVotesByPositionAsync(int electionId, int positionId);
    Task<int> CountVotesByElectionAsync(int electionId);
    Task<int> CountVotesByPositionAsync(int electionId, int positionId);
    Task<SecureVote?> GetVoteByIdAsync(string voteId);
    Task<bool> ValidateVoteIntegrityAsync(string voteId);
}