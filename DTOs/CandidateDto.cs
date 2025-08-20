using System.ComponentModel.DataAnnotations;

namespace ElectionApi.Net.DTOs;

public class CreateCandidateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Number { get; set; }

    public string? Description { get; set; }

    public string? Biography { get; set; }

    [StringLength(255)]
    [Url]
    public string? PhotoUrl { get; set; }

    [Range(1, 100)]
    public int OrderPosition { get; set; } = 1;

    [Required]
    public int PositionId { get; set; }
}

public class UpdateCandidateDto
{
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(20)]
    public string? Number { get; set; }

    public string? Description { get; set; }

    public string? Biography { get; set; }

    [StringLength(255)]
    [Url]
    public string? PhotoUrl { get; set; }

    // BLOB Photo Support - Sistema Híbrido
    public byte[]? PhotoData { get; set; }
    
    [StringLength(100)]
    public string? PhotoMimeType { get; set; }
    
    [StringLength(255)]
    public string? PhotoFileName { get; set; }

    [Range(1, 100)]
    public int? OrderPosition { get; set; }

    public bool? IsActive { get; set; }
}

public class CandidateResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? Description { get; set; }
    public string? Biography { get; set; }
    public string? PhotoUrl { get; set; }
    
    // Informações sobre armazenamento da foto - Sistema Híbrido
    public bool HasPhoto { get; set; }
    public bool HasPhotoFile { get; set; }
    public bool HasPhotoBlob { get; set; }
    public string? PhotoStorageType { get; set; } // "file", "blob", "both", "none"
    public string? PhotoMimeType { get; set; }
    public string? PhotoFileName { get; set; }
    
    public int OrderPosition { get; set; }
    public bool IsActive { get; set; }
    public int PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int VotesCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CandidateWithVotesDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public int PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public decimal VotePercentage { get; set; }
}