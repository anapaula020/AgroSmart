using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record UpdateStockItemRequest(decimal? MinimumStock, decimal? UnitCost);

public record CreateStockItemRequest(
    [Required] Guid PropertyId,
    [Required] Guid InputProductId,
    [Range(0, double.MaxValue)] decimal MinimumStock,
    [Range(0, double.MaxValue)] decimal UnitCost
);

public record CreateMovementRequest(
    [Required] MovementType Type,
    [Range(0.001, double.MaxValue)] decimal Quantity,
    string? Reason
);

[ApiController]
[Route("api/v1/stock")]
[Produces("application/json")]
[Authorize]
public class StockController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<bool> CanAccessProperty(Guid propertyId)
    {
        var prop = await db.RuralProperties.FindAsync(propertyId);
        return prop is not null && (User.IsManager() || prop.OwnerId == UserId);
    }

    // ── Stock Items ───────────────────────────────────────────────────────────
    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] Guid? propertyId = null)
    {
        var query = db.StockItems
            .Include(s => s.Property)
            .Include(s => s.InputProduct)
            .AsQueryable();

        if (!User.IsManager())
            query = query.Where(s => s.Property!.OwnerId == UserId);
        if (propertyId.HasValue)
            query = query.Where(s => s.PropertyId == propertyId);

        var result = await query.OrderBy(s => s.InputProduct!.Name).Select(s => new {
            s.Id, s.QuantityInStock, s.MinimumStock, s.UnitCost,
            IsLow = s.QuantityInStock <= s.MinimumStock,
            Property     = new { s.Property!.Id, s.Property.Name },
            InputProduct = new { s.InputProduct!.Id, s.InputProduct.Name, s.InputProduct.Type, s.InputProduct.Unit }
        }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var s = await db.StockItems
            .Include(x => x.Property)
            .Include(x => x.InputProduct)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (s is null) return NotFound();
        if (!User.IsManager() && s.Property!.OwnerId != UserId) return Forbid();

        return Ok(new {
            s.Id, s.QuantityInStock, s.MinimumStock, s.UnitCost,
            IsLow = s.QuantityInStock <= s.MinimumStock,
            Property     = new { s.Property!.Id, s.Property.Name },
            InputProduct = new { s.InputProduct!.Id, s.InputProduct.Name, s.InputProduct.Type, s.InputProduct.Unit },
            RecentMovements = s.Movements.OrderByDescending(m => m.MovedAt).Take(10).Select(m => new {
                m.Id, m.Type, m.Quantity, m.Reason, m.MovedAt
            })
        });
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateStockItemRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        if (!await CanAccessProperty(req.PropertyId)) return Forbid();

        if (!await db.InputProducts.AnyAsync(i => i.Id == req.InputProductId))
            return BadRequest(new ErrorResponse("InputProduct not found"));

        if (await db.StockItems.AnyAsync(s => s.PropertyId == req.PropertyId && s.InputProductId == req.InputProductId))
            return BadRequest(new ErrorResponse("Stock item already exists for this product in this property"));

        var item = new StockItem
        {
            PropertyId     = req.PropertyId,
            InputProductId = req.InputProductId,
            MinimumStock   = req.MinimumStock,
            UnitCost       = req.UnitCost,
            QuantityInStock = 0
        };
        db.StockItems.Add(item);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, new { item.Id });
    }

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateStockItemRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        var item = await db.StockItems.Include(s => s.Property).FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();
        if (!User.IsManager() && item.Property!.OwnerId != UserId) return Forbid();

        if (req.MinimumStock.HasValue) item.MinimumStock = req.MinimumStock.Value;
        if (req.UnitCost.HasValue)     item.UnitCost     = req.UnitCost.Value;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.MinimumStock, item.UnitCost });
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        if (!User.IsManager()) return Forbid();
        var item = await db.StockItems.Include(s => s.Property).Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();
        if (!User.IsManager() && item.Property!.OwnerId != UserId) return Forbid();
        if (item.Movements.Any()) return BadRequest(new ErrorResponse("Stock item has movements, cannot delete"));

        db.StockItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Stock Movements ───────────────────────────────────────────────────────
    [HttpGet("items/{itemId:guid}/movements")]
    public async Task<IActionResult> GetMovements(Guid itemId)
    {
        var item = await db.StockItems.Include(s => s.Property).FirstOrDefaultAsync(s => s.Id == itemId);
        if (item is null) return NotFound();
        if (!User.IsManager() && item.Property!.OwnerId != UserId) return Forbid();

        var movements = await db.StockMovements
            .Where(m => m.StockItemId == itemId)
            .OrderByDescending(m => m.MovedAt)
            .Select(m => new { m.Id, m.Type, m.Quantity, m.Reason, m.MovedAt, m.UserId })
            .ToListAsync();

        return Ok(movements);
    }

    [HttpPost("items/{itemId:guid}/movements")]
    public async Task<IActionResult> AddMovement(Guid itemId, [FromBody] CreateMovementRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        var item = await db.StockItems.Include(s => s.Property).FirstOrDefaultAsync(s => s.Id == itemId);
        if (item is null) return NotFound();
        if (!User.IsManager() && item.Property!.OwnerId != UserId) return Forbid();

        // Valida saldo para saída
        if (req.Type == MovementType.Saida && item.QuantityInStock < req.Quantity)
            return BadRequest(new ErrorResponse($"Insufficient stock. Available: {item.QuantityInStock}"));

        var movement = new StockMovement
        {
            StockItemId = itemId,
            UserId      = UserId,
            Type        = req.Type,
            Quantity    = req.Quantity,
            Reason      = req.Reason,
            MovedAt     = DateTime.UtcNow
        };
        db.StockMovements.Add(movement);

        // Atualiza saldo
        item.QuantityInStock += req.Type switch
        {
            MovementType.Entrada => req.Quantity,
            MovementType.Saida   => -req.Quantity,
            MovementType.Ajuste  => req.Quantity - item.QuantityInStock,
            _ => 0
        };
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMovements), new { itemId },
            new { movement.Id, movement.Type, movement.Quantity, NewBalance = item.QuantityInStock });
    }
}
