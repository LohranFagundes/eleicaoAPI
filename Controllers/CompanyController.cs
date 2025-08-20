using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ILogger<CompanyController> _logger;

    public CompanyController(
        ICompanyService companyService,
        ILogger<CompanyController> logger)
    {
        _companyService = companyService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<CompanyResponseDto>>> CreateCompany([FromBody] CompanyCreateDto companyDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<CompanyResponseDto>.ErrorResult("Dados inválidos", ModelState));
        }

        var result = await _companyService.CreateCompanyAsync(companyDto);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetCompanyById), new { id = result.Data?.Id }, result);
        }

        return BadRequest(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<CompanyResponseDto>>> GetCompanyById(int id)
    {
        var result = await _companyService.GetCompanyByIdAsync(id);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CompanyResponseDto>>>> GetAllCompanies(
        [FromQuery] bool includeInactive = false)
    {
        var result = await _companyService.GetAllCompaniesAsync(includeInactive);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<CompanyResponseDto>>> UpdateCompany(
        int id, 
        [FromBody] CompanyUpdateDto companyDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<CompanyResponseDto>.ErrorResult("Dados inválidos", ModelState));
        }

        var result = await _companyService.UpdateCompanyAsync(id, companyDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCompany(int id)
    {
        var result = await _companyService.DeleteCompanyAsync(id);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("{id}/logo")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<string>>> UploadCompanyLogo(
        int id, 
        [FromForm] IFormFile logoFile)
    {
        if (logoFile == null)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Arquivo de logo é obrigatório"));
        }

        var result = await _companyService.UploadCompanyLogoAsync(id, logoFile);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpDelete("{id}/logo")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCompanyLogo(int id)
    {
        var result = await _companyService.DeleteCompanyLogoAsync(id);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}