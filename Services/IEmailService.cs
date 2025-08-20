using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface IEmailService
{
    Task<EmailResponseDto> SendEmailAsync(SendEmailDto emailDto);
    Task<BulkEmailResponseDto> SendBulkEmailAsync(BulkEmailDto bulkEmailDto);
    Task<EmailResponseDto> SendTemplateEmailAsync(string toEmail, string toName, EmailTemplateDto template);
    Task<IEnumerable<EmailStatusDto>> GetEmailHistoryAsync(DateTime? startDate = null, DateTime? endDate = null, int limit = 50);
    Task<EmailStatusDto?> GetEmailStatusAsync(string emailId);
    Task<bool> ValidateEmailConfigurationAsync();
}