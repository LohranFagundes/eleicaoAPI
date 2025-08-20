using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CreateElectionDto
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(50)]
    public string ElectionType { get; set; } = "internal";

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [StringLength(100)]
    public string Timezone { get; set; } = "America/Sao_Paulo";

    public bool AllowBlankVotes { get; set; } = false;
    public bool AllowNullVotes { get; set; } = false;
    public bool RequireJustification { get; set; } = false;
    public int MaxVotesPerVoter { get; set; } = 1;

    [StringLength(20)]
    public string VotingMethod { get; set; } = "single_choice";

    [StringLength(20)]
    public string ResultsVisibility { get; set; } = "after_election";

    [Required]
    public int CompanyId { get; set; }
}

public class UpdateElectionDto
{
    [StringLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [StringLength(50)]
    public string? ElectionType { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [StringLength(100)]
    public string? Timezone { get; set; }

    public bool? AllowBlankVotes { get; set; }
    public bool? AllowNullVotes { get; set; }
    public bool? RequireJustification { get; set; }
    public int? MaxVotesPerVoter { get; set; }

    [StringLength(20)]
    public string? VotingMethod { get; set; }

    [StringLength(20)]
    public string? ResultsVisibility { get; set; }

    public int? CompanyId { get; set; }
}

public class ElectionResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ElectionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public bool AllowBlankVotes { get; set; }
    public bool AllowNullVotes { get; set; }
    public bool RequireJustification { get; set; }
    public int MaxVotesPerVoter { get; set; }
    public string VotingMethod { get; set; } = string.Empty;
    public string ResultsVisibility { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Company information
    public int CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyCnpj { get; set; }
}

public class UpdateElectionStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}