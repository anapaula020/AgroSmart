using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

// ── Authentication handler (integra com o pipeline do ASP.NET Identity) ───────
public class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService apiKeyService,
    Api.Data.AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var rawKey) || string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var apiKey = await apiKeyService.ValidateAsync(rawKey!);
        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid or expired API key");

        // Monta claims equivalentes ao JWT
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.UserId),
            new("ApiKeyId",               apiKey.Id.ToString()),
            new("ApiKeyScope",            apiKey.Scope.ToString()),
        };

        // Busca roles do usuário para manter controle de acesso por role
        var userRoles = db.UserRoles
            .Where(ur => ur.UserId == apiKey.UserId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToList();

        // Restringe roles baseado no scope da key
        var effectiveRoles = apiKey.Scope switch
        {
            Api.Models.ApiKeyScope.Admin     => userRoles,
            Api.Models.ApiKeyScope.ReadWrite => userRoles.Where(r => r != "Admin").ToList(),
            Api.Models.ApiKeyScope.ReadOnly  => [],  // sem roles = sem acesso a [Authorize(Roles="Admin")]
            _ => []
        };

        claims.AddRange(effectiveRoles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
