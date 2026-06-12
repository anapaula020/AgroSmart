using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record CreateAlertRequest(
    [Required] AlertType     Type,
    [Required] AlertSeverity Severity,
    [Required, StringLength(200, MinimumLength = 2)] string Title,
    [Required] string Message,
    Guid? PropertyId  = null,
    Guid? HarvestId   = null,
    Guid? StockItemId = null,
    DateTime? ExpiresAt = null
);

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class AlertsController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? unreadOnly   = null,
        [FromQuery] AlertSeverity? severity = null,
        [FromQuery] AlertType? type    = null,
        [FromQuery] Guid? propertyId   = null,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.Alerts.AsQueryable();

        // Usuário vê alertas seus, das suas propriedades ou workspaces onde é membro
        if (!User.IsManager())
        {
            var wsIds = await db.WorkspaceMembers.Where(m => m.UserId == UserId).Select(m => m.WorkspaceId).ToListAsync();
            query = query.Where(a =>
                a.CreatedByUserId == UserId ||
                (a.PropertyId != null && db.RuralProperties.Any(p =>
                    p.Id == a.PropertyId && (
                        p.OwnerId == UserId ||
                        (p.WorkspaceId != null && wsIds.Contains(p.WorkspaceId.Value))))));
        }

        if (unreadOnly == true)  query = query.Where(a => !a.IsRead);
        if (severity.HasValue)   query = query.Where(a => a.Severity == severity);
        if (type.HasValue)       query = query.Where(a => a.Type == type);
        if (propertyId.HasValue) query = query.Where(a => a.PropertyId == propertyId);

        // Exclui expirados
        query = query.Where(a => a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new {
                a.Id, a.Type, a.Severity, a.Title, a.Message,
                a.IsRead, a.ReadAt, a.ExpiresAt, a.CreatedAt,
                a.PropertyId, a.HarvestId, a.StockItemId
            }).ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var a = await db.Alerts
            .Include(x => x.Property)
            .Include(x => x.Harvest)
            .Include(x => x.StockItem).ThenInclude(s => s!.InputProduct)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a is null) return NotFound();
        if (!User.IsManager() && a.CreatedByUserId != UserId) return Forbid();

        return Ok(new {
            a.Id, a.Type, a.Severity, a.Title, a.Message,
            a.IsRead, a.ReadAt, a.ExpiresAt, a.CreatedAt,
            Property  = a.Property  is null ? null : new { a.Property.Id,  a.Property.Name },
            Harvest   = a.Harvest   is null ? null : new { a.Harvest.Id,   a.Harvest.Name },
            StockItem = a.StockItem is null ? null : new {
                a.StockItem.Id, a.StockItem.QuantityInStock,
                Product = a.StockItem.InputProduct?.Name
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        // Valida referências opcionais
        if (req.PropertyId.HasValue && !await db.RuralProperties.AnyAsync(p => p.Id == req.PropertyId))
            return BadRequest(new ErrorResponse("Property not found"));
        if (req.HarvestId.HasValue && !await db.Harvests.AnyAsync(h => h.Id == req.HarvestId))
            return BadRequest(new ErrorResponse("Harvest not found"));
        if (req.StockItemId.HasValue && !await db.StockItems.AnyAsync(s => s.Id == req.StockItemId))
            return BadRequest(new ErrorResponse("StockItem not found"));

        var alert = new Alert
        {
            CreatedByUserId = UserId,
            Type            = req.Type,
            Severity        = req.Severity,
            Title           = req.Title,
            Message         = req.Message,
            PropertyId      = req.PropertyId,
            HarvestId       = req.HarvestId,
            StockItemId     = req.StockItemId,
            ExpiresAt       = req.ExpiresAt
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = alert.Id }, new { alert.Id, alert.Title });
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var a = await db.Alerts.FirstOrDefaultAsync(x => x.Id == id &&
            (User.IsManager() || x.CreatedByUserId == UserId));
        if (a is null) return NotFound();
        a.IsRead = true; a.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { a.Id, a.IsRead, a.ReadAt });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var query = db.Alerts.Where(a => !a.IsRead);
        if (!User.IsManager()) query = query.Where(a => a.CreatedByUserId == UserId);

        var alerts = await query.ToListAsync();
        var now    = DateTime.UtcNow;
        foreach (var a in alerts) { a.IsRead = true; a.ReadAt = now; }
        await db.SaveChangesAsync();
        return Ok(new { MarkedCount = alerts.Count });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!User.IsManager()) return Forbid();
        var a = await db.Alerts.FirstOrDefaultAsync(x => x.Id == id &&
            (User.IsManager() || x.CreatedByUserId == UserId));
        if (a is null) return NotFound();
        db.Alerts.Remove(a);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Auto-generate: verifica estoque baixo e cria alertas ─────────────────
    [HttpPost("check-stock")]
    [Authorize(Roles = $"{Api.Roles.Admin},{Api.Roles.Gestor}")]
    public async Task<IActionResult> CheckStockAlerts()
    {
        var lowItems = await db.StockItems
            .Include(s => s.Property)
            .Include(s => s.InputProduct)
            .Where(s => s.QuantityInStock <= s.MinimumStock)
            .ToListAsync();

        int created = 0;
        foreach (var item in lowItems)
        {
            // Evita duplicar alerta ativo para o mesmo item
            var exists = await db.Alerts.AnyAsync(a =>
                a.StockItemId == item.Id &&
                a.Type == AlertType.StockLow &&
                !a.IsRead);
            if (exists) continue;

            db.Alerts.Add(new Alert
            {
                CreatedByUserId = UserId,
                Type            = AlertType.StockLow,
                Severity        = item.QuantityInStock == 0 ? AlertSeverity.Critical : AlertSeverity.High,
                Title           = $"Estoque baixo: {item.InputProduct?.Name}",
                Message         = $"Propriedade \"{item.Property?.Name}\": estoque atual {item.QuantityInStock} {item.InputProduct?.Unit} (mínimo: {item.MinimumStock})",
                PropertyId      = item.PropertyId,
                StockItemId     = item.Id
            });
            created++;
        }

        await db.SaveChangesAsync();
        return Ok(new { Created = created, TotalLowItems = lowItems.Count });
    }
}
