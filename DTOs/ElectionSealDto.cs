namespace ElectionApi.Net.DTOs;

public class ElectionSealResponseDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public string SealHash { get; set; } = string.Empty;
    public DateTime SealedAt { get; set; }
    public int SealedBy { get; set; }
    public string SealedByName { get; set; } = string.Empty;
    public bool IsSealed { get; set; }
}

public class ElectionSealStatusResponseDto
{
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public bool IsSealed { get; set; }
    public string? SealHash { get; set; }
    public DateTime? SealedAt { get; set; }
    public int? SealedBy { get; set; }
    public string? SealedByName { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanBeSealed { get; set; }
    public string? ValidationMessage { get; set; }
}