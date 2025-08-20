namespace ElectionApi.Net.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
    public int ExpiresIn { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Cpf { get; set; }
    public decimal? VoteWeight { get; set; }
    public object? Permissions { get; set; }
}