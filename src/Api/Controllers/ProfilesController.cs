using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

// ── Profiles ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ProfilesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Profiles.OrderBy(p => p.Name).Select(p => new {
            p.Id, p.Name, p.Description, p.IsDefault,
            UserCount = p.UserProfiles.Count(u => u.IsActive)
        }).ToListAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await db.Profiles.Include(x => x.UserProfiles).FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Ok(new {
            p.Id, p.Name, p.Description, p.IsDefault,
            Users = p.UserProfiles.Where(u => u.IsActive).Select(u => new { u.UserId, u.IsActive })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LookupRequest req)
    {
        var profile = new Profile { Name = req.Name, Description = req.Description };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, new { profile.Id, profile.Name });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LookupRequest req)
    {
        var p = await db.Profiles.FindAsync(id);
        if (p is null) return NotFound();
        p.Name = req.Name; p.Description = req.Description; p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.Name });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await db.Profiles.Include(x => x.UserProfiles).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (p.UserProfiles.Any()) return BadRequest(new ErrorResponse("Profile has assigned users"));
        db.Profiles.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Assign / remove profile from user ─────────────────────────────────────
    [HttpPost("{id:guid}/users/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignUser(Guid id, string userId)
    {
        if (!await db.Profiles.AnyAsync(p => p.Id == id)) return NotFound();

        var existing = await db.UserProfiles.FirstOrDefaultAsync(u => u.ProfileId == id && u.UserId == userId);
        if (existing is not null)
        {
            existing.IsActive = true;
            await db.SaveChangesAsync();
            return Ok();
        }

        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = id });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:guid}/users/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveUser(Guid id, string userId)
    {
        var up = await db.UserProfiles.FirstOrDefaultAsync(u => u.ProfileId == id && u.UserId == userId);
        if (up is null) return NotFound();
        up.IsActive = false; up.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── My profiles ───────────────────────────────────────────────────────────
    [HttpGet("me")]
    public async Task<IActionResult> MyProfiles()
    {
        var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profiles = await db.UserProfiles
            .Include(u => u.Profile)
            .Where(u => u.UserId == userId && u.IsActive)
            .Select(u => new { u.Profile!.Id, u.Profile.Name, u.Profile.Description })
            .ToListAsync();
        return Ok(profiles);
    }
}

// ── API Keys (workspace-scoped) ───────────────────────────────────────────────
[ApiController]
[Route("api/v1/apikeys")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ApiKeysController(AppDbContext db, ApiKeyService apiKeyService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin  => User.IsInRole(Roles.Admin);

    private async Task<bool> CanManageWorkspace(Guid workspaceId)
    {
        if (IsAdmin) return true;
        var ws = await db.Workspaces.FindAsync(workspaceId);
        if (ws is null) return false;
        if (ws.OwnerId == UserId) return true;
        return await db.WorkspaceMembers.AnyAsync(m =>
            m.WorkspaceId == workspaceId && m.UserId == UserId &&
            (m.Role == WorkspaceRole.Owner || m.Role == WorkspaceRole.Agronomo));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? workspaceId = null)
    {
        IQueryable<ApiKey> query;

        if (IsAdmin)
        {
            query = db.ApiKeys.Include(k => k.Workspace);
            if (workspaceId.HasValue)
                query = query.Where(k => k.WorkspaceId == workspaceId.Value);
        }
        else
        {
            var managedIds = await db.WorkspaceMembers
                .Where(m => m.UserId == UserId &&
                    (m.Role == WorkspaceRole.Owner || m.Role == WorkspaceRole.Agronomo))
                .Select(m => m.WorkspaceId)
                .ToListAsync();

            var ownedIds = await db.Workspaces
                .Where(w => w.OwnerId == UserId)
                .Select(w => w.Id)
                .ToListAsync();

            var accessIds = managedIds.Union(ownedIds).ToList();

            if (workspaceId.HasValue)
            {
                if (!accessIds.Contains(workspaceId.Value)) return Forbid();
                query = db.ApiKeys.Include(k => k.Workspace)
                    .Where(k => k.WorkspaceId == workspaceId.Value);
            }
            else
            {
                query = db.ApiKeys.Include(k => k.Workspace)
                    .Where(k => accessIds.Contains(k.WorkspaceId));
            }
        }

        return Ok(await query.OrderByDescending(k => k.CreatedAt).Select(k => new {
            k.Id, k.Name, k.Prefix, k.Scope, k.IsActive,
            k.ExpiresAt, k.LastUsedAt, k.CreatedAt,
            k.CreatedByUserId,
            WorkspaceId   = k.WorkspaceId,
            WorkspaceName = k.Workspace!.Name
        }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest req)
    {
        if (!await CanManageWorkspace(req.WorkspaceId))
            return Forbid();

        var (rawKey, prefix, hash) = ApiKeyService.GenerateKey();

        var key = new ApiKey
        {
            WorkspaceId     = req.WorkspaceId,
            CreatedByUserId = UserId,
            Name            = req.Name,
            KeyHash         = hash,
            Prefix          = prefix,
            Scope           = req.Scope,
            ExpiresAt       = req.ExpiresAt
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new {
            key.Id, key.Name, key.Prefix, key.Scope, key.ExpiresAt,
            WorkspaceId = key.WorkspaceId,
            RawKey      = rawKey,
            Warning     = "Store this key safely. It will not be shown again."
        });
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var key = await db.ApiKeys.FindAsync(id);
        if (key is null) return NotFound();
        if (!await CanManageWorkspace(key.WorkspaceId)) return Forbid();

        key.IsActive  = false;
        key.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { key.Id, key.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var key = await db.ApiKeys.FindAsync(id);
        if (key is null) return NotFound();
        if (!await CanManageWorkspace(key.WorkspaceId)) return Forbid();

        db.ApiKeys.Remove(key);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateApiKeyRequest(
    [Required] Guid WorkspaceId,
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    ApiKeyScope Scope = ApiKeyScope.ReadOnly,
    DateTime? ExpiresAt = null
);
