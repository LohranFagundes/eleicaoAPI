using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class Election : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(50)]
    public string ElectionType { get; set; } = "internal";


    public DateTime StartDate { get; set; }

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

    [StringLength(20)]
    public string Status { get; set; } = "draft";

    public int CreatedBy { get; set; }

    public int UpdatedBy { get; set; }

    // Seal System Properties
    public bool IsSealed { get; set; } = false;
    
    [StringLength(128)]
    public string? SealHash { get; set; }
    
    public DateTime? SealedAt { get; set; }
    
    public int? SealedBy { get; set; }

    // Company relationship - Required
    [Required]
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    // Navigation properties
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}