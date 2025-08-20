using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;
using System.Security.Cryptography;
using System.Text;

namespace ElectionApi.Net.Services;

public class ElectionSealService : IElectionSealService
{
    private readonly IRepository<Election> _electionRepository;
    private readonly IRepository<Admin> _adminRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<ElectionSealService> _logger;

    public ElectionSealService(
        IRepository<Election> electionRepository,
        IRepository<Admin> adminRepository,
        IAuditService auditService,
        ILogger<ElectionSealService> logger)
    {
        _electionRepository = electionRepository;
        _adminRepository = adminRepository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ApiResponse<ElectionSealResponseDto>> SealElectionAsync(int electionId, int sealedBy)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Company)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                return ApiResponse<ElectionSealResponseDto>.ErrorResult("Eleição não encontrada");
            }

            // Verificar se já está lacrada
            if (election.IsSealed)
            {
                return ApiResponse<ElectionSealResponseDto>.ErrorResult("Esta eleição já está lacrada");
            }

            // Verificar se o admin existe
            var admin = await _adminRepository.GetByIdAsync(sealedBy);
            if (admin == null)
            {
                return ApiResponse<ElectionSealResponseDto>.ErrorResult("Administrador não encontrado");
            }

            // Gerar hash único para o lacre
            var sealHash = GenerateElectionSealHash(election);

            // Atualizar eleição - o trigger do banco atualizará automaticamente is_sealed e sealed_at
            election.SealHash = sealHash;
            election.SealedBy = sealedBy;
            election.SealedAt = DateTime.UtcNow;
            election.IsSealed = true;
            election.UpdatedBy = sealedBy;

            await _electionRepository.UpdateAsync(election);

            // Log da ação
            await _auditService.LogAsync(sealedBy, "admin", "seal", "elections", electionId, 
                $"Eleição '{election.Title}' foi lacrada com hash: {sealHash}");

            _logger.LogInformation("Eleição {ElectionId} lacrada por admin {SealedBy} com hash {SealHash}", 
                electionId, sealedBy, sealHash);

            var response = new ElectionSealResponseDto
            {
                ElectionId = election.Id,
                ElectionTitle = election.Title,
                SealHash = sealHash,
                SealedAt = election.SealedAt!.Value,
                SealedBy = sealedBy,
                SealedByName = admin.Name,
                IsSealed = true
            };

            return ApiResponse<ElectionSealResponseDto>.SuccessResult(response, "Eleição lacrada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao lacrar eleição {ElectionId}", electionId);
            return ApiResponse<ElectionSealResponseDto>.ErrorResult("Erro interno ao lacrar eleição");
        }
    }

    public async Task<ApiResponse<bool>> ValidateElectionSealAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Company)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                return ApiResponse<bool>.ErrorResult("Eleição não encontrada");
            }

            if (!election.IsSealed || string.IsNullOrEmpty(election.SealHash))
            {
                return ApiResponse<bool>.SuccessResult(false, "Eleição não está lacrada");
            }

            // Regenerar hash esperado
            var expectedHash = GenerateElectionSealHash(election);
            var isValid = expectedHash == election.SealHash;

            var message = isValid ? "Lacre válido" : "Lacre inválido ou corrompido";
            return ApiResponse<bool>.SuccessResult(isValid, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar lacre da eleição {ElectionId}", electionId);
            return ApiResponse<bool>.ErrorResult("Erro interno ao validar lacre");
        }
    }

    public async Task<ApiResponse<ElectionSealStatusResponseDto>> GetElectionSealStatusAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetQueryable()
                .Include(e => e.Company)
                .FirstOrDefaultAsync(e => e.Id == electionId);

            if (election == null)
            {
                return ApiResponse<ElectionSealStatusResponseDto>.ErrorResult("Eleição não encontrada");
            }

            string? sealedByName = null;
            if (election.SealedBy.HasValue)
            {
                var admin = await _adminRepository.GetByIdAsync(election.SealedBy.Value);
                sealedByName = admin != null ? admin.Name : "Admin não encontrado";
            }

            var status = new ElectionSealStatusResponseDto
            {
                ElectionId = election.Id,
                ElectionTitle = election.Title,
                IsSealed = election.IsSealed,
                SealHash = election.SealHash,
                SealedAt = election.SealedAt,
                SealedBy = election.SealedBy,
                SealedByName = sealedByName,
                Status = election.IsSealed ? "Lacrada" : "Não lacrada",
                CanBeSealed = !election.IsSealed,
                ValidationMessage = election.IsSealed ? 
                    $"Eleição lacrada em {election.SealedAt:dd/MM/yyyy HH:mm:ss}" : 
                    "Eleição disponível para lacre"
            };

            return ApiResponse<ElectionSealStatusResponseDto>.SuccessResult(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status do lacre da eleição {ElectionId}", electionId);
            return ApiResponse<ElectionSealStatusResponseDto>.ErrorResult("Erro interno ao obter status do lacre");
        }
    }

    public async Task<bool> IsElectionSealedAsync(int electionId)
    {
        try
        {
            var election = await _electionRepository.GetByIdAsync(electionId);
            return election?.IsSealed ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar se eleição {ElectionId} está lacrada", electionId);
            return false;
        }
    }

    private string GenerateElectionSealHash(Election election)
    {
        // Criar hash baseado em dados imutáveis da eleição
        var dataToHash = new StringBuilder()
            .Append(election.Id)
            .Append("|")
            .Append(election.Title)
            .Append("|")
            .Append(election.StartDate.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("|")
            .Append(election.EndDate.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("|")
            .Append(election.CompanyId)
            .Append("|")
            .Append(election.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append("|")
            .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            .ToString();

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
        return Convert.ToHexString(hashBytes);
    }
}