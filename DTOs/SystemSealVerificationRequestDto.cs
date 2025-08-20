using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class SystemSealVerificationRequestDto
{
    [Required]
    public string ProvidedSealHash { get; set; } = string.Empty;
}