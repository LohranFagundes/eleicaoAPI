namespace ElectionApi.Net.DTOs;

public class VotingPortalCandidateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? Party { get; set; }
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoBase64 { get; set; }
    public int PositionId { get; set; }
    public string PositionName { get; set; } = string.Empty;
    public int OrderPosition { get; set; }
}

public class VotingPortalPositionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxVotes { get; set; }
    public int OrderPosition { get; set; }
    public List<VotingPortalCandidateDto> Candidates { get; set; } = new();
}

public class VotingPortalElectionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool AllowBlankVotes { get; set; }
    public bool AllowNullVotes { get; set; }
    public bool RequireJustification { get; set; }
    public int MaxVotesPerVoter { get; set; }
    public string VotingMethod { get; set; } = string.Empty;
    public List<VotingPortalPositionDto> Positions { get; set; } = new();
}

