using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CreateVoterDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(14)]
    public string Cpf { get; set; } = string.Empty;

    [Required]
    public DateTime BirthDate { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    public decimal VoteWeight { get; set; } = 1.0m;
}

public class UpdateVoterDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(128)]
    public string? Password { get; set; }

    [StringLength(14)]
    public string? Cpf { get; set; }

    public DateTime? BirthDate { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [Range(0.1, 10.0)]
    public decimal? VoteWeight { get; set; }

    public bool? IsActive { get; set; }
    public bool? IsVerified { get; set; }
}

public class VoterResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? Phone { get; set; }
    public decimal VoteWeight { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int TotalVotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VoterVerificationDto
{
    [Required]
    public string VerificationToken { get; set; } = string.Empty;
}

public class VoterStatisticsDto
{
    public int TotalVoters { get; set; }
    public int ActiveVoters { get; set; }
    public int VerifiedVoters { get; set; }
    public int VotersWhoVoted { get; set; }
    public decimal VotingPercentage { get; set; }
}