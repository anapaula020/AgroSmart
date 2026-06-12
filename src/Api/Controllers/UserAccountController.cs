using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Route("account")]
public class UserAccountController(
    UserManager<IdentityUser> userManager,
    AppDbContext db,
    TokenService tokenService) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Schemes aceitos pelos endpoints JSON: cookie de sessão, JWT ou ApiKey
    private const string JsonSchemes =
        "Identity.Application" + "," +
        JwtBearerDefaults.AuthenticationScheme + "," +
        Api.Middleware.ApiKeyAuthHandler.SchemeName;

    // ── Página — protegida via cookie Identity ────────────────────────────────
    [HttpGet("profile")]
    [Authorize(AuthenticationSchemes = "Identity.Application")]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null) return RedirectToAction("Login", "Login");
        var roles = await userManager.GetRolesAsync(user);
        ViewData["Title"] = "Minha Conta";
        ViewData["Token"] = await tokenService.GenerateAsync(user, roles);
        return View(user);
    }

    // ── JSON endpoints ────────────────────────────────────────────────────────
    [HttpPost("update-email")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailDto req)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null) return Json(new { ok = false, error = "Usuário não encontrado." });

        if (string.IsNullOrWhiteSpace(req.NewEmail) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(req.NewEmail))
            return Json(new { ok = false, error = "E-mail inválido." });

        var existing = await userManager.FindByEmailAsync(req.NewEmail);
        if (existing is not null && existing.Id != user.Id)
            return Json(new { ok = false, error = "E-mail já em uso." });

        user.Email              = req.NewEmail;
        user.UserName           = req.NewEmail;
        user.NormalizedEmail    = req.NewEmail.ToUpperInvariant();
        user.NormalizedUserName = req.NewEmail.ToUpperInvariant();
        var result = await userManager.UpdateAsync(user);

        return Json(result.Succeeded
            ? new { ok = true, email = req.NewEmail }
            : new { ok = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto req)
    {
        var user = await userManager.FindByIdAsync(UserId);
        if (user is null) return Json(new { ok = false, error = "Usuário não encontrado." });

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        return Json(result.Succeeded
            ? new { ok = true }
            : new { ok = false, error = string.Join(", ", result.Errors.Select(e => e.Description)) });
    }

    [HttpGet("apikeys")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    public async Task<IActionResult> GetApiKeys()
    {
        var keys = await db.ApiKeys
            .Where(k => k.UserId == UserId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new {
                k.Id, k.Name, k.Prefix, k.Scope,
                k.IsActive, k.ExpiresAt, k.LastUsedAt, k.CreatedAt
            })
            .ToListAsync();
        return Json(keys);
    }

    [HttpPost("apikeys")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length < 2)
            return Json(new { ok = false, error = "Nome deve ter ao menos 2 caracteres." });

        var (rawKey, prefix, hash) = ApiKeyService.GenerateKey();

        DateTime? expires = null;
        if (!string.IsNullOrEmpty(req.ExpiresAt) && DateTime.TryParse(req.ExpiresAt, out var parsed))
            expires = parsed.ToUniversalTime();

        var key = new ApiKey
        {
            UserId    = UserId,
            Name      = req.Name,
            KeyHash   = hash,
            Prefix    = prefix,
            Scope     = req.Scope,
            ExpiresAt = expires
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        return Json(new { ok = true, id = key.Id, name = key.Name, prefix = key.Prefix, scope = key.Scope, rawKey });
    }

    [HttpPost("apikeys/{id:guid}/deactivate")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeactivateApiKey(Guid id)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId);
        if (key is null) return Json(new { ok = false, error = "Chave não encontrada." });

        key.IsActive  = false;
        key.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpPost("apikeys/{id:guid}/delete")]
    [Authorize(AuthenticationSchemes = JsonSchemes)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteApiKey(Guid id)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.UserId == UserId);
        if (key is null) return Json(new { ok = false, error = "Chave não encontrada." });

        db.ApiKeys.Remove(key);
        await db.SaveChangesAsync();
        return Json(new { ok = true });
    }
}

public record UpdateEmailDto(string NewEmail);
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
public record CreateApiKeyDto(string Name, ApiKeyScope Scope = ApiKeyScope.ReadOnly, string? ExpiresAt = null);
