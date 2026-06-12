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

        // API key pertence ao workspace — derivamos identidade do dono do workspace
        var workspace = await db.Workspaces.FindAsync(apiKey.WorkspaceId);
        if (workspace is null)
            return AuthenticateResult.Fail("Workspace for this API key not found");

        // Autentica como o dono do workspace para que verificações OwnerId == UserId
        // continuem funcionando para dados vinculados ao workspace
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, workspace.OwnerId),
            new("WorkspaceId",             apiKey.WorkspaceId.ToString()),
            new("ApiKeyId",                apiKey.Id.ToString()),
            new("ApiKeyScope",             apiKey.Scope.ToString()),
        };

        // Mapeia scope para roles — nenhuma key concede role de sistema Admin
        var effectiveRoles = apiKey.Scope switch
        {
            Api.Models.ApiKeyScope.Admin     => new[] { Roles.Gestor, Roles.Operador },
            Api.Models.ApiKeyScope.ReadWrite => new[] { Roles.Operador },
            _                                => Array.Empty<string>()
        };

        claims.AddRange(effectiveRoles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
