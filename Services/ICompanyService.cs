using ElectionApi.Net.DTOs;

namespace ElectionApi.Net.Services;

public interface ICompanyService
{
    Task<ApiResponse<CompanyResponseDto>> CreateCompanyAsync(CompanyCreateDto companyDto);
    Task<ApiResponse<CompanyResponseDto>> GetCompanyByIdAsync(int id);
    Task<ApiResponse<IEnumerable<CompanyResponseDto>>> GetAllCompaniesAsync(bool includeInactive = false);
    Task<ApiResponse<CompanyResponseDto>> UpdateCompanyAsync(int id, CompanyUpdateDto companyDto);
    Task<ApiResponse<bool>> DeleteCompanyAsync(int id);
    Task<ApiResponse<string>> UploadCompanyLogoAsync(int companyId, IFormFile logoFile);
    Task<ApiResponse<bool>> DeleteCompanyLogoAsync(int companyId);
}