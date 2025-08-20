using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface ILogService
{
    Task<PagedResult<AuditLogResponseDto>> GetLogsAsync(AuditLogFilterDto filter);
    Task<AuditLogResponseDto?> GetLogByIdAsync(int id);
    Task<AuditStatisticsDto> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<SecurityReportDto> GenerateSecurityReportAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<AuditLogResponseDto>> GetUserActivityAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<AuditLogResponseDto>> GetEntityHistoryAsync(string entityType, int entityId);
    Task<byte[]> ExportLogsAsync(ExportRequestDto exportRequest);
    Task<bool> CleanupOldLogsAsync(int retentionDays = 365);
    Task<IEnumerable<SuspiciousActivityDto>> DetectSuspiciousActivityAsync(DateTime? startDate = null, DateTime? endDate = null);
}