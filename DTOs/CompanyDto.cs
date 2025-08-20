using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CompanyCreateDto
{
    [Required(ErrorMessage = "Nome fantasia é obrigatório")]
    [StringLength(200, ErrorMessage = "Nome fantasia deve ter no máximo 200 caracteres")]
    public string NomeFantasia { get; set; } = string.Empty;

    [Required(ErrorMessage = "Razão social é obrigatória")]
    [StringLength(200, ErrorMessage = "Razão social deve ter no máximo 200 caracteres")]
    public string RazaoSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "CNPJ é obrigatório")]
    [StringLength(18, ErrorMessage = "CNPJ deve ter no máximo 18 caracteres")]
    [RegularExpression(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$", ErrorMessage = "CNPJ deve estar no formato XX.XXX.XXX/XXXX-XX")]
    public string Cnpj { get; set; } = string.Empty;

    [Required(ErrorMessage = "CEP é obrigatório")]
    [StringLength(10, ErrorMessage = "CEP deve ter no máximo 10 caracteres")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP deve estar no formato XXXXX-XXX")]
    public string Cep { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bairro é obrigatório")]
    [StringLength(100, ErrorMessage = "Bairro deve ter no máximo 100 caracteres")]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Logradouro é obrigatório")]
    [StringLength(200, ErrorMessage = "Logradouro deve ter no máximo 200 caracteres")]
    public string Logradouro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Número é obrigatório")]
    [StringLength(10, ErrorMessage = "Número deve ter no máximo 10 caracteres")]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cidade é obrigatória")]
    [StringLength(100, ErrorMessage = "Cidade deve ter no máximo 100 caracteres")]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "País é obrigatório")]
    [StringLength(100, ErrorMessage = "País deve ter no máximo 100 caracteres")]
    public string Pais { get; set; } = string.Empty;
}

public class CompanyUpdateDto
{
    [StringLength(200, ErrorMessage = "Nome fantasia deve ter no máximo 200 caracteres")]
    public string? NomeFantasia { get; set; }

    [StringLength(200, ErrorMessage = "Razão social deve ter no máximo 200 caracteres")]
    public string? RazaoSocial { get; set; }

    [StringLength(18, ErrorMessage = "CNPJ deve ter no máximo 18 caracteres")]
    [RegularExpression(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$", ErrorMessage = "CNPJ deve estar no formato XX.XXX.XXX/XXXX-XX")]
    public string? Cnpj { get; set; }

    [StringLength(10, ErrorMessage = "CEP deve ter no máximo 10 caracteres")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP deve estar no formato XXXXX-XXX")]
    public string? Cep { get; set; }

    [StringLength(100, ErrorMessage = "Bairro deve ter no máximo 100 caracteres")]
    public string? Bairro { get; set; }

    [StringLength(200, ErrorMessage = "Logradouro deve ter no máximo 200 caracteres")]
    public string? Logradouro { get; set; }

    [StringLength(10, ErrorMessage = "Número deve ter no máximo 10 caracteres")]
    [RegularExpression(@"^\d{1,10}$", ErrorMessage = "Número deve conter apenas dígitos")]
    public string? Numero { get; set; }

    [StringLength(100, ErrorMessage = "Cidade deve ter no máximo 100 caracteres")]
    public string? Cidade { get; set; }

    [StringLength(100, ErrorMessage = "País deve ter no máximo 100 caracteres")]
    public string? Pais { get; set; }

    public bool? IsActive { get; set; }
}

public class CompanyResponseDto
{
    public int Id { get; set; }
    public string NomeFantasia { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Logradouro { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}