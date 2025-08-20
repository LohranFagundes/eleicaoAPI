using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class VotingService : IVotingService
{
    private readonly IRepository<Voter> _voterRepository;
    private readonly IRepository<Election> _electionRepository;
    private readonly IRepository<Position> _positionRepository;
    private readonly IRepository<Candidate> _candidateRepository;
    private readonly IRepository<Vote> _voteRepository;
    private readonly ISecureVoteRepository _secureVoteRepository;
    private readonly IVoteCryptographyService _cryptographyService;
    private readonly IRepository<VoteReceipt> _receiptRepository;
    private readonly IRepository<SystemSeal> _sealRepository;
    private readonly IRepository<ZeroReport> _zeroReportRepository;
    private readonly IAuditService _auditService;
    private readonly IAuthService _authService;
    private readonly ILogger<VotingService> _logger;
    private readonly IDateTimeService _dateTimeService;

    public VotingService(
        IRepository<Voter> voterRepository,
        IRepository<Election> electionRepository,
        IRepository<Position> positionRepository,
        IRepository<Candidate> candidateRepository,
        IRepository<Vote> voteRepository,
        ISecureVoteRepository secureVoteRepository,
        IVoteCryptographyService cryptographyService,
        IRepository<VoteReceipt> receiptRepository,
        IRepository<SystemSeal> sealRepository,
        IRepository<ZeroReport> zeroReportRepository,
        IAuditService auditService,
        IAuthService authService,
        ILogger<VotingService> logger,
        IDateTimeService dateTimeService)
    {
        _voterRepository = voterRepository;
        _electionRepository = electionRepository;
        _positionRepository = positionRepository;
        _candidateRepository = candidateRepository;
        _voteRepository = voteRepository;
        _secureVoteRepository = secureVoteRepository;
        _cryptographyService = cryptographyService;
        _receiptRepository = receiptRepository;
        _sealRepository = sealRepository;
        _zeroReportRepository = zeroReportRepository;
        _auditService = auditService;
        _authService = authService;
        _logger = logger;
        _dateTimeService = dateTimeService;
    }

    public async Task<ApiResponse<object>> LoginVoterAsync(VotingLoginDto loginDto, string ipAddress, string userAgent)
    {
        try
        {
            var voter = await _voterRepository.GetQueryable()
                .FirstOrDefaultAsync(v => v.Cpf == loginDto.Cpf);

            if (voter == null || !_authService.VerifyPassword(loginDto.Password, voter.Password))
            {
                await _auditService.LogAsync(null, "voter", "login_failed", "authentication", null,
                    $"Failed login attempt for CPF: {loginDto.Cpf}");
                return ApiResponse<object>.ErrorResult("CPF ou senha inválidos");
            }

            if (!voter.IsActive)
            {
                await _auditService.LogAsync(voter.Id, "voter", "login_blocked", "authentication", null,
                    "Login attempt by inactive voter");
                return ApiResponse<object>.ErrorResult("Voter inativo");
            }

            var election = await _electionRepository.GetByIdAsync(loginDto.ElectionId);
            if (election == null)
            {
                return ApiResponse<object>.ErrorResult("Eleição não encontrada");
            }

            var canVoteResult = await CanVoteInElectionAsync(voter.Id, loginDto.ElectionId);
            if (!canVoteResult.Success || !canVoteResult.Data)
            {
                return ApiResponse<object>.ErrorResult(canVoteResult.Message);
            }

            voter.LastLoginAt = DateTime.UtcNow;
            voter.LastLoginIp = ipAddress;
            await _voterRepository.UpdateAsync(voter);

            var token = await _authService.GenerateJwtTokenAsync(voter.Id, "voter");

            await _auditService.LogAsync(voter.Id, "voter", "login_success", "authentication", null,
                "Voter logged in for voting");

            return ApiResponse<object>.SuccessResult(new
            {
                Token = token,
                VoterId = voter.Id,
                VoterName = voter.Name,
                ElectionId = election.Id,
                ElectionTitle = election.Title
            }, "Login realizado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during voter login");
            return ApiResponse<object>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<ElectionStatusDto>> GetElectionStatusAsync(int electionId, int voterId)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                    .ThenInclude(p => p.Candidates)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                return ApiResponse<ElectionStatusDto>.ErrorResult("Eleição não encontrada");
            }

            var canVoteResult = await CanVoteInElectionAsync(voterId, electionId);
            var hasVotedResult = await HasVoterVotedAsync(voterId, electionId);

            var status = new ElectionStatusDto
            {
                ElectionId = election.Id,
                Title = election.Title,
                Status = election.IsSealed ? "sealed" : "open",
                StartDate = election.StartDate,
                EndDate = election.EndDate,
                IsSealed = election.IsSealed,
                SealedAt = election.SealedAt,
                CanVote = canVoteResult.Success && canVoteResult.Data && !hasVotedResult.Data,
                Message = GetElectionStatusMessage(election, canVoteResult.Data, hasVotedResult.Data),
                Positions = election.Positions.Select(p => new PositionSummaryDto
                {
                    PositionId = p.Id,
                    Name = p.Title,
                    Description = p.Description ?? "",
                    MaxCandidates = p.MaxCandidates ?? 10,
                    Candidates = p.Candidates.Select(c => new CandidateSummaryDto
                    {
                        CandidateId = c.Id,
                        Name = c.Name,
                        Number = c.Number,
                        Party = c.Party,
                        PhotoUrl = c.PhotoUrl,
                        Biography = c.Biography
                    }).ToList()
                }).ToList()
            };

            return ApiResponse<ElectionStatusDto>.SuccessResult(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting election status");
            return ApiResponse<ElectionStatusDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<VoteReceiptDto>> CastVoteAsync(VotingCastVoteDto voteDto, int voterId, string ipAddress, string userAgent)
    {
        try
        {
            // Validate election and voting permissions
            var canVoteResult = await CanVoteInElectionAsync(voterId, voteDto.ElectionId);
            if (!canVoteResult.Success || !canVoteResult.Data)
            {
                return ApiResponse<VoteReceiptDto>.ErrorResult(canVoteResult.Message);
            }

            var hasVotedResult = await HasVoterVotedAsync(voterId, voteDto.ElectionId);
            if (hasVotedResult.Data)
            {
                return ApiResponse<VoteReceiptDto>.ErrorResult("Você já votou nesta eleição");
            }

            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                .FirstOrDefaultAsync(e => e.Id == voteDto.ElectionId);

            if (election.IsSealed)
            {
                return ApiResponse<VoteReceiptDto>.ErrorResult("Eleição está lacrada e não pode receber votos");
            }

            var position = await _positionRepository.GetByIdAsync(voteDto.PositionId);
            if (position == null || position.ElectionId != voteDto.ElectionId)
            {
                return ApiResponse<VoteReceiptDto>.ErrorResult("Cargo inválido para esta eleição");
            }

            Candidate? candidate = null;
            if (voteDto.CandidateId.HasValue && !voteDto.IsBlankVote && !voteDto.IsNullVote)
            {
                candidate = await _candidateRepository.GetByIdAsync(voteDto.CandidateId.Value);
                if (candidate == null || candidate.PositionId != voteDto.PositionId)
                {
                    return ApiResponse<VoteReceiptDto>.ErrorResult("Candidato inválido para este cargo");
                }
            }

            var voter = await _voterRepository.GetByIdAsync(voterId);

            // Create the secure vote
            var voteId = Guid.NewGuid().ToString("N").ToUpper();
            var votedAt = DateTime.UtcNow;

            // Prepare encryption data
            var voteEncryptionData = new VoteEncryptionData
            {
                CandidateId = voteDto.CandidateId,
                CandidateName = candidate?.Name,
                CandidateNumber = candidate?.Number,
                IsBlankVote = voteDto.IsBlankVote,
                IsNullVote = voteDto.IsNullVote,
                EncryptedAt = votedAt,
                VoteId = voteId
            };

            // Encrypt vote data
            var encryptedVoteData = await _cryptographyService.EncryptVoteDataAsync(voteEncryptionData, election.SealHash!);
            var voteHash = _cryptographyService.GenerateVoteHash(voteId, voterId, voteDto.CandidateId ?? 0, votedAt);
            var voteSignature = _cryptographyService.GenerateVoteSignature(voteHash, encryptedVoteData);
            var creationHash = _cryptographyService.GenerateCreationHash(voteId, voterId, voteDto.ElectionId, votedAt);
            var deviceFingerprint = _cryptographyService.GenerateDeviceFingerprint(userAgent, ipAddress);
            var encryptedJustification = await _cryptographyService.EncryptJustificationAsync(voteDto.Justification);

            var secureVote = new SecureVote
            {
                VoteId = voteId,
                VoteType = GetVoteType(voteDto),
                EncryptedVoteData = encryptedVoteData,
                VoteHash = voteHash,
                VoteSignature = voteSignature,
                VoteWeight = voter.VoteWeight,
                VotedAt = votedAt,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceFingerprint = deviceFingerprint,
                VoterId = voterId,
                ElectionId = voteDto.ElectionId,
                PositionId = voteDto.PositionId,
                IsBlankVote = voteDto.IsBlankVote,
                IsNullVote = voteDto.IsNullVote,
                EncryptedJustification = encryptedJustification,
                CreationHash = creationHash,
                CreatedAt = votedAt,
                ElectionSealHash = election.SealHash!
            };

            await _secureVoteRepository.CreateVoteAsync(secureVote);

            // Generate vote receipt
            var receipt = await GenerateVoteReceiptAsync(secureVote, voter, election, position, candidate);

            await _auditService.LogAsync(voterId, "voter", "cast_vote", "secure_vote", secureVote.Id,
                $"Secure vote cast in election {election.Title} for position {position.Title}");

            _logger.LogInformation("Secure vote cast successfully. Voter: {VoterId}, Election: {ElectionId}, VoteId: {VoteId}, Receipt: {ReceiptToken}",
                voterId, voteDto.ElectionId, voteId, receipt.ReceiptToken);

            return ApiResponse<VoteReceiptDto>.SuccessResult(receipt, "Voto registrado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote for voter {VoterId}", voterId);
            return ApiResponse<VoteReceiptDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<VoteReceiptDto>> GetVoteReceiptAsync(string receiptToken)
    {
        try
        {
            var receipt = await _receiptRepository.GetQueryable()
                .Include(r => r.Voter)
                .Include(r => r.Election)
                .FirstOrDefaultAsync(r => r.ReceiptToken == receiptToken);

            if (receipt == null)
            {
                return ApiResponse<VoteReceiptDto>.ErrorResult("Recibo não encontrado");
            }

            var receiptDto = JsonSerializer.Deserialize<VoteReceiptDto>(receipt.VoteData);

            return ApiResponse<VoteReceiptDto>.SuccessResult(receiptDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vote receipt");
            return ApiResponse<VoteReceiptDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<bool>> HasVoterVotedAsync(int voterId, int electionId)
    {
        try
        {
            var hasVoted = await _secureVoteRepository.HasVoterVotedAsync(voterId, electionId);
            return ApiResponse<bool>.SuccessResult(hasVoted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if voter has voted");
            return ApiResponse<bool>.ErrorResult("Erro interno do servidor");
        }
    }

    // DEPRECATED: Use IElectionSealService instead
    /*
    public async Task<ApiResponse<SystemSealResponseDto>> SealElectionAsync(ElectionSealDto sealDto, int adminId, string ipAddress, string userAgent)
    {
        try
        {
            var election = await _electionRepository.GetByIdAsync(sealDto.ElectionId);
            if (election == null)
            {
                return ApiResponse<SystemSealResponseDto>.ErrorResult("Eleição não encontrada");
            }

            if (election.IsSealed)
            {
                return ApiResponse<SystemSealResponseDto>.ErrorResult("Eleição já está lacrada");
            }

            // Generate system seal hash
            var systemData = await GenerateSystemDataAsync(election.Id);
            var sealHash = GenerateHash(systemData);

            // Create system seal record
            var systemSeal = new SystemSeal
            {
                SealHash = sealHash,
                SealType = "election_seal",
                ElectionId = election.Id,
                SealedAt = DateTime.UtcNow,
                SealedBy = adminId,
                SystemData = systemData,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            await _sealRepository.AddAsync(systemSeal);

            // Update election
            election.IsSealed = true;
            election.SealHash = sealHash;
            election.SealedAt = DateTime.UtcNow;
            election.SealedBy = adminId;

            await _electionRepository.UpdateAsync(election);

            await _auditService.LogAsync(adminId, "admin", "seal_election", "election", election.Id,
                $"Election '{election.Title}' sealed with hash: {sealHash}");

            var response = new SystemSealResponseDto
            {
                Success = true,
                Message = "Eleição lacrada com sucesso",
                SealHash = sealHash,
                SealedAt = systemSeal.SealedAt,
                SystemData = systemData
            };

            return ApiResponse<SystemSealResponseDto>.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sealing election");
            return ApiResponse<SystemSealResponseDto>.ErrorResult("Erro interno do servidor");
        }
    }
    */

    public async Task<ApiResponse<ZeroReportDto>> GenerateZeroReportAsync(int electionId, int adminId, string ipAddress)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Positions)
                    .ThenInclude(p => p.Candidates)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                return ApiResponse<ZeroReportDto>.ErrorResult("Eleição não encontrada");
            }

            // Check if it's within 10 minutes before election start
            var now = DateTime.UtcNow;
            var timeUntilStart = election.StartDate - now;
            if (timeUntilStart.TotalMinutes > 10 || timeUntilStart.TotalMinutes < 0)
            {
                return ApiResponse<ZeroReportDto>.ErrorResult("Relatório de zeresima só pode ser gerado até 10 minutos antes do início da eleição");
            }

            // Check if already exists
            var existingReport = await _zeroReportRepository.GetQueryable()
                .FirstOrDefaultAsync(zr => zr.ElectionId == electionId);

            if (existingReport != null)
            {
                return ApiResponse<ZeroReportDto>.ErrorResult("Relatório de zeresima já foi gerado para esta eleição");
            }

            var totalVoters = await _voterRepository.GetQueryable()
                .Where(v => v.IsActive)
                .CountAsync();

            var totalVotes = await _voteRepository.GetQueryable()
                .Where(v => v.ElectionId == electionId)
                .CountAsync();

            var report = new ZeroReportDto
            {
                ElectionId = election.Id,
                ElectionTitle = election.Title,
                GeneratedAt = now,
                GeneratedBy = "Admin", // You might want to get admin name
                TotalRegisteredVoters = totalVoters,
                TotalCandidates = election.Positions.Sum(p => p.Candidates.Count),
                TotalPositions = election.Positions.Count,
                TotalVotes = totalVotes,
                Positions = election.Positions.Select(p => new ZeroReportPositionDto
                {
                    PositionName = p.Title,
                    TotalCandidates = p.Candidates.Count,
                    TotalVotes = 0, // Should be 0 for zero report
                    Candidates = p.Candidates.Select(c => new ZeroReportCandidateDto
                    {
                        CandidateName = c.Name,
                        CandidateNumber = c.Number,
                        VoteCount = 0 // Should be 0 for zero report
                    }).ToList()
                }).ToList()
            };

            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            var reportHash = GenerateHash(reportJson);

            var zeroReport = new ZeroReport
            {
                ElectionId = electionId,
                GeneratedAt = now,
                GeneratedBy = adminId,
                ReportData = reportJson,
                ReportHash = reportHash,
                TotalRegisteredVoters = totalVoters,
                TotalCandidates = report.TotalCandidates,
                TotalPositions = report.TotalPositions,
                TotalVotes = totalVotes,
                IpAddress = ipAddress
            };

            await _zeroReportRepository.AddAsync(zeroReport);

            report.ReportHash = reportHash;

            await _auditService.LogAsync(adminId, "admin", "generate_zero_report", "zero_report", zeroReport.Id,
                $"Zero report generated for election '{election.Title}'");

            return ApiResponse<ZeroReportDto>.SuccessResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating zero report");
            return ApiResponse<ZeroReportDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<IntegrityReportDto>> ValidateElectionIntegrityAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetByIdAsync(electionId);
            if (election == null)
            {
                return ApiResponse<IntegrityReportDto>.ErrorResult("Eleição não encontrada");
            }

            if (!election.IsSealed)
            {
                return ApiResponse<IntegrityReportDto>.ErrorResult("Eleição não está lacrada");
            }

            var originalSeal = await _sealRepository.GetQueryable()
                .Where(ss => ss.ElectionId == electionId && ss.SealType == "election_seal")
                .OrderByDescending(ss => ss.SealedAt)
                .FirstOrDefaultAsync();

            if (originalSeal == null)
            {
                return ApiResponse<IntegrityReportDto>.ErrorResult("Lacre original não encontrado");
            }

            // Regenerate system data and compare
            var currentSystemData = await GenerateSystemDataAsync(electionId);
            var currentHash = GenerateHash(currentSystemData);

            var integrityValid = originalSeal.SealHash == currentHash;

            var report = new IntegrityReportDto
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                OriginalSealHash = originalSeal.SealHash,
                CurrentSystemHash = currentHash,
                IntegrityValid = integrityValid,
                ReportGeneratedAt = DateTime.UtcNow,
                Message = integrityValid ? "Integridade da eleição confirmada" : "ATENÇÃO: Integridade da eleição comprometida",
                ValidationDetails = new List<string>
                {
                    $"Lacre original: {originalSeal.SealHash}",
                    $"Hash atual: {currentHash}",
                    $"Data do lacre: {originalSeal.SealedAt:yyyy-MM-dd HH:mm:ss}",
                    $"Status: {(integrityValid ? "VÁLIDO" : "INVÁLIDO")}"
                }
            };

            await _auditService.LogAsync(null, "system", "validate_integrity", "election", electionId,
                $"Integrity validation for election '{election.Title}': {(integrityValid ? "VALID" : "INVALID")}");

            return ApiResponse<IntegrityReportDto>.SuccessResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating election integrity");
            return ApiResponse<IntegrityReportDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<bool>> CanVoteInElectionAsync(int voterId, int electionId)
    {
        try
        {
            var voter = await _voterRepository.GetByIdAsync(voterId);
            if (voter == null || !voter.IsActive)
            {
                return ApiResponse<bool>.ErrorResult("Voter inativo ou não encontrado");
            }

            var election = await _electionRepository.GetByIdAsync(electionId);
            if (election == null)
            {
                return ApiResponse<bool>.ErrorResult("Eleição não encontrada");
            }

            var now = DateTime.UtcNow;

            // Só permite votação em eleições seladas
            if (!election.IsSealed)
            {
                return ApiResponse<bool>.ErrorResult("Eleição não está lacrada para receber votos");
            }

            if (now < election.StartDate)
            {
                return ApiResponse<bool>.ErrorResult("Eleição ainda não iniciou");
            }

            if (now > election.EndDate)
            {
                return ApiResponse<bool>.ErrorResult("Eleição já encerrou");
            }

            return ApiResponse<bool>.SuccessResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if voter can vote");
            return ApiResponse<bool>.ErrorResult("Erro interno do servidor");
        }
    }

    private async Task<VoteReceiptDto> GenerateVoteReceiptAsync(SecureVote vote, Voter voter, Election election, Position position, Candidate? candidate)
    {
        var receiptToken = Guid.NewGuid().ToString("N").ToUpper();
        var receiptHash = GenerateReceiptHash(vote.VoteId, receiptToken, vote.VotedAt);

        var receipt = new VoteReceiptDto
        {
            ReceiptToken = receiptToken,
            VoteHash = receiptHash,
            VotedAt = vote.VotedAt,
            ElectionId = election.Id,
            ElectionTitle = election.Title,
            VoterName = voter.Name,
            VoterCpf = MaskCpf(voter.Cpf),
            VoteDetails = new List<VoteDetailDto>
            {
                new VoteDetailDto
                {
                    PositionName = position.Title,
                    CandidateName = candidate?.Name,
                    CandidateNumber = candidate?.Number,
                    IsBlankVote = vote.IsBlankVote,
                    IsNullVote = vote.IsNullVote
                }
            }
        };

        // Store receipt in database
        var voteReceipt = new VoteReceipt
        {
            VoterId = vote.VoterId,
            ElectionId = vote.ElectionId,
            ReceiptToken = receiptToken,
            VoteHash = receiptHash,
            VotedAt = vote.VotedAt,
            IpAddress = vote.IpAddress,
            UserAgent = vote.UserAgent,
            VoteData = JsonSerializer.Serialize(receipt)
        };

        await _receiptRepository.AddAsync(voteReceipt);

        return receipt;
    }

    private async Task<string> GenerateSystemDataAsync(int electionId)
    {
        var election = await _electionRepository.GetQueryable()
            .Include(e => e.Positions)
                .ThenInclude(p => p.Candidates)
            .FirstOrDefaultAsync(e => e.Id == electionId);

        var systemData = new
        {
            ElectionId = election.Id,
            Title = election.Title,
            StartDate = election.StartDate,
            EndDate = election.EndDate,
            Status = election.IsSealed ? "sealed" : "open",
            Positions = election.Positions.Select(p => new
            {
                p.Id,
                p.Title,
                p.MaxCandidates,
                Candidates = p.Candidates.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Number
                }).OrderBy(c => c.Id)
            }).OrderBy(p => p.Id),
            GeneratedAt = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(systemData, new JsonSerializerOptions { WriteIndented = false });
    }

    private string GenerateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private string GenerateReceiptHash(string voteId, string receiptToken, DateTime votedAt)
    {
        var voteData = $"{voteId}-{receiptToken}-{votedAt:yyyy-MM-dd HH:mm:ss.fff}";
        return GenerateHash(voteData);
    }

    private string GetVoteType(VotingCastVoteDto voteDto)
    {
        if (voteDto.IsBlankVote) return "blank";
        if (voteDto.IsNullVote) return "null";
        return "candidate";
    }

    private string GetElectionStatusMessage(Election election, bool canVote, bool hasVoted)
    {
        if (hasVoted) return "Você já votou nesta eleição";
        if (!election.IsSealed) return "Eleição não está lacrada para receber votos";
        if (!canVote) return "Não é possível votar nesta eleição no momento";
        if (DateTime.UtcNow < election.StartDate) return "Eleição ainda não iniciou";
        if (DateTime.UtcNow > election.EndDate) return "Eleição já encerrou";
        return "Você pode votar nesta eleição";
    }

    public async Task<ApiResponse<ElectionValidationDto>> ValidateElectionForVotingAsync(int electionId)
    {
        try
        {
            _logger.LogInformation("Validating election {ElectionId} for voting", electionId);

            var validation = new ElectionValidationDto();
            var errors = new List<string>();

            var election = await _electionRepository.GetByIdAsync(electionId);
            if (election == null)
            {
                return ApiResponse<ElectionValidationDto>.ErrorResult("Election not found");
            }

            var currentTime = _dateTimeService.UtcNow;
            
            // Converter datas para UTC para comparação
            var startDateUtc = TimeZoneInfo.ConvertTimeToUtc(election.StartDate, TimeZoneInfo.FindSystemTimeZoneById(election.Timezone));
            var endDateUtc = TimeZoneInfo.ConvertTimeToUtc(election.EndDate, TimeZoneInfo.FindSystemTimeZoneById(election.Timezone));

            validation.Status = election.IsSealed ? "sealed" : "open";
            validation.IsSealed = election.IsSealed;
            validation.StartDate = election.StartDate;
            validation.EndDate = election.EndDate;
            validation.IsInVotingPeriod = currentTime >= startDateUtc && currentTime <= endDateUtc;
            validation.IsActive = !election.IsSealed;

            // Regra 1: Eleição deve estar lacrada (sealed)
            if (!election.IsSealed)
            {
                errors.Add("Election must be sealed before voting can begin");
            }

            // Regra 2: Eleição deve estar ativa
            if (election.IsSealed)
            {
                errors.Add("Election is sealed and cannot accept votes");
            }

            // Regra 3: Deve estar dentro do período de votação
            if (currentTime < startDateUtc)
            {
                errors.Add($"Voting has not started yet. Starts at: {election.StartDate:yyyy-MM-dd HH:mm:ss} {election.Timezone}");
            }
            else if (currentTime > endDateUtc)
            {
                errors.Add($"Voting has ended. Ended at: {election.EndDate:yyyy-MM-dd HH:mm:ss} {election.Timezone}");
            }

            validation.ValidationErrors = errors;
            validation.IsValid = errors.Count == 0;
            validation.ValidationMessage = validation.IsValid 
                ? "Election is valid for voting" 
                : string.Join("; ", errors);

            await _auditService.LogAsync(null, "system", "validate_election_for_voting", 
                "voting_service", electionId, $"Election validation result: {validation.IsValid}");

            return ApiResponse<ElectionValidationDto>.SuccessResult(validation, "Election validation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating election {ElectionId} for voting", electionId);
            await _auditService.LogAsync(null, "system", "validate_election_error", 
                "voting_service", electionId, $"Error validating election: {ex.Message}");
            return ApiResponse<ElectionValidationDto>.ErrorResult("Internal server error while validating election");
        }
    }

    private string MaskCpf(string cpf)
    {
        if (cpf.Length != 11) return cpf;
        return $"{cpf.Substring(0, 3)}.***.**{cpf.Substring(9, 2)}";
    }
}