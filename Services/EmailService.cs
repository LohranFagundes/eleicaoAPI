using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class EmailService : IEmailService
{
    private readonly IRepository<Voter> _voterRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(
        IRepository<Voter> voterRepository,
        IAuditService auditService,
        ILogger<EmailService> logger,
        IConfiguration configuration)
    {
        _voterRepository = voterRepository;
        _auditService = auditService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<EmailResponseDto> SendEmailAsync(SendEmailDto emailDto)
    {
        try
        {
            var emailId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting email send process. EmailId: {EmailId}", emailId);

            using var client = CreateSmtpClient();
            using var message = CreateMailMessage(emailDto.ToEmail, emailDto.ToName, emailDto.Subject, emailDto.Body, emailDto.IsHtml, emailDto.Attachments);

            await client.SendMailAsync(message);
            
            var sentAt = DateTime.UtcNow;
            _logger.LogInformation("Email sent successfully. EmailId: {EmailId}, To: {Email}", emailId, emailDto.ToEmail);

            await _auditService.LogAsync(null, "system", "send_email", "email", null,
                $"Email sent to {emailDto.ToEmail} with subject: {emailDto.Subject}");

            return new EmailResponseDto
            {
                Success = true,
                Message = "Email sent successfully",
                SentAt = sentAt,
                EmailId = emailId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", emailDto.ToEmail);
            
            await _auditService.LogAsync(null, "system", "send_email_failed", "email", null,
                $"Failed to send email to {emailDto.ToEmail}: {ex.Message}");

            return new EmailResponseDto
            {
                Success = false,
                Message = $"Failed to send email: {ex.Message}",
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<BulkEmailResponseDto> SendBulkEmailAsync(BulkEmailDto bulkEmailDto)
    {
        try
        {
            var bulkEmailId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting bulk email send process. BulkEmailId: {BulkEmailId}", bulkEmailId);

            var targets = await GetBulkEmailTargetsAsync(bulkEmailDto.Target);
            var totalTargets = targets.Count;
            var successfulSends = 0;
            var failedSends = 0;
            var errors = new List<string>();

            _logger.LogInformation("Found {TotalTargets} targets for bulk email", totalTargets);

            using var client = CreateSmtpClient();

            foreach (var target in targets)
            {
                try
                {
                    var personalizedBody = PersonalizeEmailContent(bulkEmailDto.Body, target.Name, target.Email);
                    
                    using var message = CreateMailMessage(target.Email, target.Name, bulkEmailDto.Subject, personalizedBody, bulkEmailDto.IsHtml, bulkEmailDto.Attachments);
                    await client.SendMailAsync(message);
                    
                    successfulSends++;
                    _logger.LogDebug("Email sent successfully to {Email}", target.Email);
                }
                catch (Exception ex)
                {
                    failedSends++;
                    var error = $"Failed to send to {target.Email}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogWarning(ex, "Failed to send bulk email to {Email}", target.Email);
                }

                // Add small delay to avoid overwhelming SMTP server
                await Task.Delay(100);
            }

            var sentAt = DateTime.UtcNow;
            var resultMessage = $"Bulk email completed. Successful: {successfulSends}, Failed: {failedSends}";
            
            _logger.LogInformation("Bulk email process completed. BulkEmailId: {BulkEmailId}, Total: {Total}, Success: {Success}, Failed: {Failed}", 
                bulkEmailId, totalTargets, successfulSends, failedSends);

            await _auditService.LogAsync(null, "system", "send_bulk_email", "bulk_email", null,
                $"Bulk email sent with subject: {bulkEmailDto.Subject}. Total: {totalTargets}, Success: {successfulSends}, Failed: {failedSends}");

            return new BulkEmailResponseDto
            {
                Success = failedSends == 0,
                Message = resultMessage,
                SentAt = sentAt,
                TotalTargets = totalTargets,
                SuccessfulSends = successfulSends,
                FailedSends = failedSends,
                Errors = errors,
                BulkEmailId = bulkEmailId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk email");
            
            await _auditService.LogAsync(null, "system", "send_bulk_email_failed", "bulk_email", null,
                $"Failed to send bulk email: {ex.Message}");

            return new BulkEmailResponseDto
            {
                Success = false,
                Message = $"Failed to send bulk email: {ex.Message}",
                SentAt = DateTime.UtcNow,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<EmailResponseDto> SendTemplateEmailAsync(string toEmail, string toName, EmailTemplateDto template)
    {
        try
        {
            var body = template.Body;
            var subject = template.Subject;

            // Replace template variables
            if (template.Variables != null)
            {
                foreach (var variable in template.Variables)
                {
                    body = body.Replace($"{{{variable.Key}}}", variable.Value);
                    subject = subject.Replace($"{{{variable.Key}}}", variable.Value);
                }
            }

            var emailDto = new SendEmailDto
            {
                ToEmail = toEmail,
                ToName = toName,
                Subject = subject,
                Body = body,
                IsHtml = true
            };

            return await SendEmailAsync(emailDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send template email to {Email}", toEmail);
            return new EmailResponseDto
            {
                Success = false,
                Message = $"Failed to send template email: {ex.Message}",
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<IEnumerable<EmailStatusDto>> GetEmailHistoryAsync(DateTime? startDate = null, DateTime? endDate = null, int limit = 50)
    {
        // This is a simplified implementation
        // In a real system, you'd store email logs in the database
        await Task.CompletedTask;
        return new List<EmailStatusDto>();
    }

    public async Task<EmailStatusDto?> GetEmailStatusAsync(string emailId)
    {
        // This is a simplified implementation
        // In a real system, you'd query email status from the database
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> ValidateEmailConfigurationAsync()
    {
        try
        {
            var smtpHost = _configuration["SMTP_HOST"] ?? _configuration["EmailSettings:SmtpHost"];
            var smtpPort = _configuration.GetValue<int>("SMTP_PORT", _configuration.GetValue<int>("EmailSettings:SmtpPort"));
            var username = _configuration["SMTP_USERNAME"] ?? _configuration["EmailSettings:Username"];
            var password = _configuration["SMTP_PASSWORD"] ?? _configuration["EmailSettings:Password"];

            if (string.IsNullOrEmpty(smtpHost) || smtpPort == 0 || 
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            // Test SMTP connection
            using var client = CreateSmtpClient();
            // Some SMTP servers don't support Connect without sending
            // This is a basic validation
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email configuration validation failed");
            return false;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        var smtpHost = _configuration["SMTP_HOST"] ?? _configuration["EmailSettings:SmtpHost"] ?? throw new InvalidOperationException("SMTP Host not configured");
        var smtpPort = _configuration.GetValue<int>("SMTP_PORT", _configuration.GetValue<int>("EmailSettings:SmtpPort", 587));
        var username = _configuration["SMTP_USERNAME"] ?? _configuration["EmailSettings:Username"] ?? throw new InvalidOperationException("SMTP Username not configured");
        var password = _configuration["SMTP_PASSWORD"] ?? _configuration["EmailSettings:Password"] ?? throw new InvalidOperationException("SMTP Password not configured");
        var enableSsl = _configuration.GetValue<bool>("SMTP_ENABLE_SSL", _configuration.GetValue<bool>("EmailSettings:EnableSsl", true));

        var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };

        return client;
    }

    private MailMessage CreateMailMessage(string toEmail, string? toName, string subject, string body, bool isHtml, List<string>? attachments = null)
    {
        var fromEmail = _configuration["SMTP_FROM_EMAIL"] ?? _configuration["EmailSettings:FromEmail"] ?? throw new InvalidOperationException("From Email not configured");
        var fromName = _configuration["SMTP_FROM_NAME"] ?? _configuration["EmailSettings:FromName"] ?? "Election System";

        var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        message.To.Add(new MailAddress(toEmail, toName ?? ""));

        // Add attachments if provided
        if (attachments != null)
        {
            foreach (var attachmentPath in attachments)
            {
                if (File.Exists(attachmentPath))
                {
                    message.Attachments.Add(new Attachment(attachmentPath));
                }
            }
        }

        return message;
    }

    private async Task<List<EmailTarget>> GetBulkEmailTargetsAsync(BulkEmailTargetDto target)
    {
        var targets = new List<EmailTarget>();

        // Get voters from database based on criteria
        if (target.SendToAllActiveVoters || target.SendToAllVerifiedVoters)
        {
            var query = _voterRepository.GetQueryable();

            if (target.SendToAllActiveVoters)
                query = query.Where(v => v.IsActive);

            if (target.SendToAllVerifiedVoters)
                query = query.Where(v => v.IsVerified);

            if (target.VotersRegisteredAfter.HasValue)
                query = query.Where(v => v.CreatedAt >= target.VotersRegisteredAfter.Value);

            if (target.VotersRegisteredBefore.HasValue)
                query = query.Where(v => v.CreatedAt <= target.VotersRegisteredBefore.Value);

            var voters = await query.ToListAsync();
            targets.AddRange(voters.Select(v => new EmailTarget { Email = v.Email, Name = v.Name }));
        }

        // Add specific voter IDs
        if (target.SpecificVoterIds != null && target.SpecificVoterIds.Any())
        {
            var specificVoters = await _voterRepository.GetQueryable()
                .Where(v => target.SpecificVoterIds.Contains(v.Id))
                .ToListAsync();
            
            targets.AddRange(specificVoters.Select(v => new EmailTarget { Email = v.Email, Name = v.Name }));
        }

        // Add specific emails
        if (target.SpecificEmails != null && target.SpecificEmails.Any())
        {
            targets.AddRange(target.SpecificEmails.Select(email => new EmailTarget { Email = email, Name = "" }));
        }

        // Remove duplicates
        return targets.GroupBy(t => t.Email.ToLower()).Select(g => g.First()).ToList();
    }

    private string PersonalizeEmailContent(string body, string name, string email)
    {
        return body
            .Replace("{NAME}", name)
            .Replace("{EMAIL}", email)
            .Replace("{VOTER_NAME}", name)
            .Replace("{VOTER_EMAIL}", email);
    }

    private class EmailTarget
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}