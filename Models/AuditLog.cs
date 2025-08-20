using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class AuditLog : BaseEntity
{
    public int? UserId { get; set; }

    [Required]
    [StringLength(20)]
    public string UserType { get; set; } = string.Empty; // admin, voter, system

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(255)]
    public string? UserAgent { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}