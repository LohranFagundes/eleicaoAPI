using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class LogService : ILogService
{
    private readonly IRepository<AuditLog> _auditRepository;
    private readonly IRepository<Admin> _adminRepository;
    private readonly IRepository<Voter> _voterRepository;

    public LogService(
        IRepository<AuditLog> auditRepository,
        IRepository<Admin> adminRepository,
        IRepository<Voter> voterRepository)
    {
        _auditRepository = auditRepository;
        _adminRepository = adminRepository;
        _voterRepository = voterRepository;
    }

    public async Task<PagedResult<AuditLogResponseDto>> GetLogsAsync(AuditLogFilterDto filter)
    {
        var query = _auditRepository.GetQueryable();

        // Apply filters
        if (filter.StartDate.HasValue)
            query = query.Where(l => l.LoggedAt >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(l => l.LoggedAt <= filter.EndDate.Value);

        if (!string.IsNullOrEmpty(filter.UserType))
            query = query.Where(l => l.UserType == filter.UserType);

        if (!string.IsNullOrEmpty(filter.Action))
            query = query.Where(l => l.Action.Contains(filter.Action));

        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(l => l.EntityType == filter.EntityType);

        if (filter.UserId.HasValue)
            query = query.Where(l => l.UserId == filter.UserId.Value);

        if (!string.IsNullOrEmpty(filter.IpAddress))
            query = query.Where(l => l.IpAddress == filter.IpAddress);

        query = query.OrderByDescending(l => l.LoggedAt);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((filter.Page - 1) * filter.Limit)
            .Take(filter.Limit)
            .ToListAsync();

        var mappedItems = new List<AuditLogResponseDto>();
        foreach (var item in items)
        {
            mappedItems.Add(await MapToResponseDto(item));
        }

        return new PagedResult<AuditLogResponseDto>
        {
            Items = mappedItems,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / filter.Limit),
            CurrentPage = filter.Page,
            HasNextPage = filter.Page * filter.Limit < totalItems,
            HasPreviousPage = filter.Page > 1
        };
    }

    public async Task<AuditLogResponseDto?> GetLogByIdAsync(int id)
    {
        var log = await _auditRepository.GetByIdAsync(id);
        return log != null ? await MapToResponseDto(log) : null;
    }

    public async Task<AuditStatisticsDto> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _auditRepository.GetQueryable();

        if (startDate.HasValue)
            query = query.Where(l => l.LoggedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.LoggedAt <= endDate.Value);

        var logs = await query.ToListAsync();
        var now = DateTime.UtcNow;

        var statistics = new AuditStatisticsDto
        {
            TotalLogs = logs.Count,
            LogsToday = logs.Count(l => l.LoggedAt.Date == now.Date),
            LogsThisWeek = logs.Count(l => l.LoggedAt >= now.AddDays(-7)),
            LogsThisMonth = logs.Count(l => l.LoggedAt >= now.AddMonths(-1)),
            ActionCounts = logs.GroupBy(l => l.Action)
                              .ToDictionary(g => g.Key, g => g.Count()),
            EntityTypeCounts = logs.GroupBy(l => l.EntityType)
                                  .ToDictionary(g => g.Key, g => g.Count()),
            UserTypeCounts = logs.GroupBy(l => l.UserType)
                                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Top users activity
        var userActivities = logs.Where(l => l.UserId.HasValue)
                                .GroupBy(l => l.UserId.Value)
                                .OrderByDescending(g => g.Count())
                                .Take(10)
                                .ToList();

        var topUsers = new List<TopUserActivityDto>();
        foreach (var userActivity in userActivities)
        {
            var userName = await GetUserNameAsync(userActivity.First().UserType, userActivity.Key);
            topUsers.Add(new TopUserActivityDto
            {
                UserId = userActivity.Key,
                UserName = userName,
                UserType = userActivity.First().UserType,
                ActivityCount = userActivity.Count()
            });
        }
        statistics.TopUsers = topUsers;

        // Daily activity for last 30 days
        var dailyActivity = logs.Where(l => l.LoggedAt >= now.AddDays(-30))
                               .GroupBy(l => l.LoggedAt.Date)
                               .Select(g => new DailyActivityDto
                               {
                                   Date = g.Key,
                                   LogCount = g.Count(),
                                   ActionBreakdown = g.GroupBy(x => x.Action)
                                                   .ToDictionary(x => x.Key, x => x.Count())
                               })
                               .OrderBy(d => d.Date);

        statistics.DailyActivity = dailyActivity;

        return statistics;
    }

    public async Task<SecurityReportDto> GenerateSecurityReportAsync(DateTime startDate, DateTime endDate)
    {
        var logs = await _auditRepository.GetQueryable()
            .Where(l => l.LoggedAt >= startDate && l.LoggedAt <= endDate)
            .ToListAsync();

        var loginLogs = logs.Where(l => l.Action.Contains("login")).ToList();
        
        var report = new SecurityReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = startDate,
            PeriodEnd = endDate,
            TotalLoginAttempts = loginLogs.Count,
            SuccessfulLogins = loginLogs.Count(l => l.Action == "login_success"),
            FailedLogins = loginLogs.Count(l => l.Action == "login_failed"),
            UniqueUsers = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId.Value).Distinct().Count(),
            AdminActions = logs.Count(l => l.UserType == "admin"),
            TopIpAddresses = logs.Where(l => !string.IsNullOrEmpty(l.IpAddress))
                                .GroupBy(l => l.IpAddress)
                                .OrderByDescending(g => g.Count())
                                .Take(10)
                                .Select(g => $"{g.Key} ({g.Count()} activities)")
        };

        // Detect suspicious activities
        report.SuspiciousActivities = await DetectSuspiciousActivityAsync(startDate, endDate);

        return report;
    }

    public async Task<IEnumerable<AuditLogResponseDto>> GetUserActivityAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _auditRepository.GetQueryable()
            .Where(l => l.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(l => l.LoggedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.LoggedAt <= endDate.Value);

        var logs = await query.OrderByDescending(l => l.LoggedAt).ToListAsync();

        var result = new List<AuditLogResponseDto>();
        foreach (var log in logs)
        {
            result.Add(await MapToResponseDto(log));
        }

        return result;
    }

    public async Task<IEnumerable<AuditLogResponseDto>> GetEntityHistoryAsync(string entityType, int entityId)
    {
        var logs = await _auditRepository.GetQueryable()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.LoggedAt)
            .ToListAsync();

        var result = new List<AuditLogResponseDto>();
        foreach (var log in logs)
        {
            result.Add(await MapToResponseDto(log));
        }

        return result;
    }

    public async Task<byte[]> ExportLogsAsync(ExportRequestDto exportRequest)
    {
        var filter = new AuditLogFilterDto
        {
            StartDate = exportRequest.StartDate,
            EndDate = exportRequest.EndDate,
            UserType = exportRequest.UserType,
            Action = exportRequest.Action,
            EntityType = exportRequest.EntityType,
            UserId = exportRequest.UserId,
            Limit = int.MaxValue
        };

        var logs = await GetLogsAsync(filter);

        switch (exportRequest.Format.ToLower())
        {
            case "csv":
                return ExportToCsv(logs.Items, exportRequest.IncludeDetails);
            case "json":
                return ExportToJson(logs.Items);
            default:
                return ExportToCsv(logs.Items, exportRequest.IncludeDetails);
        }
    }

    public async Task<bool> CleanupOldLogsAsync(int retentionDays = 365)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var oldLogs = await _auditRepository.GetQueryable()
            .Where(l => l.LoggedAt < cutoffDate)
            .ToListAsync();

        if (oldLogs.Any())
        {
            await _auditRepository.DeleteRangeAsync(oldLogs);
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<SuspiciousActivityDto>> DetectSuspiciousActivityAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _auditRepository.GetQueryable();

        if (startDate.HasValue)
            query = query.Where(l => l.LoggedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.LoggedAt <= endDate.Value);

        var logs = await query.ToListAsync();
        var suspicious = new List<SuspiciousActivityDto>();

        // Multiple failed login attempts from same IP
        var failedLogins = logs.Where(l => l.Action == "login_failed" && !string.IsNullOrEmpty(l.IpAddress))
                              .GroupBy(l => l.IpAddress)
                              .Where(g => g.Count() > 5);

        foreach (var group in failedLogins)
        {
            suspicious.Add(new SuspiciousActivityDto
            {
                IpAddress = group.Key,
                ActivityType = "Multiple Failed Logins",
                Count = group.Count(),
                FirstOccurrence = group.Min(x => x.LoggedAt),
                LastOccurrence = group.Max(x => x.LoggedAt),
                Reason = $"More than 5 failed login attempts from IP {group.Key}"
            });
        }

        // High volume of actions from single user
        var highVolumeUsers = logs.Where(l => l.UserId.HasValue)
                                 .GroupBy(l => l.UserId.Value)
                                 .Where(g => g.Count() > 100);

        foreach (var group in highVolumeUsers)
        {
            var userName = await GetUserNameAsync(group.First().UserType, group.Key);
            suspicious.Add(new SuspiciousActivityDto
            {
                UserId = group.Key,
                UserName = userName,
                ActivityType = "High Volume Activity",
                Count = group.Count(),
                FirstOccurrence = group.Min(x => x.LoggedAt),
                LastOccurrence = group.Max(x => x.LoggedAt),
                Reason = $"User performed more than 100 actions in the period"
            });
        }

        return suspicious.OrderByDescending(s => s.Count);
    }

    private async Task<AuditLogResponseDto> MapToResponseDto(AuditLog log)
    {
        var userName = log.UserId.HasValue ? await GetUserNameAsync(log.UserType, log.UserId.Value) : null;
        var entityName = log.EntityId.HasValue ? await GetEntityNameAsync(log.EntityType, log.EntityId.Value) : null;

        return new AuditLogResponseDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = userName,
            UserType = log.UserType,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            EntityName = entityName,
            Details = log.Details,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            LoggedAt = log.LoggedAt,
            CreatedAt = log.CreatedAt
        };
    }

    private async Task<string?> GetUserNameAsync(string userType, int userId)
    {
        switch (userType.ToLower())
        {
            case "admin":
                var admin = await _adminRepository.GetByIdAsync(userId);
                return admin?.Name;
            case "voter":
                var voter = await _voterRepository.GetByIdAsync(userId);
                return voter?.Name;
            default:
                return null;
        }
    }

    private async Task<string?> GetEntityNameAsync(string entityType, int entityId)
    {
        // This could be expanded to get names from different entities
        // For now, just return the entity type and ID
        return $"{entityType}#{entityId}";
    }

    private byte[] ExportToCsv(IEnumerable<AuditLogResponseDto> logs, bool includeDetails)
    {
        var csv = new StringBuilder();
        
        // Header
        if (includeDetails)
        {
            csv.AppendLine("Id,DateTime,User,UserType,Action,EntityType,EntityId,EntityName,Details,IpAddress,UserAgent");
        }
        else
        {
            csv.AppendLine("Id,DateTime,User,UserType,Action,EntityType,EntityId,IpAddress");
        }

        // Data
        foreach (var log in logs)
        {
            var line = $"{log.Id},{log.LoggedAt:yyyy-MM-dd HH:mm:ss},{log.UserName ?? "N/A"},{log.UserType},{log.Action},{log.EntityType},{log.EntityId ?? 0},{log.EntityName ?? "N/A"}";
            
            if (includeDetails)
            {
                line += $",\"{log.Details?.Replace("\"", "\"\"") ?? ""}\",{log.IpAddress ?? "N/A"},\"{log.UserAgent?.Replace("\"", "\"\"") ?? "N/A"}\"";
            }
            else
            {
                line += $",{log.IpAddress ?? "N/A"}";
            }
            
            csv.AppendLine(line);
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private byte[] ExportToJson(IEnumerable<AuditLogResponseDto> logs)
    {
        var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Encoding.UTF8.GetBytes(json);
    }
}