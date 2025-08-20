using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.Models;

public class Position : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;


    public string? Description { get; set; }

    public int? MaxCandidates { get; set; } = 10;

    public int? MaxVotesPerVoter { get; set; } = 1;

    public bool AllowBlankVotes { get; set; } = true;

    public bool AllowNullVotes { get; set; } = false;

    public int? OrderPosition { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public int? ElectionId { get; set; }

    // Navigation properties
    public Election Election { get; set; } = null!;
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}