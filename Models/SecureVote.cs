using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectionApi.Net.Models;

[Table("secure_votes")]
public class SecureVote
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string VoteId { get; set; } = string.Empty; // UUID único para o voto

    [Required]
    [StringLength(20)]
    public string VoteType { get; set; } = "candidate"; // candidate, blank, null

    [Required]
    [StringLength(512)]
    public string EncryptedVoteData { get; set; } = string.Empty; // Dados criptografados do candidato

    [Required]
    [StringLength(128)]
    public string VoteHash { get; set; } = string.Empty; // Hash para integridade

    [Required]
    [StringLength(256)]
    public string VoteSignature { get; set; } = string.Empty; // Assinatura digital

    [Required]
    public decimal VoteWeight { get; set; } = 1.0m;

    [Required]
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string UserAgent { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string DeviceFingerprint { get; set; } = string.Empty; // Identificação do dispositivo

    // Dados não criptografados (para auditoria e contagem)
    [Required]
    public int VoterId { get; set; }

    [Required]
    public int ElectionId { get; set; }

    [Required]
    public int PositionId { get; set; }

    public bool IsBlankVote { get; set; } = false;

    public bool IsNullVote { get; set; } = false;

    [StringLength(128)]
    public string? EncryptedJustification { get; set; } // Justificativa criptografada

    // Campos de integridade e segurança
    [Required]
    [StringLength(128)]
    public string CreationHash { get; set; } = string.Empty; // Hash da criação

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsValid { get; set; } = true;

    // Campos para validação da eleição lacrada
    [Required]
    [StringLength(128)]
    public string ElectionSealHash { get; set; } = string.Empty; // Hash do lacre da eleição

    // Navigation properties (somente leitura)
    [ForeignKey("VoterId")]
    public Voter Voter { get; set; } = null!;

    [ForeignKey("ElectionId")]
    public Election Election { get; set; } = null!;

    [ForeignKey("PositionId")]
    public Position Position { get; set; } = null!;
}