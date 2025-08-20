using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectionApi.Net.Models;

[Table("Companies")]
public class Company
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string NomeFantasia { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    [Required]
    [StringLength(18)]
    public string Cnpj { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Cep { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Bairro { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Logradouro { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Numero { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Cidade { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Pais { get; set; } = string.Empty;

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Election> Elections { get; set; } = new List<Election>();
}