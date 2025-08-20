using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using ElectionApi.Net.Data;
using ElectionApi.Net.Models;
using System.Security.Claims;
using AutoMapper;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,master")]
public class AdminManagementController : ControllerBase
{
    private readonly IRepository<Admin> _adminRepository;
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;
    private readonly ILogger<AdminManagementController> _logger;

    public AdminManagementController(
        IRepository<Admin> adminRepository,
        IAuthService authService,
        IAuditService auditService,
        IMapper mapper,
        ILogger<AdminManagementController> logger)
    {
        _adminRepository = adminRepository;
        _authService = authService;
        _auditService = auditService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os administradores com paginação
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAdmins([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        try
        {
            var query = _adminRepository.GetQueryable()
                .OrderByDescending(a => a.CreatedAt);

            var totalItems = await query.CountAsync();
            var admins = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var adminDtos = _mapper.Map<List<AdminResponseDto>>(admins);

            var pagedResult = new PagedResult<AdminResponseDto>
            {
                Items = adminDtos,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling((double)totalItems / limit),
                CurrentPage = page,
                HasNextPage = page * limit < totalItems,
                HasPreviousPage = page > 1
            };

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "admin_list", null,
                    $"Visualizou lista de administradores - Página {page}");
            }

            return Ok(ApiResponse<PagedResult<AdminResponseDto>>.SuccessResult(pagedResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar administradores");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    /// <summary>
    /// Obtém um administrador específico por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAdmin(int id)
    {
        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Administrador não encontrado"));
            }

            var adminDto = _mapper.Map<AdminResponseDto>(admin);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "view", "admin", id,
                    $"Visualizou dados do administrador {admin.Name}");
            }

            return Ok(ApiResponse<AdminResponseDto>.SuccessResult(adminDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter administrador {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    /// <summary>
    /// Cria um novo administrador
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,master")] // Apenas super admins podem criar outros admins
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminDto createDto)
    {
        try
        {
            // Verifica se já existe um admin com este email
            var existingAdmin = await _adminRepository.GetQueryable()
                .FirstOrDefaultAsync(a => a.Email == createDto.Email);

            if (existingAdmin != null)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Já existe um administrador com este email"));
            }

            // Verifica se apenas master users podem criar outros admins
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            // Cria o novo administrador
            var admin = new Admin
            {
                Name = createDto.Name,
                Email = createDto.Email,
                Password = _authService.HashPassword(createDto.Password),
                Role = createDto.Role,
                Permissions = createDto.Permissions,
                IsActive = createDto.IsActive,
                IsSuper = createDto.IsSuper,
                IsMaster = false, // Apenas o sistema pode criar masters
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdAdmin = await _adminRepository.AddAsync(admin);
            var adminDto = _mapper.Map<AdminResponseDto>(createdAdmin);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "create", "admin", createdAdmin.Id,
                    $"Criou novo administrador: {createDto.Name} ({createDto.Email})");
            }

            return CreatedAtAction(nameof(GetAdmin), new { id = createdAdmin.Id }, 
                ApiResponse<AdminResponseDto>.SuccessResult(adminDto, "Administrador criado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar administrador");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    /// <summary>
    /// Atualiza um administrador existente
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAdmin(int id, [FromBody] UpdateAdminDto updateDto)
    {
        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Administrador não encontrado"));
            }

            // Protege master users de modificações
            if (admin.IsMaster)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Usuário master não pode ser modificado"));
            }

            // Verifica se o email está sendo alterado e se já existe
            if (!string.IsNullOrEmpty(updateDto.Email) && updateDto.Email != admin.Email)
            {
                var existingAdmin = await _adminRepository.GetQueryable()
                    .FirstOrDefaultAsync(a => a.Email == updateDto.Email && a.Id != id);

                if (existingAdmin != null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Já existe um administrador com este email"));
                }
            }

            // Atualiza apenas os campos fornecidos
            if (!string.IsNullOrEmpty(updateDto.Name))
                admin.Name = updateDto.Name;

            if (!string.IsNullOrEmpty(updateDto.Email))
                admin.Email = updateDto.Email;

            if (!string.IsNullOrEmpty(updateDto.Password))
                admin.Password = _authService.HashPassword(updateDto.Password);

            if (!string.IsNullOrEmpty(updateDto.Role))
                admin.Role = updateDto.Role;

            if (updateDto.Permissions != null)
                admin.Permissions = updateDto.Permissions;

            if (updateDto.IsActive.HasValue)
                admin.IsActive = updateDto.IsActive.Value;

            if (updateDto.IsSuper.HasValue)
                admin.IsSuper = updateDto.IsSuper.Value;


            admin.UpdatedAt = DateTime.UtcNow;

            await _adminRepository.UpdateAsync(admin);
            var adminDto = _mapper.Map<AdminResponseDto>(admin);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "update", "admin", id,
                    $"Atualizou administrador: {admin.Name} ({admin.Email})");
            }

            return Ok(ApiResponse<AdminResponseDto>.SuccessResult(adminDto, "Administrador atualizado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar administrador {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    /// <summary>
    /// Desativa um administrador (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateAdmin(int id)
    {
        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Administrador não encontrado"));
            }

            // Protege master users de desativação
            if (admin.IsMaster)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Usuário master não pode ser desativado"));
            }

            // Não permite desativar o próprio usuário
            var currentUserId = GetCurrentUserId();
            if (currentUserId == id)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Não é possível desativar sua própria conta"));
            }

            // Desativa o administrador ao invés de deletar
            admin.IsActive = false;
            admin.UpdatedAt = DateTime.UtcNow;

            await _adminRepository.UpdateAsync(admin);

            if (currentUserId.HasValue)
            {
                await _auditService.LogAsync(currentUserId.Value, "admin", "deactivate", "admin", id,
                    $"Desativou administrador: {admin.Name} ({admin.Email})");
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Administrador desativado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desativar administrador {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    /// <summary>
    /// Reativa um administrador desativado
    /// </summary>
    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> ReactivateAdmin(int id)
    {
        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Administrador não encontrado"));
            }

            admin.IsActive = true;
            admin.UpdatedAt = DateTime.UtcNow;

            await _adminRepository.UpdateAsync(admin);

            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                await _auditService.LogAsync(userId.Value, "admin", "reactivate", "admin", id,
                    $"Reativou administrador: {admin.Name} ({admin.Email})");
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Administrador reativado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reativar administrador {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erro interno do servidor"));
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}