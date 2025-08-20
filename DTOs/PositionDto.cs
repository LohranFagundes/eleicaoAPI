using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CreatePositionDto
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(1, 100)]
    public int MaxCandidates { get; set; } = 10;

    [Range(1, 10)]
    public int MaxVotesPerVoter { get; set; } = 1;

    public bool AllowBlankVotes { get; set; } = true;

    public bool AllowNullVotes { get; set; } = false;

    [Range(1, 100)]
    public int OrderPosition { get; set; } = 1;

    [Required]
    public int ElectionId { get; set; }
}

public class UpdatePositionDto
{
    [StringLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Range(1, 100)]
    public int? MaxCandidates { get; set; }

    [Range(1, 10)]
    public int? MaxVotesPerVoter { get; set; }

    public bool? AllowBlankVotes { get; set; }

    public bool? AllowNullVotes { get; set; }

    [Range(1, 100)]
    public int? OrderPosition { get; set; }

    public bool? IsActive { get; set; }
}

public class PositionResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxCandidates { get; set; }
    public int MaxVotesPerVoter { get; set; }
    public bool AllowBlankVotes { get; set; }
    public bool AllowNullVotes { get; set; }
    public int OrderPosition { get; set; }
    public bool IsActive { get; set; }
    public int ElectionId { get; set; }
    public string ElectionTitle { get; set; } = string.Empty;
    public int CandidatesCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PositionWithCandidatesDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxCandidates { get; set; }
    public int MaxVotesPerVoter { get; set; }
    public bool AllowBlankVotes { get; set; }
    public bool AllowNullVotes { get; set; }
    public int OrderPosition { get; set; }
    public bool IsActive { get; set; }
    public int ElectionId { get; set; }
    public IEnumerable<CandidateResponseDto> Candidates { get; set; } = new List<CandidateResponseDto>();
}