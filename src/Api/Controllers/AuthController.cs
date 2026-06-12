using System.Security.Claims;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    TokenService tokenService,
    IConfiguration config) : ControllerBase
{
    /// <summary>Registrar novo usuário</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user   = new IdentityUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new ErrorResponse("Registration failed",
                Errors: result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));

        await userManager.AddToRoleAsync(user, "User");
        var roles   = await userManager.GetRolesAsync(user);
        var token   = await tokenService.GenerateAsync(user, roles);
        var expires = DateTime.UtcNow.AddHours(double.Parse(config["Jwt:ExpiresHours"] ?? "8"));

        return StatusCode(201, new AuthResponse(token, user.Email!, roles, expires));
    }

    /// <summary>Login - retorna JWT</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new ErrorResponse("Invalid credentials"));

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ErrorResponse(result.IsLockedOut ? "Account locked" : "Invalid credentials"));

        var roles   = await userManager.GetRolesAsync(user);
        var token   = await tokenService.GenerateAsync(user, roles);
        var expires = DateTime.UtcNow.AddHours(double.Parse(config["Jwt:ExpiresHours"] ?? "8"));

        return Ok(new AuthResponse(token, user.Email!, roles, expires));
    }

    /// <summary>Dados do usuário autenticado</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await userManager.FindByIdAsync(userId ?? "");
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new { user.Id, user.Email, Roles = roles });
    }
}
