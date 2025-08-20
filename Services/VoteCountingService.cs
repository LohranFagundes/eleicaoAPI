using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ElectionApi.Net.Data;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class VoteCountingService : IVoteCountingService
{
    private readonly ISecureVoteRepository _secureVoteRepository;
    private readonly IVoteCryptographyService _cryptographyService;
    private readonly IRepository<Election> _electionRepository;
    private readonly IRepository<Position> _positionRepository;
    private readonly IRepository<Candidate> _candidateRepository;
    private readonly IRepository<Voter> _voterRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<VoteCountingService> _logger;

    public VoteCountingService(
        ISecureVoteRepository secureVoteRepository,
        IVoteCryptographyService cryptographyService,
        IRepository<Election> electionRepository,
        IRepository<Position> positionRepository,
        IRepository<Candidate> candidateRepository,
        IRepository<Voter> voterRepository,
        IAuditService auditService,
        ILogger<VoteCountingService> logger)
    {
        _secureVoteRepository = secureVoteRepository;
        _cryptographyService = cryptographyService;
        _electionRepository = electionRepository;
        _positionRepository = positionRepository;
        _candidateRepository = candidateRepository;
        _voterRepository = voterRepository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<VoteCountResultDto> CountVotesAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
                throw new ArgumentException("Election not found");

            var votes = await _secureVoteRepository.GetVotesByElectionAsync(electionId);
            var positionResults = new List<PositionVoteCountDto>();

            foreach (var position in election.Positions)
            {
                var positionVotes = votes.Where(v => v.PositionId == position.Id).ToList();
                var positionResult = await CountVotesByPositionInternalAsync(electionId, position.Id, positionVotes);
                positionResults.Add(positionResult);
            }

            var result = new VoteCountResultDto
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                TotalVotes = votes.Count(),
                TotalValidVotes = votes.Count(v => !v.IsBlankVote && !v.IsNullVote),
                TotalBlankVotes = votes.Count(v => v.IsBlankVote),
                TotalNullVotes = votes.Count(v => v.IsNullVote),
                CountedAt = DateTime.UtcNow,
                PositionResults = positionResults
            };

            await _auditService.LogAsync(null, "system", "count_votes", "election", electionId,
                $"Vote counting completed for election '{election.Title}'. Total votes: {result.TotalVotes}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count votes for election {ElectionId}", electionId);
            throw;
        }
    }

    public async Task<PositionVoteCountDto> CountVotesByPositionAsync(int electionId, int positionId)
    {
        try
        {
            var votes = await _secureVoteRepository.GetVotesByPositionAsync(electionId, positionId);
            return await CountVotesByPositionInternalAsync(electionId, positionId, votes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count votes for position {PositionId}", positionId);
            throw;
        }
    }

    public async Task<bool> ValidateAllVotesIntegrityAsync(int electionId)
    {
        try
        {
            var votes = await _secureVoteRepository.GetVotesByElectionAsync(electionId);
            var validationTasks = votes.Select(vote => ValidateVoteIntegrityAsync(vote));
            var results = await Task.WhenAll(validationTasks);

            var allValid = results.All(r => r);
            
            if (!allValid)
            {
                _logger.LogWarning("Integrity validation failed for some votes in election {ElectionId}", electionId);
            }

            await _auditService.LogAsync(null, "system", "validate_votes_integrity", "election", electionId,
                $"Vote integrity validation completed. All valid: {allValid}");

            return allValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate votes integrity for election {ElectionId}", electionId);
            throw;
        }
    }

    public async Task<ElectionResultsDto> GenerateElectionResultsAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                    .ThenInclude(p => p.Candidates)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
                throw new ArgumentException("Election not found");

            var voteCounts = await CountVotesAsync(electionId);
            var results = new List<PositionResultDto>();

            foreach (var positionCount in voteCounts.PositionResults)
            {
                var position = election.Positions.First(p => p.Id == positionCount.PositionId);
                var winners = DetermineWinners(positionCount, position.MaxCandidates ?? 10);

                var positionResult = new PositionResultDto
                {
                    PositionId = position.Id,
                    PositionName = position.Title,
                    Winners = winners,
                    AllResults = positionCount.CandidateResults
                };

                results.Add(positionResult);
            }

            var electionResults = new ElectionResultsDto
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                Status = election.IsSealed ? "sealed" : "open",
                CountedAt = DateTime.UtcNow,
                VoteCounts = voteCounts,
                Results = results
            };

            // Generate result hash for integrity
            var resultJson = JsonSerializer.Serialize(electionResults, new JsonSerializerOptions { WriteIndented = false });
            electionResults.ResultHash = ComputeHash(resultJson);

            await _auditService.LogAsync(null, "system", "generate_election_results", "election", electionId,
                $"Election results generated with hash: {electionResults.ResultHash}");

            return electionResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate election results for election {ElectionId}", electionId);
            throw;
        }
    }

    public async Task<List<VoteAuditDto>> GetVoteAuditTrailAsync(int electionId, bool includeDecryptedData = false)
    {
        try
        {
            var votes = await _secureVoteRepository.GetVotesByElectionAsync(electionId);
            var auditTrail = new List<VoteAuditDto>();

            foreach (var vote in votes)
            {
                var voter = await _voterRepository.GetByIdAsync(vote.VoterId);
                var position = await _positionRepository.GetByIdAsync(vote.PositionId);

                var auditEntry = new VoteAuditDto
                {
                    VoteId = vote.VoteId,
                    VoterId = vote.VoterId,
                    VoterName = voter?.Name ?? "Unknown",
                    ElectionId = vote.ElectionId,
                    PositionId = vote.PositionId,
                    PositionName = position?.Title ?? "Unknown",
                    VotedAt = vote.VotedAt,
                    VoteType = vote.VoteType,
                    VoteHash = vote.VoteHash,
                    DeviceFingerprint = vote.DeviceFingerprint,
                    IntegrityValid = await ValidateVoteIntegrityAsync(vote)
                };

                // Only decrypt data if explicitly requested and authorized
                if (includeDecryptedData && vote.ElectionSealHash != null)
                {
                    try
                    {
                        var decryptedData = await _cryptographyService.DecryptVoteDataAsync(vote.EncryptedVoteData, vote.ElectionSealHash);
                        auditEntry.DecryptedCandidateId = decryptedData.CandidateId;
                        auditEntry.DecryptedCandidateName = decryptedData.CandidateName;
                        
                        if (!string.IsNullOrEmpty(vote.EncryptedJustification))
                        {
                            auditEntry.DecryptedJustification = await _cryptographyService.DecryptJustificationAsync(vote.EncryptedJustification);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to decrypt vote data for audit trail. VoteId: {VoteId}", vote.VoteId);
                    }
                }

                auditTrail.Add(auditEntry);
            }

            await _auditService.LogAsync(null, "system", "get_vote_audit_trail", "election", electionId,
                $"Vote audit trail generated. Include decrypted data: {includeDecryptedData}");

            return auditTrail.OrderBy(a => a.VotedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vote audit trail for election {ElectionId}", electionId);
            throw;
        }
    }

    private async Task<PositionVoteCountDto> CountVotesByPositionInternalAsync(int electionId, int positionId, IEnumerable<SecureVote> votes)
    {
        var position = await _positionRepository.GetByIdAsync(positionId);
        var candidates = await _candidateRepository.GetQueryable()
            .Where(c => c.PositionId == positionId)
            .ToListAsync();

        var election = await _electionRepository.GetByIdAsync(electionId);
        
        var candidateResults = new List<CandidateVoteCountDto>();
        var totalVotes = votes.Count();

        foreach (var candidate in candidates)
        {
            var candidateVotes = new List<SecureVote>();

            // Decrypt votes to count for this candidate
            foreach (var vote in votes.Where(v => !v.IsBlankVote && !v.IsNullVote))
            {
                try
                {
                    var decryptedData = await _cryptographyService.DecryptVoteDataAsync(vote.EncryptedVoteData, vote.ElectionSealHash);
                    if (decryptedData.CandidateId == candidate.Id)
                    {
                        candidateVotes.Add(vote);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt vote for counting. VoteId: {VoteId}", vote.VoteId);
                }
            }

            var voteCount = candidateVotes.Count;
            var weightedVotes = candidateVotes.Sum(v => (decimal)v.VoteWeight);
            var percentage = totalVotes > 0 ? (decimal)voteCount / totalVotes * 100 : 0;

            candidateResults.Add(new CandidateVoteCountDto
            {
                CandidateId = candidate.Id,
                CandidateName = candidate.Name,
                CandidateNumber = candidate.Number,
                VoteCount = voteCount,
                VotePercentage = percentage,
                WeightedVotes = weightedVotes
            });
        }

        return new PositionVoteCountDto
        {
            PositionId = positionId,
            PositionName = position?.Title ?? "Unknown",
            TotalVotes = totalVotes,
            TotalBlankVotes = votes.Count(v => v.IsBlankVote),
            TotalNullVotes = votes.Count(v => v.IsNullVote),
            CandidateResults = candidateResults.OrderByDescending(c => c.VoteCount).ToList()
        };
    }

    private async Task<bool> ValidateVoteIntegrityAsync(SecureVote vote)
    {
        try
        {
            // Validate vote signature and integrity
            var isSignatureValid = _cryptographyService.ValidateVoteIntegrity(vote.EncryptedVoteData, vote.VoteHash, vote.VoteSignature);
            
            // Validate repository-level integrity
            var isRepositoryValid = await _secureVoteRepository.ValidateVoteIntegrityAsync(vote.VoteId);

            return isSignatureValid && isRepositoryValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate vote integrity. VoteId: {VoteId}", vote.VoteId);
            return false;
        }
    }

    private List<CandidateResultDto> DetermineWinners(PositionVoteCountDto positionCount, int maxCandidates)
    {
        return positionCount.CandidateResults
            .OrderByDescending(c => c.VoteCount)
            .Take(maxCandidates)
            .Select(c => new CandidateResultDto
            {
                CandidateId = c.CandidateId ?? 0,
                CandidateName = c.CandidateName ?? "Unknown",
                CandidateNumber = c.CandidateNumber ?? "",
                VoteCount = c.VoteCount,
                VotePercentage = c.VotePercentage,
                IsWinner = true
            })
            .ToList();
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}