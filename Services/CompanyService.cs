using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.Data;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Models;

namespace ElectionApi.Net.Services;

public class CompanyService : ICompanyService
{
    private readonly IRepository<Company> _companyRepository;
    private readonly ILogger<CompanyService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditService _auditService;

    public CompanyService(
        IRepository<Company> companyRepository,
        ILogger<CompanyService> logger,
        IWebHostEnvironment environment,
        IAuditService auditService)
    {
        _companyRepository = companyRepository;
        _logger = logger;
        _environment = environment;
        _auditService = auditService;
    }

    public async Task<ApiResponse<CompanyResponseDto>> CreateCompanyAsync(CompanyCreateDto companyDto)
    {
        try
        {
            // Verificar se CNPJ já existe
            var existingCompany = await _companyRepository.GetQueryable()
                .FirstOrDefaultAsync(c => c.Cnpj == companyDto.Cnpj);

            if (existingCompany != null)
            {
                return ApiResponse<CompanyResponseDto>.ErrorResult("CNPJ já cadastrado no sistema");
            }

            var company = new Company
            {
                NomeFantasia = companyDto.NomeFantasia,
                RazaoSocial = companyDto.RazaoSocial,
                Cnpj = companyDto.Cnpj,
                Cep = companyDto.Cep,
                Bairro = companyDto.Bairro,
                Logradouro = companyDto.Logradouro,
                Numero = companyDto.Numero,
                Cidade = companyDto.Cidade,
                Pais = companyDto.Pais,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _companyRepository.AddAsync(company);

            var response = MapToCompanyResponse(company);

            await _auditService.LogAsync(null, "system", "create_company", "company", company.Id,
                $"Company '{company.NomeFantasia}' created with CNPJ: {company.Cnpj}");

            return ApiResponse<CompanyResponseDto>.SuccessResult(response, "Empresa criada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return ApiResponse<CompanyResponseDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<CompanyResponseDto>> GetCompanyByIdAsync(int id)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return ApiResponse<CompanyResponseDto>.ErrorResult("Empresa não encontrada");
            }

            var response = MapToCompanyResponse(company);
            return ApiResponse<CompanyResponseDto>.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company by ID: {CompanyId}", id);
            return ApiResponse<CompanyResponseDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<IEnumerable<CompanyResponseDto>>> GetAllCompaniesAsync(bool includeInactive = false)
    {
        try
        {
            var query = _companyRepository.GetQueryable();
            
            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            var companies = await query.OrderBy(c => c.NomeFantasia).ToListAsync();
            var response = companies.Select(MapToCompanyResponse);

            return ApiResponse<IEnumerable<CompanyResponseDto>>.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all companies");
            return ApiResponse<IEnumerable<CompanyResponseDto>>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<CompanyResponseDto>> UpdateCompanyAsync(int id, CompanyUpdateDto companyDto)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return ApiResponse<CompanyResponseDto>.ErrorResult("Empresa não encontrada");
            }

            // Verificar se CNPJ já existe em outra empresa
            if (!string.IsNullOrEmpty(companyDto.Cnpj) && companyDto.Cnpj != company.Cnpj)
            {
                var existingCompany = await _companyRepository.GetQueryable()
                    .FirstOrDefaultAsync(c => c.Cnpj == companyDto.Cnpj && c.Id != id);

                if (existingCompany != null)
                {
                    return ApiResponse<CompanyResponseDto>.ErrorResult("CNPJ já cadastrado no sistema");
                }
            }

            // Atualizar apenas campos fornecidos
            if (!string.IsNullOrEmpty(companyDto.NomeFantasia))
                company.NomeFantasia = companyDto.NomeFantasia;

            if (!string.IsNullOrEmpty(companyDto.RazaoSocial))
                company.RazaoSocial = companyDto.RazaoSocial;

            if (!string.IsNullOrEmpty(companyDto.Cnpj))
                company.Cnpj = companyDto.Cnpj;

            if (!string.IsNullOrEmpty(companyDto.Cep))
                company.Cep = companyDto.Cep;

            if (!string.IsNullOrEmpty(companyDto.Bairro))
                company.Bairro = companyDto.Bairro;

            if (!string.IsNullOrEmpty(companyDto.Logradouro))
                company.Logradouro = companyDto.Logradouro;

            if (!string.IsNullOrEmpty(companyDto.Numero))
                company.Numero = companyDto.Numero;

            if (!string.IsNullOrEmpty(companyDto.Cidade))
                company.Cidade = companyDto.Cidade;

            if (!string.IsNullOrEmpty(companyDto.Pais))
                company.Pais = companyDto.Pais;

            if (companyDto.IsActive.HasValue)
                company.IsActive = companyDto.IsActive.Value;

            company.UpdatedAt = DateTime.UtcNow;

            await _companyRepository.UpdateAsync(company);

            var response = MapToCompanyResponse(company);

            await _auditService.LogAsync(null, "system", "update_company", "company", company.Id,
                $"Company '{company.NomeFantasia}' updated");

            return ApiResponse<CompanyResponseDto>.SuccessResult(response, "Empresa atualizada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company with ID: {CompanyId}", id);
            return ApiResponse<CompanyResponseDto>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<bool>> DeleteCompanyAsync(int id)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(id);
            if (company == null)
            {
                return ApiResponse<bool>.ErrorResult("Empresa não encontrada");
            }

            // Soft delete
            company.IsActive = false;
            company.UpdatedAt = DateTime.UtcNow;

            await _companyRepository.UpdateAsync(company);

            await _auditService.LogAsync(null, "system", "delete_company", "company", company.Id,
                $"Company '{company.NomeFantasia}' deactivated");

            return ApiResponse<bool>.SuccessResult(true, "Empresa desativada com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company with ID: {CompanyId}", id);
            return ApiResponse<bool>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<string>> UploadCompanyLogoAsync(int companyId, IFormFile logoFile)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return ApiResponse<string>.ErrorResult("Empresa não encontrada");
            }

            // Validar arquivo
            if (logoFile == null || logoFile.Length == 0)
            {
                return ApiResponse<string>.ErrorResult("Arquivo não fornecido");
            }

            // Validar extensão
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var fileExtension = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return ApiResponse<string>.ErrorResult("Formato de arquivo não suportado. Use: JPG, PNG, GIF, BMP");
            }

            // Validar tamanho (5MB máximo)
            if (logoFile.Length > 5 * 1024 * 1024)
            {
                return ApiResponse<string>.ErrorResult("Arquivo muito grande. Tamanho máximo: 5MB");
            }

            // Criar diretório se não existir
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "company-logos");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            // Remover logo anterior se existir
            if (!string.IsNullOrEmpty(company.LogoUrl))
            {
                await DeleteCompanyLogoFileAsync(company.LogoUrl);
            }

            // Gerar nome único para o arquivo
            var fileName = $"company-{companyId}-{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Salvar arquivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await logoFile.CopyToAsync(stream);
            }

            // Atualizar empresa com nova URL
            var logoUrl = $"/uploads/company-logos/{fileName}";
            company.LogoUrl = logoUrl;
            company.UpdatedAt = DateTime.UtcNow;

            await _companyRepository.UpdateAsync(company);

            await _auditService.LogAsync(null, "system", "upload_company_logo", "company", company.Id,
                $"Logo uploaded for company '{company.NomeFantasia}'");

            return ApiResponse<string>.SuccessResult(logoUrl, "Logo da empresa enviado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading logo for company: {CompanyId}", companyId);
            return ApiResponse<string>.ErrorResult("Erro interno do servidor");
        }
    }

    public async Task<ApiResponse<bool>> DeleteCompanyLogoAsync(int companyId)
    {
        try
        {
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return ApiResponse<bool>.ErrorResult("Empresa não encontrada");
            }

            if (string.IsNullOrEmpty(company.LogoUrl))
            {
                return ApiResponse<bool>.ErrorResult("Empresa não possui logo");
            }

            // Remover arquivo físico
            await DeleteCompanyLogoFileAsync(company.LogoUrl);

            // Atualizar empresa
            company.LogoUrl = null;
            company.UpdatedAt = DateTime.UtcNow;

            await _companyRepository.UpdateAsync(company);

            await _auditService.LogAsync(null, "system", "delete_company_logo", "company", company.Id,
                $"Logo deleted for company '{company.NomeFantasia}'");

            return ApiResponse<bool>.SuccessResult(true, "Logo da empresa removido com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logo for company: {CompanyId}", companyId);
            return ApiResponse<bool>.ErrorResult("Erro interno do servidor");
        }
    }

    private async Task DeleteCompanyLogoFileAsync(string logoUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(logoUrl)) return;

            var fileName = Path.GetFileName(logoUrl);
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", "company-logos", fileName);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete logo file: {LogoUrl}", logoUrl);
        }
    }

    private CompanyResponseDto MapToCompanyResponse(Company company)
    {
        return new CompanyResponseDto
        {
            Id = company.Id,
            NomeFantasia = company.NomeFantasia,
            RazaoSocial = company.RazaoSocial,
            Cnpj = company.Cnpj,
            Cep = company.Cep,
            Bairro = company.Bairro,
            Logradouro = company.Logradouro,
            Numero = company.Numero,
            Cidade = company.Cidade,
            Pais = company.Pais,
            LogoUrl = company.LogoUrl,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt,
            IsActive = company.IsActive
        };
    }
}