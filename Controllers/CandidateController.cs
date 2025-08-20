using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CandidateController : ControllerBase
{
    private readonly ICandidateService _candidateService;
    private readonly IAuditService _auditService;

    public CandidateController(ICandidateService candidateService, IAuditService auditService)
    {
        _candidateService = candidateService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetCandidates([FromQuery] int page = 1, [FromQuery] int limit = 10,
        [FromQuery] int? positionId = null, [FromQuery] bool? isActive = null)
    {
        try
        {
            var result = await _candidateService.GetCandidatesAsync(page, limit, positionId, isActive);
            
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "list", "candidates");
            }

            return Ok(ApiResponse<PagedResult<CandidateResponseDto>>.SuccessResult(result));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch candidates"));
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetCandidate(int id)
    {
        try
        {
            var candidate = await _candidateService.GetCandidateByIdAsync(id);
            
            if (candidate == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "candidates", id);
            }

            return Ok(ApiResponse<CandidateResponseDto>.SuccessResult(candidate));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch candidate"));
        }
    }

    [HttpGet("position/{positionId}")]
    public async Task<IActionResult> GetCandidatesByPosition(int positionId)
    {
        try
        {
            var candidates = await _candidateService.GetCandidatesByPositionAsync(positionId);
            return Ok(ApiResponse<IEnumerable<CandidateResponseDto>>.SuccessResult(candidates));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch candidates for position"));
        }
    }

    [HttpGet("position/{positionId}/with-votes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetCandidatesWithVotes(int positionId)
    {
        try
        {
            var candidates = await _candidateService.GetCandidatesWithVotesAsync(positionId);
            return Ok(ApiResponse<IEnumerable<CandidateWithVotesDto>>.SuccessResult(candidates));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to fetch candidates with votes"));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateCandidate([FromBody] CreateCandidateDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var candidate = await _candidateService.CreateCandidateAsync(createDto, userId.Value);
            
            return CreatedAtAction(
                nameof(GetCandidate), 
                new { id = candidate.Id }, 
                ApiResponse<CandidateResponseDto>.SuccessResult(candidate, "Candidate created successfully"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to create candidate"));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateCandidate(int id, [FromBody] UpdateCandidateDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var candidate = await _candidateService.UpdateCandidateAsync(id, updateDto, userId.Value);
            
            if (candidate == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            return Ok(ApiResponse<CandidateResponseDto>.SuccessResult(candidate, "Candidate updated successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to update candidate"));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteCandidate(int id)
    {
        try
        {
            var result = await _candidateService.DeleteCandidateAsync(id);
            
            if (!result)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "delete", "candidates", id);
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Candidate deleted successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to delete candidate"));
        }
    }

    [HttpPost("{id}/upload-photo")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UploadCandidatePhoto(int id, IFormFile photo)
    {
        try
        {
            if (photo == null || photo.Length == 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("No photo file provided"));
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid file type. Only JPG, PNG and GIF are allowed"));
            }

            // Validate file size (max 5MB)
            if (photo.Length > 5 * 1024 * 1024)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("File size too large. Maximum size is 5MB"));
            }

            // Check if candidate exists
            var candidate = await _candidateService.GetCandidateByIdAsync(id);
            if (candidate == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            // Create uploads directory if it doesn't exist
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "candidates");
            Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var fileName = $"{id}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            // Update candidate with photo URL
            var photoUrl = $"/uploads/candidates/{fileName}";
            var updateDto = new UpdateCandidateDto { PhotoUrl = photoUrl };
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var updatedCandidate = await _candidateService.UpdateCandidateAsync(id, updateDto, userId.Value);

            await _auditService.LogAsync(userId.Value, "admin", "upload_photo", "candidates", id, $"Photo uploaded: {fileName}");

            return Ok(ApiResponse<object>.SuccessResult(new { photoUrl }, "Photo uploaded successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to upload photo: {ex.Message}"));
        }
    }

    [HttpPost("{id}/upload-photo-blob")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UploadCandidatePhotoBlob(int id, IFormFile photo)
    {
        try
        {
            if (photo == null || photo.Length == 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("No photo file provided"));
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Invalid file type. Only JPG, PNG, GIF and WebP are allowed"));
            }

            // Validate file size (max 10MB for BLOB)
            if (photo.Length > 10 * 1024 * 1024)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("File size too large. Maximum size is 10MB"));
            }

            // Check if candidate exists
            var candidate = await _candidateService.GetCandidateByIdAsync(id);
            if (candidate == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            // Process and optimize image using ImageSharp
            byte[] optimizedImageData;
            string finalMimeType;
            
            using var inputStream = photo.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);
            
            // Resize if image is too large (max 800x600 for profile photos)
            if (image.Width > 800 || image.Height > 600)
            {
                var targetWidth = Math.Min(800, image.Width);
                var targetHeight = Math.Min(600, image.Height);
                
                // Maintain aspect ratio
                var ratio = Math.Min((double)targetWidth / image.Width, (double)targetHeight / image.Height);
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                
                image.Mutate(x => x.Resize(newWidth, newHeight));
            }

            // Convert to JPEG for consistent format and better compression
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder
            {
                Quality = 85 // Good balance between quality and file size
            });
            
            optimizedImageData = outputStream.ToArray();
            finalMimeType = "image/jpeg";

            // Update candidate with BLOB data
            var updateDto = new UpdateCandidateDto 
            { 
                PhotoData = optimizedImageData,
                PhotoMimeType = finalMimeType,
                PhotoFileName = $"{id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg"
            };
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("User authentication required"));
            }

            var updatedCandidate = await _candidateService.UpdateCandidateAsync(id, updateDto, userId.Value);

            await _auditService.LogAsync(userId.Value, "admin", "upload_photo_blob", "candidates", id, 
                $"BLOB photo uploaded: {updateDto.PhotoFileName}, size: {optimizedImageData.Length} bytes");

            return Ok(ApiResponse<object>.SuccessResult(new { 
                message = "Photo uploaded and optimized successfully",
                fileName = updateDto.PhotoFileName,
                mimeType = finalMimeType,
                sizeBytes = optimizedImageData.Length,
                storageType = "blob"
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to upload BLOB photo: {ex.Message}"));
        }
    }

    [HttpGet("{id}/photo")]
    public async Task<IActionResult> GetCandidatePhoto(int id)
    {
        try
        {
            // Get candidate data
            var candidate = await _candidateService.GetCandidateByIdAsync(id);
            if (candidate == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Candidate not found"));
            }

            // Smart photo logic - prioritize BLOB over file
            bool hasBlobPhoto = candidate.HasPhotoBlob;
            bool hasFilePhoto = candidate.HasPhotoFile;

            if (!hasBlobPhoto && !hasFilePhoto)
            {
                return Ok(ApiResponse<object>.SuccessResult(new { 
                    photoUrl = (string?)null,
                    hasPhoto = false,
                    storageType = "none",
                    message = "No photo available for this candidate"
                }));
            }

            // Prefer BLOB storage if available
            if (hasBlobPhoto)
            {
                // Get the full candidate model to access PhotoData
                var candidateModel = await _candidateService.GetCandidateModelByIdAsync(id);
                if (candidateModel?.PhotoData == null)
                {
                    return Ok(ApiResponse<object>.SuccessResult(new { 
                        photoUrl = (string?)null,
                        hasPhoto = false,
                        storageType = "blob_missing",
                        message = "BLOB photo data not found"
                    }));
                }

                // Log access to BLOB photo
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    await _auditService.LogAsync(userId.Value, GetCurrentUserRole() ?? "anonymous", "view_photo_blob", "candidates", id, 
                        $"BLOB photo accessed: {candidate.PhotoFileName}");
                }

                // Return BLOB photo as Base64 data URL
                var photoBase64 = Convert.ToBase64String(candidateModel.PhotoData);
                var dataUrl = $"data:{candidateModel.PhotoMimeType};base64,{photoBase64}";

                return Ok(ApiResponse<object>.SuccessResult(new { 
                    photoUrl = dataUrl,
                    hasPhoto = true,
                    storageType = "blob",
                    mimeType = candidateModel.PhotoMimeType,
                    fileName = candidateModel.PhotoFileName,
                    sizeBytes = candidateModel.PhotoData.Length,
                    candidateName = candidate.Name
                }));
            }

            // Fall back to file storage
            if (hasFilePhoto)
            {
                // Check if photo file actually exists
                var photoPath = candidate.PhotoUrl!.TrimStart('/');
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photoPath);
                
                if (!System.IO.File.Exists(fullPath))
                {
                    return Ok(ApiResponse<object>.SuccessResult(new { 
                        photoUrl = (string?)null,
                        hasPhoto = false,
                        storageType = "file_missing",
                        message = "Photo file not found on server"
                    }));
                }

                // Log access to file photo
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    await _auditService.LogAsync(userId.Value, GetCurrentUserRole() ?? "anonymous", "view_photo_file", "candidates", id, 
                        $"File photo accessed: {candidate.PhotoUrl}");
                }

                // Return file photo information
                return Ok(ApiResponse<object>.SuccessResult(new { 
                    photoUrl = candidate.PhotoUrl,
                    hasPhoto = true,
                    storageType = "file",
                    fullUrl = $"{Request.Scheme}://{Request.Host}{candidate.PhotoUrl}",
                    candidateName = candidate.Name
                }));
            }

            return Ok(ApiResponse<object>.SuccessResult(new { 
                photoUrl = (string?)null,
                hasPhoto = false,
                storageType = "none",
                message = "No photo available"
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Failed to get candidate photo: {ex.Message}"));
        }
    }

    [HttpPut("position/{positionId}/reorder")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ReorderCandidates(int positionId, [FromBody] Dictionary<int, int> candidateOrders)
    {
        try
        {
            var result = await _candidateService.ReorderCandidatesAsync(positionId, candidateOrders);
            
            if (!result)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Failed to reorder candidates"));
            }

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "reorder", "candidates", positionId, "Candidates reordered");
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Candidates reordered successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to reorder candidates"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }
}