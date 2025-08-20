using ElectionApi.Net.Data;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _auditRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(IRepository<AuditLog> auditRepository, IHttpContextAccessor httpContextAccessor)
    {
        _auditRepository = auditRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(int? userId, string userType, string action, string entityType, int? entityId = null, string? details = null)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            UserType = userType,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent(),
            LoggedAt = DateTime.UtcNow
        };

        await _auditRepository.AddAsync(auditLog);
    }

    public async Task LogAsync(string userType, string action, string entityType, int? entityId = null, string? details = null)
    {
        await LogAsync(null, userType, action, entityType, entityId, details);
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? 
               httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
    }

    private string? GetUserAgent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Request.Headers["User-Agent"].FirstOrDefault();
    }
}