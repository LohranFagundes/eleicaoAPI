namespace ElectionApi.Net.DTOs;

public class SystemSealVerificationDto
{
    public int ElectionId { get; set; }
    public string ProvidedSealHash { get; set; } = string.Empty;
    public string? StoredSealHash { get; set; }
    public string? CurrentCalculatedHash { get; set; }
    public DateTime? SealedAt { get; set; }
    public object? SystemData { get; set; }
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
}