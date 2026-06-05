using System.ComponentModel.DataAnnotations;

namespace Api.Models;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(string Token, string Email, IList<string> Roles, DateTime ExpiresAt);
