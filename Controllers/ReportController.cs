using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using ElectionApi.Net.Data;
using ElectionApi.Net.Models;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ReportController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly IAuditService _auditService;

    public ReportController(ILogService logService, IAuditService auditService)
    {
        _logService = logService;
        _auditService = auditService;
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogFilterDto filter)
    {
        try
        {
            var result = await _logService.GetLogsAsync(filter);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "audit_logs", null, 
                    $"Viewed audit logs with filter: {System.Text.Json.JsonSerializer.Serialize(filter)}");
            }

            return Ok(ApiResponse<PagedResult<AuditLogResponseDto>>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch audit logs: {ex.Message}"));
        }
    }

    [HttpGet("debug-test")]
    [AllowAnonymous]  // Temporary - Remove auth for debug testing
    public IActionResult DebugTest()
    {
        return Ok(new { 
            message = "Debug endpoint is working!", 
            timestamp = DateTime.UtcNow,
            version = "1.1.1"
        });
    }

    [HttpGet("debug-audit-logs")]
    [AllowAnonymous]  // Temporary - Remove auth for debug testing
    public async Task<IActionResult> DebugAuditLogs()
    {
        try
        {
            // Test 1: Check if LogService is available
            if (_logService == null)
                return BadRequest("LogService is null");
                
            if (_auditService == null)
                return BadRequest("AuditService is null");

            // Test 2: Try simple filter first
            var simpleFilter = new AuditLogFilterDto { Page = 1, Limit = 5 };
            var simpleResult = await _logService.GetLogsAsync(simpleFilter);

            // Test 3: Check direct repository access
            var auditRepository = HttpContext.RequestServices.GetService<IRepository<AuditLog>>();
            if (auditRepository == null)
                return BadRequest("AuditLog repository not found in DI container");

            // Test 4: Check total count directly
            var totalCount = await auditRepository.CountAsync();
            
            // Test 5: Get first 5 records directly
            var directLogs = await auditRepository.GetQueryable().Take(5).ToListAsync();
            
            // Test 6: Try login filter with service
            var loginFilter = new AuditLogFilterDto { 
                Page = 1, 
                Limit = 10, 
                Action = "login" 
            };
            var loginServiceResult = await _logService.GetLogsAsync(loginFilter);
            
            return Ok(new {
                message = "Debug audit logs working",
                timestamp = DateTime.UtcNow,
                tests = new {
                    totalCountDirect = totalCount,
                    directLogsCount = directLogs.Count,
                    directLogs = directLogs.Select(l => new {
                        l.Id, l.UserId, l.UserType, l.Action, l.EntityType, l.LoggedAt
                    }),
                    simpleServiceResult = new {
                        totalItems = simpleResult.TotalItems,
                        itemsCount = simpleResult.Items.Count()
                    },
                    loginServiceResult = new {
                        totalItems = loginServiceResult.TotalItems,
                        itemsCount = loginServiceResult.Items.Count()
                    },
                    filtersUsed = new {
                        simple = simpleFilter,
                        login = loginFilter
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Debug failed: {ex.Message}\n\nStackTrace: {ex.StackTrace}"));
        }
    }

    [HttpGet("audit-logs/{id}")]
    public async Task<IActionResult> GetAuditLog(int id)
    {
        try
        {
            var log = await _logService.GetLogByIdAsync(id);
            
            if (log == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Audit log not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "audit_logs", id);
            }

            return Ok(ApiResponse<AuditLogResponseDto>.SuccessResult(log));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch audit log: {ex.Message}"));
        }
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetAuditStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var statistics = await _logService.GetAuditStatisticsAsync(startDate, endDate);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "generate", "audit_statistics", null,
                    $"Generated audit statistics for period: {startDate} - {endDate}");
            }

            return Ok(ApiResponse<AuditStatisticsDto>.SuccessResult(statistics));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to generate audit statistics: {ex.Message}"));
        }
    }

    [HttpGet("security-report")]
    public async Task<IActionResult> GenerateSecurityReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Start date and end date are required"));
            }

            if (startDate >= endDate)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Start date must be before end date"));
            }

            var report = await _logService.GenerateSecurityReportAsync(startDate, endDate);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "generate", "security_report", null,
                    $"Generated security report for period: {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}");
            }

            return Ok(ApiResponse<SecurityReportDto>.SuccessResult(report));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to generate security report: {ex.Message}"));
        }
    }

    [HttpGet("user-activity/{userId}")]
    public async Task<IActionResult> GetUserActivity(int userId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var activity = await _logService.GetUserActivityAsync(userId, startDate, endDate);
            
            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue)
            {
                await _auditService.LogAsync(currentUserId.Value, "admin", "view", "user_activity", userId,
                    $"Viewed user activity for period: {startDate} - {endDate}");
            }

            return Ok(ApiResponse<IEnumerable<AuditLogResponseDto>>.SuccessResult(activity));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch user activity: {ex.Message}"));
        }
    }

    [HttpGet("entity-history/{entityType}/{entityId}")]
    public async Task<IActionResult> GetEntityHistory(string entityType, int entityId)
    {
        try
        {
            var history = await _logService.GetEntityHistoryAsync(entityType, entityId);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "entity_history", entityId,
                    $"Viewed history for {entityType}#{entityId}");
            }

            return Ok(ApiResponse<IEnumerable<AuditLogResponseDto>>.SuccessResult(history));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch entity history: {ex.Message}"));
        }
    }

    [HttpGet("suspicious-activity")]
    public async Task<IActionResult> GetSuspiciousActivity([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            var suspicious = await _logService.DetectSuspiciousActivityAsync(startDate, endDate);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "detect", "suspicious_activity", null,
                    $"Detected suspicious activity for period: {startDate} - {endDate}");
            }

            return Ok(ApiResponse<IEnumerable<SuspiciousActivityDto>>.SuccessResult(suspicious));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to detect suspicious activity: {ex.Message}"));
        }
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportLogs([FromBody] ExportRequestDto exportRequest)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid export parameters", ModelState));
        }

        try
        {
            var data = await _logService.ExportLogsAsync(exportRequest);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "export", "audit_logs", null,
                    $"Exported audit logs in {exportRequest.Format} format");
            }

            var contentType = exportRequest.Format.ToLower() switch
            {
                "csv" => "text/csv",
                "json" => "application/json",
                _ => "text/csv"
            };

            var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{exportRequest.Format}";

            return File(data, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to export logs: {ex.Message}"));
        }
    }

    [HttpPost("cleanup-old-logs")]
    public async Task<IActionResult> CleanupOldLogs([FromQuery] int retentionDays = 365)
    {
        try
        {
            if (retentionDays < 30)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Retention period must be at least 30 days"));
            }

            var result = await _logService.CleanupOldLogsAsync(retentionDays);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "cleanup", "audit_logs", null,
                    $"Cleaned up audit logs older than {retentionDays} days. Deleted: {result}");
            }

            var message = result ? "Old logs cleaned up successfully" : "No old logs found to clean up";
            return Ok(ApiResponse<object>.SuccessResult(new { deleted = result }, message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to cleanup old logs: {ex.Message}"));
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardData()
    {
        try
        {
            var now = DateTime.UtcNow;
            var last30Days = now.AddDays(-30);
            
            var statistics = await _logService.GetAuditStatisticsAsync(last30Days, now);
            var suspicious = await _logService.DetectSuspiciousActivityAsync(last30Days, now);
            
            var dashboard = new
            {
                Statistics = statistics,
                SuspiciousActivities = suspicious.Take(5),
                Period = new { Start = last30Days, End = now }
            };

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "audit_dashboard");
            }

            return Ok(ApiResponse<object>.SuccessResult(dashboard));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to load dashboard data: {ex.Message}"));
        }
    }

    [HttpGet("real-time")]
    public async Task<IActionResult> GetRealTimeLogs([FromQuery] int limit = 20)
    {
        try
        {
            var filter = new AuditLogFilterDto
            {
                Page = 1,
                Limit = limit,
                StartDate = DateTime.UtcNow.AddHours(-1) // Last hour
            };

            var result = await _logService.GetLogsAsync(filter);
            
            return Ok(ApiResponse<IEnumerable<AuditLogResponseDto>>.SuccessResult(result.Items));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to fetch real-time logs: {ex.Message}"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}