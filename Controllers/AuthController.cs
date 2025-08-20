using Microsoft.AspNetCore.Mvc;
using ElectionApi.Net.DTOs;
using ElectionApi.Net.Services;
using System.Security.Claims;

namespace ElectionApi.Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;

    public AuthController(IAuthService authService, IAuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
    }

    [HttpPost("admin/login")]
    public async Task<IActionResult> AdminLogin([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Dados de entrada inválidos", ModelState));
        }

        try
        {
            var admin = await _authService.ValidateAdminCredentialsAsync(loginDto.Email, loginDto.Password);
            
            if (admin == null)
            {
                await _auditService.LogAsync(null, "anonymous", "login_failed", "auth", null, 
                    $"Tentativa de login inválida para {loginDto.Email}");
                return Unauthorized(ApiResponse<object>.ErrorResult("Credenciais inválidas"));
            }

            // Verifica se o administrador está ativo
            if (!admin.IsActive)
            {
                await _auditService.LogAsync(admin.Id, "admin", "login_failed_inactive", "auth", admin.Id,
                    $"Tentativa de login com conta inativa: {admin.Email}");
                return StatusCode(403, ApiResponse<object>.ErrorResult("Conta desativada"));
            }

            // Login direto sem 2FA
            var token = await _authService.GenerateJwtTokenAsync(admin.Id, admin.Role);
            await _authService.UpdateLastLoginAsync(admin.Id, "admin");
            
            await _auditService.LogAsync(admin.Id, "admin", "login_success", "auth", admin.Id,
                $"Login bem-sucedido para {admin.Email}");

            var response = new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = admin.Id,
                    Name = admin.Name,
                    Email = admin.Email,
                    Role = admin.Role,
                    Permissions = admin.Permissions
                },
                ExpiresIn = 3600 // 1 hour
            };

            return Ok(ApiResponse<LoginResponseDto>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(null, "system", "login_error", "auth", null, 
                $"Erro no login: {ex.Message}");
            return StatusCode(500, ApiResponse<object>.ErrorResult("Falha no login"));
        }
    }


    [HttpPost("voter/login")]
    public async Task<IActionResult> VoterLogin([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data", ModelState));
        }

        try
        {
            var voter = await _authService.ValidateVoterCredentialsAsync(loginDto.Email, loginDto.Password);
            
            if (voter == null)
            {
                await _auditService.LogAsync("voter", "login_failed", "auth", null, loginDto.Email);
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid credentials"));
            }

            if (!voter.IsActive || !voter.IsVerified)
            {
                return Forbid("Account not active or verified");
            }

            var token = await _authService.GenerateJwtTokenAsync(voter.Id, "voter");
            await _authService.UpdateLastLoginAsync(voter.Id, "voter");
            
            await _auditService.LogAsync(voter.Id, "voter", "login_success", "auth", voter.Id);

            var response = new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = voter.Id,
                    Name = voter.Name,
                    Email = voter.Email,
                    Cpf = voter.Cpf,
                    VoteWeight = voter.VoteWeight
                },
                ExpiresIn = 300 // 5 minutes
            };

            return Ok(ApiResponse<LoginResponseDto>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync("system", "login_error", "auth", null, ex.Message);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Login failed"));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            
            if (userId.HasValue && !string.IsNullOrEmpty(role))
            {
                await _auditService.LogAsync(userId.Value, role, "logout", "auth", userId.Value);
            }

            return Ok(ApiResponse<object>.SuccessResult(null, "Logged out successfully"));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Logout failed"));
        }
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken()
    {
        try
        {
            var token = GetTokenFromHeader();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Token not provided"));
            }

            var isValid = await _authService.ValidateTokenAsync(token);
            if (!isValid)
            {
                return Unauthorized(ApiResponse<object>.ErrorResult("Invalid token"));
            }

            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            var response = new
            {
                Valid = true,
                UserId = userId,
                Role = role
            };

            return Ok(ApiResponse<object>.SuccessResult(response));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponse<object>.ErrorResult("Token validation failed"));
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

    private string? GetTokenFromHeader()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer "))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }
}