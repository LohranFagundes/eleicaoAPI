using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class AuditLogResponseDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string UserType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime LoggedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserType { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public int? UserId { get; set; }
    public string? IpAddress { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 50;
}

public class AuditStatisticsDto
{
    public int TotalLogs { get; set; }
    public int LogsToday { get; set; }
    public int LogsThisWeek { get; set; }
    public int LogsThisMonth { get; set; }
    public Dictionary<string, int> ActionCounts { get; set; } = new();
    public Dictionary<string, int> EntityTypeCounts { get; set; } = new();
    public Dictionary<string, int> UserTypeCounts { get; set; } = new();
    public IEnumerable<TopUserActivityDto> TopUsers { get; set; } = new List<TopUserActivityDto>();
    public IEnumerable<DailyActivityDto> DailyActivity { get; set; } = new List<DailyActivityDto>();
}

public class TopUserActivityDto
{
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string UserType { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
}

public class DailyActivityDto
{
    public DateTime Date { get; set; }
    public int LogCount { get; set; }
    public Dictionary<string, int> ActionBreakdown { get; set; } = new();
}

public class SecurityReportDto
{
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalLoginAttempts { get; set; }
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public int UniqueUsers { get; set; }
    public int AdminActions { get; set; }
    public IEnumerable<SuspiciousActivityDto> SuspiciousActivities { get; set; } = new List<SuspiciousActivityDto>();
    public IEnumerable<string> TopIpAddresses { get; set; } = new List<string>();
}

public class SuspiciousActivityDto
{
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ExportRequestDto
{
    [Required]
    public string Format { get; set; } = "csv"; // csv, excel, pdf
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? UserType { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public int? UserId { get; set; }
    public bool IncludeDetails { get; set; } = true;
}