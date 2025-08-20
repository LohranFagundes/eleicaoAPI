using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class SendEmailDto
{
    [Required]
    [EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ToName { get; set; }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; } = true;

    public List<string>? Attachments { get; set; }
}

public class BulkEmailDto
{
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsHtml { get; set; } = true;

    public BulkEmailTargetDto Target { get; set; } = new();

    public List<string>? Attachments { get; set; }
}

public class BulkEmailTargetDto
{
    public bool SendToAllActiveVoters { get; set; } = true;
    
    public bool SendToAllVerifiedVoters { get; set; } = false;
    
    public List<int>? SpecificVoterIds { get; set; }
    
    public List<string>? SpecificEmails { get; set; }
    
    public DateTime? VotersRegisteredAfter { get; set; }
    
    public DateTime? VotersRegisteredBefore { get; set; }
}

public class EmailResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string? EmailId { get; set; }
}

public class BulkEmailResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public int TotalTargets { get; set; }
    public int SuccessfulSends { get; set; }
    public int FailedSends { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? BulkEmailId { get; set; }
}

public class EmailTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string>? Variables { get; set; }
}

public class EmailStatusDto
{
    public string EmailId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}