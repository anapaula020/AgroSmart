using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize(Roles = Api.Roles.Admin)]
public class UsersController(UserManager<IdentityUser> userManager) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var result = new List<object>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            result.Add(new
            {
                u.Id,
                u.Email,
                Role       = roles.FirstOrDefault(r => r != "User") ?? roles.FirstOrDefault() ?? "Operador",
                IsDisabled = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                IsCurrent  = u.Id == CurrentUserId
            });
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new ErrorResponse("Email e senha são obrigatórios."));

        var role = IsValidRole(req.Role) ? req.Role : Api.Roles.Operador;

        var user = new IdentityUser { UserName = req.Email, Email = req.Email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new ErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, role);
        return Ok(new { user.Id, user.Email, Role = role });
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleRequest req)
    {
        if (!IsValidRole(req.Role))
            return BadRequest(new ErrorResponse("Role inválida."));

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == CurrentUserId)
            return BadRequest(new ErrorResponse("Não é possível alterar sua própria role."));

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, req.Role);
        return Ok(new { id, Role = req.Role });
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == CurrentUserId)
            return BadRequest(new ErrorResponse("Não é possível desativar sua própria conta."));

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        return Ok();
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == CurrentUserId)
            return BadRequest(new ErrorResponse("Não é possível excluir sua própria conta."));

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new ErrorResponse(string.Join(", ", result.Errors.Select(e => e.Description))));

        return NoContent();
    }

    private static bool IsValidRole(string? role) =>
        role is Api.Roles.Admin or Api.Roles.Gestor or Api.Roles.Operador or Api.Roles.Consulta;
}

public record CreateUserRequest(
    [Required] string Email,
    [Required] string Password,
    string Role = Api.Roles.Operador
);

public record ChangeRoleRequest([Required] string Role);
