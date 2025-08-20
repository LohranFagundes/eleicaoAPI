namespace ElectionApi.Net.Services;

public interface IAuditService
{
    Task LogAsync(int? userId, string userType, string action, string entityType, int? entityId = null, string? details = null);
    Task LogAsync(string userType, string action, string entityType, int? entityId = null, string? details = null);
}