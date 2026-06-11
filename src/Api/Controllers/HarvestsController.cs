using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record CreateHarvestRequest(
    [Required] Guid FieldId,
    [Required] Guid CultureId,
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Required] DateOnly PlantingDate,
    [Required] DateOnly ExpectedHarvestDate,
    [Range(0, double.MaxValue)] decimal EstimatedYieldTons
);

public record UpdateHarvestRequest(
    string? Name,
    DateOnly? ExpectedHarvestDate,
    DateOnly? ActualHarvestDate,
    HarvestStatus? Status,
    decimal? EstimatedYieldTons,
    decimal? ActualYieldTons
);

public record CreateProductivityRecordRequest(
    [Range(0, double.MaxValue)] decimal YieldTonsPerHa,
    string? Notes
);

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class HarvestsController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin  => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? fieldId = null,
        [FromQuery] HarvestStatus? status = null)
    {
        var query = db.Harvests
            .Include(h => h.Field).ThenInclude(f => f!.Property)
            .Include(h => h.Culture)
            .AsQueryable();

        if (!IsAdmin)
            query = query.Where(h => h.Field!.Property!.OwnerId == UserId);
        if (fieldId.HasValue)
            query = query.Where(h => h.FieldId == fieldId);
        if (status.HasValue)
            query = query.Where(h => h.Status == status);

        var result = await query.OrderByDescending(h => h.PlantingDate).Select(h => new {
            h.Id, h.Name, h.Status, h.PlantingDate, h.ExpectedHarvestDate,
            h.ActualHarvestDate, h.EstimatedYieldTons, h.ActualYieldTons,
            h.ResponsibleUserId, h.CreatedAt,
            Field    = new { h.Field!.Id, h.Field.Name, PropertyId = h.Field.PropertyId },
            Culture  = new { h.Culture!.Id, h.Culture.CommonName }
        }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var h = await db.Harvests
            .Include(x => x.Field).ThenInclude(f => f!.Property)
            .Include(x => x.Culture)
            .Include(x => x.ProductivityRecords)
            .Include(x => x.HarvestInputs).ThenInclude(hi => hi.StockMovement)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (h is null) return NotFound();
        if (!IsAdmin && h.Field!.Property!.OwnerId != UserId) return Forbid();

        return Ok(new {
            h.Id, h.Name, h.Status, h.PlantingDate, h.ExpectedHarvestDate,
            h.ActualHarvestDate, h.EstimatedYieldTons, h.ActualYieldTons,
            h.ResponsibleUserId, h.CreatedAt,
            Field   = new { h.Field!.Id, h.Field.Name },
            Culture = new { h.Culture!.Id, h.Culture.CommonName, h.Culture.AverageCycleDays },
            ProductivityRecords = h.ProductivityRecords.OrderByDescending(p => p.RecordedAt).Select(p => new {
                p.Id, p.YieldTonsPerHa, p.RecordedAt, p.Notes
            }),
            InputsCount = h.HarvestInputs.Count
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHarvestRequest req)
    {
        var field = await db.Fields.Include(f => f.Property).FirstOrDefaultAsync(f => f.Id == req.FieldId);
        if (field is null) return BadRequest(new ErrorResponse("Field not found"));
        if (!IsAdmin && field.Property!.OwnerId != UserId) return Forbid();

        if (!await db.Cultures.AnyAsync(c => c.Id == req.CultureId))
            return BadRequest(new ErrorResponse("Culture not found"));

        var harvest = new Harvest
        {
            FieldId              = req.FieldId,
            CultureId            = req.CultureId,
            ResponsibleUserId    = UserId,
            Name                 = req.Name,
            PlantingDate         = req.PlantingDate,
            ExpectedHarvestDate  = req.ExpectedHarvestDate,
            EstimatedYieldTons   = req.EstimatedYieldTons
        };
        db.Harvests.Add(harvest);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = harvest.Id }, new { harvest.Id, harvest.Name });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHarvestRequest req)
    {
        var h = await db.Harvests.Include(x => x.Field).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        if (!IsAdmin && h.Field!.Property!.OwnerId != UserId) return Forbid();

        if (req.Name is not null)                    h.Name                = req.Name;
        if (req.ExpectedHarvestDate.HasValue)        h.ExpectedHarvestDate = req.ExpectedHarvestDate.Value;
        if (req.ActualHarvestDate.HasValue)          h.ActualHarvestDate   = req.ActualHarvestDate;
        if (req.Status.HasValue)                     h.Status              = req.Status.Value;
        if (req.EstimatedYieldTons.HasValue)         h.EstimatedYieldTons  = req.EstimatedYieldTons.Value;
        if (req.ActualYieldTons.HasValue)            h.ActualYieldTons     = req.ActualYieldTons;
        h.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { h.Id, h.Name, h.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var h = await db.Harvests.Include(x => x.Field).ThenInclude(f => f!.Property)
            .Include(x => x.HarvestInputs)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        if (!IsAdmin && h.Field!.Property!.OwnerId != UserId) return Forbid();
        if (h.HarvestInputs.Any()) return BadRequest(new ErrorResponse("Harvest has applied inputs, remove them first"));

        db.Harvests.Remove(h);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Productivity records (sub-resource) ───────────────────────────────────
    [HttpGet("{harvestId:guid}/productivity")]
    public async Task<IActionResult> GetProductivity(Guid harvestId)
    {
        var h = await db.Harvests.Include(x => x.Field).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(x => x.Id == harvestId);
        if (h is null) return NotFound();
        if (!IsAdmin && h.Field!.Property!.OwnerId != UserId) return Forbid();

        var records = await db.ProductivityRecords
            .Where(p => p.HarvestId == harvestId)
            .OrderByDescending(p => p.RecordedAt)
            .Select(p => new { p.Id, p.YieldTonsPerHa, p.RecordedAt, p.Notes })
            .ToListAsync();
        return Ok(records);
    }

    [HttpPost("{harvestId:guid}/productivity")]
    public async Task<IActionResult> AddProductivity(Guid harvestId, [FromBody] CreateProductivityRecordRequest req)
    {
        var h = await db.Harvests.Include(x => x.Field).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(x => x.Id == harvestId);
        if (h is null) return NotFound();
        if (!IsAdmin && h.Field!.Property!.OwnerId != UserId) return Forbid();

        var record = new ProductivityRecord
        {
            HarvestId      = harvestId,
            YieldTonsPerHa = req.YieldTonsPerHa,
            Notes          = req.Notes,
            RecordedAt     = DateTime.UtcNow
        };
        db.ProductivityRecords.Add(record);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetProductivity), new { harvestId },
            new { record.Id, record.YieldTonsPerHa, record.RecordedAt });
    }

    [HttpDelete("{harvestId:guid}/productivity/{recordId:guid}")]
    public async Task<IActionResult> DeleteProductivity(Guid harvestId, Guid recordId)
    {
        var record = await db.ProductivityRecords
            .FirstOrDefaultAsync(p => p.Id == recordId && p.HarvestId == harvestId);
        if (record is null) return NotFound();

        db.ProductivityRecords.Remove(record);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
