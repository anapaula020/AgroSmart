using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class WorkspacesController(AppDbContext db, UserManager<IdentityUser> userManager) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ── GET /api/v1/workspaces ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var workspaces = await db.WorkspaceMembers
            .Where(m => m.UserId == UserId)
            .Include(m => m.Workspace)
            .ThenInclude(w => w!.Members)
            .Select(m => new
            {
                m.Workspace!.Id,
                m.Workspace.Name,
                m.Workspace.Slug,
                m.Workspace.Description,
                m.Workspace.OwnerId,
                m.Workspace.CreatedAt,
                myRole      = m.Role.ToString(),
                memberCount = m.Workspace.Members.Count,
                isOwner     = m.Workspace.OwnerId == UserId,
            })
            .ToListAsync();

        if (User.IsInRole(Roles.Admin))
        {
            var adminExtra = await db.Workspaces
                .Where(w => !db.WorkspaceMembers.Any(m => m.WorkspaceId == w.Id && m.UserId == UserId))
                .Select(w => new
                {
                    w.Id, w.Name, w.Slug, w.Description, w.OwnerId, w.CreatedAt,
                    myRole      = "Admin",
                    memberCount = w.Members.Count,
                    isOwner     = false,
                })
                .ToListAsync();
            return Ok(workspaces.Concat(adminExtra));
        }

        return Ok(workspaces);
    }

    // ── GET /api/v1/workspaces/{id} ───────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ws = await db.Workspaces
            .Include(w => w.Members)
            .Include(w => w.Invites.Where(i => i.Status == InviteStatus.Pending))
            .Include(w => w.Properties)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (ws is null) return NotFound();
        if (!await CanAccess(ws)) return Forbid();

        var users = await userManager.Users.ToListAsync();

        return Ok(new
        {
            ws.Id, ws.Name, ws.Slug, ws.Description, ws.OwnerId, ws.CreatedAt,
            members = ws.Members.Select(m => new
            {
                m.UserId,
                email = users.FirstOrDefault(u => u.Id == m.UserId)?.Email,
                role  = m.Role.ToString(),
                m.JoinedAt,
                isCurrent = m.UserId == UserId,
                isOwner   = ws.OwnerId == m.UserId,
            }),
            invites = ws.Invites.Select(i => new
            {
                i.Id, i.InvitedEmail, role = i.Role.ToString(),
                i.Token, status = i.Status.ToString(), i.CreatedAt, i.ExpiresAt,
            }),
            properties = ws.Properties.Select(p => new { p.Id, p.Name, p.TotalAreaHa }),
        });
    }

    // ── POST /api/v1/workspaces ───────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Nome é obrigatório." });

        var slug = Slugify(req.Name);
        if (await db.Workspaces.AnyAsync(w => w.Slug == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..4]}";

        var ws = new Workspace
        {
            OwnerId     = UserId,
            Name        = req.Name.Trim(),
            Slug        = slug,
            Description = req.Description?.Trim(),
        };
        db.Workspaces.Add(ws);

        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = ws.Id,
            UserId      = UserId,
            Role        = WorkspaceRole.Owner,
            JoinedAt    = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return Ok(new { ws.Id, ws.Name, ws.Slug });
    }

    // ── PUT /api/v1/workspaces/{id} ───────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest req)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        ws.Name        = req.Name?.Trim() ?? ws.Name;
        ws.Description = req.Description?.Trim() ?? ws.Description;
        await db.SaveChangesAsync();
        return Ok(new { ws.Id, ws.Name });
    }

    // ── DELETE /api/v1/workspaces/{id} ────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        db.Workspaces.Remove(ws);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/v1/workspaces/{id}/members ──────────────────────────────────
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest req)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        var target = await userManager.FindByIdAsync(req.UserId);
        if (target is null) return BadRequest(new { message = "Usuário não encontrado." });

        if (await db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == id && m.UserId == req.UserId))
            return Conflict(new { message = "Usuário já é membro deste workspace." });

        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = id,
            UserId      = req.UserId,
            Role        = Enum.TryParse<WorkspaceRole>(req.Role, true, out var r) ? r : WorkspaceRole.Produtor,
            JoinedAt    = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── PUT /api/v1/workspaces/{id}/members/{userId}/role ────────────────────
    [HttpPut("{id:guid}/members/{userId}/role")]
    public async Task<IActionResult> ChangeMemberRole(Guid id, string userId, [FromBody] ChangeMemberRoleRequest req)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();
        if (userId == ws.OwnerId && !User.IsInRole(Roles.Admin))
            return BadRequest(new { message = "Não é possível alterar o role do proprietário." });

        var member = await db.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);
        if (member is null) return NotFound();

        if (!Enum.TryParse<WorkspaceRole>(req.Role, true, out var role))
            return BadRequest(new { message = "Role inválido." });

        member.Role = role;
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── DELETE /api/v1/workspaces/{id}/members/{userId} ──────────────────────
    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();
        if (userId == ws.OwnerId)
            return BadRequest(new { message = "Não é possível remover o proprietário do workspace." });

        var member = await db.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);
        if (member is null) return NotFound();

        db.WorkspaceMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/v1/workspaces/{id}/invites ─────────────────────────────────
    [HttpPost("{id:guid}/invites")]
    public async Task<IActionResult> CreateInvite(Guid id, [FromBody] CreateInviteRequest req)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email é obrigatório." });

        var existing = await db.WorkspaceInvites.FirstOrDefaultAsync(i =>
            i.WorkspaceId == id && i.InvitedEmail == req.Email && i.Status == InviteStatus.Pending);
        if (existing is not null)
            return Conflict(new { message = "Já existe um convite pendente para este email." });

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
            .Replace("+", "").Replace("/", "").Replace("=", "")[..32];

        var invite = new WorkspaceInvite
        {
            WorkspaceId     = id,
            InvitedEmail    = req.Email.Trim().ToLower(),
            Role            = Enum.TryParse<WorkspaceRole>(req.Role, true, out var r) ? r : WorkspaceRole.Tecnico,
            Token           = token,
            Status          = InviteStatus.Pending,
            InvitedByUserId = UserId,
            ExpiresAt       = DateTime.UtcNow.AddDays(7),
        };
        db.WorkspaceInvites.Add(invite);
        await db.SaveChangesAsync();

        return Ok(new { invite.Id, invite.Token, invite.InvitedEmail, role = invite.Role.ToString(), invite.ExpiresAt });
    }

    // ── DELETE /api/v1/workspaces/{id}/invites/{inviteId} ────────────────────
    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> CancelInvite(Guid id, Guid inviteId)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        var invite = await db.WorkspaceInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.WorkspaceId == id);
        if (invite is null) return NotFound();

        invite.Status = InviteStatus.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/v1/workspaces/join ─────────────────────────────────────────
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinWorkspaceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { message = "Token é obrigatório." });

        var invite = await db.WorkspaceInvites
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i =>
                i.Token == req.Token &&
                i.Status == InviteStatus.Pending &&
                (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow));

        if (invite is null)
            return BadRequest(new { message = "Convite inválido ou expirado." });

        if (await db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == invite.WorkspaceId && m.UserId == UserId))
            return Conflict(new { message = "Você já é membro deste workspace." });

        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = invite.WorkspaceId,
            UserId      = UserId,
            Role        = invite.Role,
            JoinedAt    = DateTime.UtcNow,
        });

        invite.Status     = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { workspaceId = invite.WorkspaceId, workspaceName = invite.Workspace?.Name });
    }

    // ── PUT /api/v1/workspaces/{id}/properties/{propId} ──────────────────────
    [HttpPut("{id:guid}/properties/{propId:guid}")]
    public async Task<IActionResult> AssignProperty(Guid id, Guid propId)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        var prop = await db.RuralProperties.FindAsync(propId);
        if (prop is null) return NotFound();
        if (!User.IsManager() && prop.OwnerId != UserId) return Forbid();

        prop.WorkspaceId = id;
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── DELETE /api/v1/workspaces/{id}/properties/{propId} ───────────────────
    [HttpDelete("{id:guid}/properties/{propId:guid}")]
    public async Task<IActionResult> UnassignProperty(Guid id, Guid propId)
    {
        var ws = await db.Workspaces.FindAsync(id);
        if (ws is null) return NotFound();
        if (!await IsOwnerOrAdmin(ws)) return Forbid();

        var prop = await db.RuralProperties.FindAsync(propId);
        if (prop is null || prop.WorkspaceId != id) return NotFound();

        prop.WorkspaceId = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<bool> CanAccess(Workspace ws) =>
        User.IsInRole(Roles.Admin) ||
        ws.OwnerId == UserId ||
        await db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == ws.Id && m.UserId == UserId);

    private async Task<bool> IsOwnerOrAdmin(Workspace ws) =>
        User.IsInRole(Roles.Admin) || ws.OwnerId == UserId ||
        await db.WorkspaceMembers.AnyAsync(m =>
            m.WorkspaceId == ws.Id && m.UserId == UserId &&
            (m.Role == WorkspaceRole.Owner || m.Role == WorkspaceRole.Agronomo));

    private static string Slugify(string name)
    {
        var slug = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) slug.Append(c);
            else if (c == ' ' || c == '-') slug.Append('-');
        }
        return slug.ToString().Trim('-');
    }
}

public record CreateWorkspaceRequest(string Name, string? Description);
public record UpdateWorkspaceRequest(string? Name, string? Description);
public record AddMemberRequest(string UserId, string Role);
public record ChangeMemberRoleRequest(string Role);
public record CreateInviteRequest(string Email, string Role);
public record JoinWorkspaceRequest(string Token);
