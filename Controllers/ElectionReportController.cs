using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using ElectionApi.Net.Models;
using ElectionApi.Net.Data;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/election-reports")]
[Authorize(Roles = "admin")]
public class ElectionReportController : ControllerBase
{
    private readonly IVotingService _votingService;
    private readonly IEmailService _emailService;
    private readonly IElectionService _electionService;
    private readonly IRepository<Admin> _adminRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<ElectionReportController> _logger;

    public ElectionReportController(
        IVotingService votingService,
        IEmailService emailService,
        IElectionService electionService,
        IRepository<Admin> adminRepository,
        IAuditService auditService,
        ILogger<ElectionReportController> logger)
    {
        _votingService = votingService;
        _emailService = emailService;
        _electionService = electionService;
        _adminRepository = adminRepository;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("{electionId}/send-zero-report")]
    public async Task<IActionResult> SendZeroReportByEmail(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Verificar se a eleição existe e está selada
            var election = await _electionService.GetElectionByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Eleição não encontrada"));
            }
            // Verificar se a eleição está ativa, agendada ou selada para gerar relatório
            if (election.Status != "active" && election.Status != "scheduled" && election.Status != "sealed")
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Eleição deve estar ativa, agendada ou selada para gerar relatório zeresima"));
            }

            // Gerar relatório zeresima
            var zeroReport = await _votingService.GenerateZeroReportAsync(electionId, adminId.Value, ipAddress);
            if (!zeroReport.Success)
            {
                return BadRequest(zeroReport);
            }

            // Obter emails dos administradores
            var admins = await _adminRepository.GetAllAsync();
            _logger.LogInformation("Found {AdminCount} total administrators in database", admins?.Count() ?? 0);
            
            if (admins == null || !admins.Any())
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Nenhum administrador encontrado para envio do relatório"));
            }

            // Log details of each admin for debugging
            foreach (var admin in admins)
            {
                _logger.LogInformation("Admin found - ID: {Id}, Name: {Name}, Email: {Email}, IsActive: {IsActive}", 
                    admin.Id, admin.Name, admin.Email, admin.IsActive);
            }

            var adminEmails = admins.Where(a => a.IsActive && !string.IsNullOrEmpty(a.Email)).Select(a => a.Email).ToList();
            _logger.LogInformation("Filtered admin emails for sending: {AdminEmails}", string.Join(", ", adminEmails));
            
            if (!adminEmails.Any())
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Nenhum email de administrador válido encontrado"));
            }

            // Gerar HTML do relatório zeresima
            var htmlContent = GenerateZeroReportHtml(election, zeroReport.Data);

            // Enviar email para todos os administradores
            var emailResult = await _emailService.SendBulkEmailAsync(new BulkEmailDto
            {
                Subject = $"📊 Relatório Zeresima - {election.Title}",
                Body = htmlContent,
                Target = new BulkEmailTargetDto
                {
                    SpecificEmails = adminEmails
                },
                IsHtml = true
            });

            // Log da ação
            await _auditService.LogAsync(adminId.Value, "admin", "send_zero_report", "election", electionId,
                $"Zero report sent by email for election {election.Title} to {adminEmails.Count} administrators");

            _logger.LogInformation("Zero report sent by email for election {ElectionId} to {AdminCount} administrators", 
                electionId, adminEmails.Count);

            return Ok(ApiResponse<object>.SuccessResult(new
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                EmailsSent = adminEmails.Count,
                Recipients = adminEmails,
                EmailResult = emailResult.Success
            }, "Relatório zeresima enviado com sucesso por email"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending zero report by email for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    [HttpPost("{electionId}/send-final-report")]
    public async Task<IActionResult> SendFinalReportByEmail(int electionId)
    {
        try
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Admin não autenticado"));
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Verificar se a eleição existe
            var election = await _electionService.GetElectionByIdAsync(electionId);
            if (election == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Eleição não encontrada"));
            }

            // Verificar se a eleição terminou
            var now = DateTime.UtcNow;
            var electionEndTime = DateTime.SpecifyKind(election.EndDate, DateTimeKind.Utc);
            
            if (now < electionEndTime)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Eleição ainda não terminou. Relatório final só pode ser enviado após o fim da eleição"));
            }

            // Gerar relatório final de contabilização
            var finalReport = await _votingService.GenerateElectionCountingReportAsync(electionId, adminId.Value, ipAddress);
            if (!finalReport.Success)
            {
                return BadRequest(finalReport);
            }

            // Obter emails dos administradores
            var admins = await _adminRepository.GetAllAsync();
            if (admins == null || !admins.Any())
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Nenhum administrador encontrado para envio do relatório"));
            }

            var adminEmails = admins.Where(a => a.IsActive && !string.IsNullOrEmpty(a.Email)).Select(a => a.Email).ToList();
            if (!adminEmails.Any())
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Nenhum email de administrador válido encontrado"));
            }

            // Gerar HTML do relatório final
            var htmlContent = GenerateFinalReportHtml(election, finalReport.Data);

            // Enviar email para todos os administradores
            var emailResult = await _emailService.SendBulkEmailAsync(new BulkEmailDto
            {
                Subject = $"🏆 Relatório Final de Eleição - {election.Title}",
                Body = htmlContent,
                Target = new BulkEmailTargetDto
                {
                    SpecificEmails = adminEmails
                },
                IsHtml = true
            });

            // Log da ação
            await _auditService.LogAsync(adminId.Value, "admin", "send_final_report", "election", electionId,
                $"Final report sent by email for election {election.Title} to {adminEmails.Count} administrators");

            _logger.LogInformation("Final report sent by email for election {ElectionId} to {AdminCount} administrators", 
                electionId, adminEmails.Count);

            return Ok(ApiResponse<object>.SuccessResult(new
            {
                ElectionId = electionId,
                ElectionTitle = election.Title,
                EmailsSent = adminEmails.Count,
                Recipients = adminEmails,
                EmailResult = emailResult.Success
            }, "Relatório final enviado com sucesso por email"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending final report by email for election {ElectionId}", electionId);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    private string GenerateZeroReportHtml(ElectionResponseDto election, ZeroReportDto report)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Relatório Zeresima</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 800px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; border-bottom: 3px solid #007bff; padding-bottom: 20px; margin-bottom: 30px; }}
        .header h1 {{ color: #007bff; margin: 0; font-size: 28px; }}
        .header h2 {{ color: #6c757d; margin: 10px 0 0 0; font-size: 18px; font-weight: normal; }}
        .election-info {{ background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 30px; }}
        .election-info h3 {{ color: #495057; margin-top: 0; }}
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 10px; }}
        .info-label {{ font-weight: bold; color: #495057; }}
        .info-value {{ color: #6c757d; }}
        .positions-section {{ margin-bottom: 30px; }}
        .position-block {{ border: 2px solid #dee2e6; border-radius: 8px; margin-bottom: 25px; padding: 20px; }}
        .position-title {{ background-color: #007bff; color: white; padding: 10px 15px; margin: -20px -20px 15px -20px; border-radius: 6px 6px 0 0; font-size: 18px; font-weight: bold; }}
        .candidates-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        .candidates-table th {{ background-color: #e9ecef; padding: 12px; text-align: left; border: 1px solid #dee2e6; font-weight: bold; }}
        .candidates-table td {{ padding: 12px; border: 1px solid #dee2e6; }}
        .candidates-table tr:nth-child(even) {{ background-color: #f8f9fa; }}
        .votes-summary {{ background-color: #d1ecf1; padding: 15px; border-radius: 8px; margin-top: 15px; border-left: 4px solid #bee5eb; }}
        .footer {{ margin-top: 40px; padding-top: 20px; border-top: 2px solid #dee2e6; text-align: center; }}
        .seal-info {{ background-color: #fff3cd; padding: 15px; border-radius: 8px; margin-top: 20px; border-left: 4px solid #ffeaa7; }}
        .seal-hash {{ font-family: monospace; word-break: break-all; background-color: #f8f9fa; padding: 10px; border-radius: 4px; font-size: 12px; }}
        .timestamp {{ color: #6c757d; font-size: 14px; }}
        .zero-votes {{ color: #28a745; font-weight: bold; }}
        .status-badge {{ padding: 4px 12px; border-radius: 15px; font-size: 12px; font-weight: bold; }}
        .status-sealed {{ background-color: #d4edda; color: #155724; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📊 RELATÓRIO ZERESIMA</h1>
            <h2>Contabilização Inicial - Antes do Início da Votação</h2>
        </div>

        <div class='election-info'>
            <h3>🗳️ Informações da Eleição</h3>
            <div class='info-row'>
                <span class='info-label'>Título:</span>
                <span class='info-value'>{election.Title}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Descrição:</span>
                <span class='info-value'>{election.Description ?? "N/A"}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Tipo:</span>
                <span class='info-value'>{election.ElectionType}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Período:</span>
                <span class='info-value'>{election.StartDate:dd/MM/yyyy HH:mm} até {election.EndDate:dd/MM/yyyy HH:mm}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Status:</span>
                <span class='info-value'><span class='status-badge status-sealed'>SELADA</span></span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Empresa:</span>
                <span class='info-value'>{election.CompanyName}</span>
            </div>
        </div>

        <div class='positions-section'>
            <h3>📋 Cargos e Candidatos</h3>";

        // Adicionar informações das posições (se disponível no report)
        if (report.Positions?.Any() == true)
        {
            foreach (var position in report.Positions)
            {
                html += $@"
            <div class='position-block'>
                <div class='position-title'>{position.PositionName}</div>
                <p><strong>Total de Candidatos:</strong> {position.TotalCandidates}</p>
                
                <table class='candidates-table'>
                    <thead>
                        <tr>
                            <th>Número</th>
                            <th>Nome do Candidato</th>
                            <th>Votos Iniciais</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                    <tbody>";

                if (position.Candidates?.Any() == true)
                {
                    foreach (var candidate in position.Candidates)
                    {
                        html += $@"
                        <tr>
                            <td><strong>{candidate.CandidateNumber ?? "N/A"}</strong></td>
                            <td>{candidate.CandidateName}</td>
                            <td><span class='zero-votes'>0 votos</span></td>
                            <td>Ativo</td>
                        </tr>";
                    }
                }
                else
                {
                    html += @"
                        <tr>
                            <td colspan='4' style='text-align: center; color: #6c757d; font-style: italic;'>Nenhum candidato cadastrado</td>
                        </tr>";
                }

                html += $@"
                    </tbody>
                </table>
                
                <div class='votes-summary'>
                    <strong>📊 Resumo Inicial do Cargo:</strong><br>
                    • Total de Candidatos: {position.TotalCandidates}<br>
                    • Votos em Branco: <span class='zero-votes'>0</span><br>
                    • Votos Nulos: <span class='zero-votes'>0</span><br>
                    • Total de Votos: <span class='zero-votes'>{position.TotalVotes}</span>
                </div>
            </div>";
            }
        }

        html += $@"
        </div>

        <div class='seal-info'>
            <h4>🔒 Informações do Lacre</h4>
            <p><strong>Hash do Relatório:</strong></p>
            <div class='seal-hash'>{report.ReportHash ?? "N/A"}</div>
            <p><strong>Hash do Sistema (Selamento):</strong></p>
            <div class='seal-hash'>{election.SealHash ?? "N/A"}</div>
            <p><strong>Data de Geração:</strong> {report.GeneratedAt:dd/MM/yyyy HH:mm:ss}</p>
            <p><strong>Gerado por:</strong> {report.GeneratedBy}</p>
            <p><em><strong>Nota de Privacidade:</strong> Este relatório apresenta apenas a contabilização por cargo e candidato, sem revelar informações individuais dos eleitores. A identidade dos votantes é protegida pelo sistema de criptografia.</em></p>
        </div>

        <div class='footer'>
            <div class='timestamp'>
                <p><strong>Relatório gerado em:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                <p><strong>Sistema:</strong> Election API v1.4.1</p>
                <p><em>Este relatório confirma que a eleição foi selada e está pronta para receber votos.</em></p>
                <p><em>Todos os contadores estão zerados conforme esperado antes do início da votação.</em></p>
            </div>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    private string GenerateFinalReportHtml(ElectionResponseDto election, ElectionCountingReportDto report)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Relatório Final de Eleição</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 800px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; border-bottom: 3px solid #28a745; padding-bottom: 20px; margin-bottom: 30px; }}
        .header h1 {{ color: #28a745; margin: 0; font-size: 28px; }}
        .header h2 {{ color: #6c757d; margin: 10px 0 0 0; font-size: 18px; font-weight: normal; }}
        .election-info {{ background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 30px; }}
        .election-info h3 {{ color: #495057; margin-top: 0; }}
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 10px; }}
        .info-label {{ font-weight: bold; color: #495057; }}
        .info-value {{ color: #6c757d; }}
        .positions-section {{ margin-bottom: 30px; }}
        .position-block {{ border: 2px solid #dee2e6; border-radius: 8px; margin-bottom: 25px; padding: 20px; }}
        .position-title {{ background-color: #28a745; color: white; padding: 10px 15px; margin: -20px -20px 15px -20px; border-radius: 6px 6px 0 0; font-size: 18px; font-weight: bold; }}
        .candidates-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
        .candidates-table th {{ background-color: #e9ecef; padding: 12px; text-align: left; border: 1px solid #dee2e6; font-weight: bold; }}
        .candidates-table td {{ padding: 12px; border: 1px solid #dee2e6; }}
        .candidates-table tr:nth-child(even) {{ background-color: #f8f9fa; }}
        .votes-summary {{ background-color: #d1f2d1; padding: 15px; border-radius: 8px; margin-top: 15px; border-left: 4px solid #28a745; }}
        .footer {{ margin-top: 40px; padding-top: 20px; border-top: 2px solid #dee2e6; text-align: center; }}
        .seal-info {{ background-color: #d1ecf1; padding: 15px; border-radius: 8px; margin-top: 20px; border-left: 4px solid #bee5eb; }}
        .seal-hash {{ font-family: monospace; word-break: break-all; background-color: #f8f9fa; padding: 10px; border-radius: 4px; font-size: 12px; }}
        .timestamp {{ color: #6c757d; font-size: 14px; }}
        .final-votes {{ color: #dc3545; font-weight: bold; }}
        .status-badge {{ padding: 4px 12px; border-radius: 15px; font-size: 12px; font-weight: bold; }}
        .status-completed {{ background-color: #d4edda; color: #155724; }}
        .winner {{ background-color: #fff3cd !important; border-left: 4px solid #ffc107; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🏆 RELATÓRIO FINAL DE ELEIÇÃO</h1>
            <h2>Contabilização Final - Resultados da Votação</h2>
        </div>

        <div class='election-info'>
            <h3>🗳️ Informações da Eleição</h3>
            <div class='info-row'>
                <span class='info-label'>Título:</span>
                <span class='info-value'>{election.Title}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Descrição:</span>
                <span class='info-value'>{election.Description ?? "N/A"}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Tipo:</span>
                <span class='info-value'>{election.ElectionType}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Período:</span>
                <span class='info-value'>{election.StartDate:dd/MM/yyyy HH:mm} até {election.EndDate:dd/MM/yyyy HH:mm}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Status:</span>
                <span class='info-value'><span class='status-badge status-completed'>FINALIZADA</span></span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Empresa:</span>
                <span class='info-value'>{election.CompanyName}</span>
            </div>
        </div>

        <div class='positions-section'>
            <h3>🏆 Resultados por Cargo</h3>";

        // Adicionar informações das posições com resultados finais
        if (report.Positions?.Any() == true)
        {
            foreach (var position in report.Positions)
            {
                html += $@"
            <div class='position-block'>
                <div class='position-title'>{position.PositionName}</div>
                <p><strong>Total de Candidatos:</strong> {position.Candidates.Count}</p>
                
                <table class='candidates-table'>
                    <thead>
                        <tr>
                            <th>Posição</th>
                            <th>Número</th>
                            <th>Nome do Candidato</th>
                            <th>Total de Votos</th>
                            <th>Percentual</th>
                        </tr>
                    </thead>
                    <tbody>";

                if (position.Candidates?.Any() == true)
                {
                    var sortedCandidates = position.Candidates.OrderByDescending(c => c.VoteCount).ToList();
                    
                    for (int i = 0; i < sortedCandidates.Count; i++)
                    {
                        var candidate = sortedCandidates[i];
                        var rowClass = i == 0 && candidate.VoteCount > 0 ? "winner" : "";
                        var positionText = i == 0 && candidate.VoteCount > 0 ? "🥇 1º" : $"{i + 1}º";

                        html += $@"
                        <tr class='{rowClass}'>
                            <td><strong>{positionText}</strong></td>
                            <td><strong>{candidate.CandidateNumber ?? "N/A"}</strong></td>
                            <td>{candidate.CandidateName}</td>
                            <td><span class='final-votes'>{candidate.VoteCount:N0}</span></td>
                            <td>{candidate.Percentage:F2}%</td>
                        </tr>";
                    }
                }
                else
                {
                    html += @"
                        <tr>
                            <td colspan='5' style='text-align: center; color: #6c757d; font-style: italic;'>Nenhum candidato cadastrado</td>
                        </tr>";
                }

                var validVotes = position.Candidates?.Sum(c => c.VoteCount) ?? 0;

                html += $@"
                    </tbody>
                </table>
                
                <div class='votes-summary'>
                    <strong>📊 Resumo Final do Cargo:</strong><br>
                    • Total de Candidatos: {position.Candidates.Count}<br>
                    • Votos Válidos: <span class='final-votes'>{validVotes:N0}</span><br>
                    • Votos em Branco: <span class='final-votes'>{position.BlankVotes:N0}</span><br>
                    • Votos Nulos: <span class='final-votes'>{position.NullVotes:N0}</span><br>
                    • <strong>Total Geral de Votos: <span class='final-votes'>{position.TotalVotes:N0}</span></strong>
                </div>
            </div>";
            }
        }

        html += $@"
        </div>

        <div class='seal-info'>
            <h4>🔒 Informações do Relatório</h4>
            <p><strong>Hash do Relatório:</strong></p>
            <div class='seal-hash'>{report.ReportHash ?? "N/A"}</div>
            <p><strong>Hash do Sistema (Selamento):</strong></p>
            <div class='seal-hash'>{report.SystemSealHash ?? "N/A"}</div>
            <p><strong>Data de Geração:</strong> {report.GeneratedAt:dd/MM/yyyy HH:mm:ss}</p>
            <p><strong>Gerado por:</strong> {report.GeneratedBy}</p>
            <p><em><strong>Nota de Privacidade:</strong> Este relatório apresenta apenas os resultados finais por cargo e candidato, preservando o sigilo do voto. A identidade individual dos eleitores é protegida pelo sistema de criptografia e não é revelada neste relatório.</em></p>
        </div>

        <div class='footer'>
            <div class='timestamp'>
                <p><strong>Relatório gerado em:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                <p><strong>Sistema:</strong> Election API v1.4.1</p>
                <p><em>Este relatório apresenta os resultados finais da eleição após o encerramento da votação.</em></p>
                <p><em>A integridade dos dados foi preservada através do sistema de lacres criptográficos.</em></p>
            </div>
        </div>
    </div>
</body>
</html>";

        return html;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}