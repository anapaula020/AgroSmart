using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers;

public record CreateCultureRequest(
    [Required, StringLength(150, MinimumLength = 2)] string CommonName,
    string? ScientificName,
    int? AverageCycleDays,
    decimal? MinTempCelsius,
    decimal? MaxTempCelsius,
    decimal? IdealRainfallMm
);

public record UpdateCultureRequest(
    string? CommonName,
    string? ScientificName,
    int? AverageCycleDays,
    decimal? MinTempCelsius,
    decimal? MaxTempCelsius,
    decimal? IdealRainfallMm
);

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class CulturesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Cultures.OrderBy(c => c.CommonName)
            .Select(c => new {
                c.Id, c.CommonName, c.ScientificName,
                c.AverageCycleDays, c.MinTempCelsius, c.MaxTempCelsius, c.IdealRainfallMm,
                c.CreatedAt
            }).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await db.Cultures.FindAsync(id);
        return c is null ? NotFound() : Ok(new {
            c.Id, c.CommonName, c.ScientificName,
            c.AverageCycleDays, c.MinTempCelsius, c.MaxTempCelsius, c.IdealRainfallMm,
            c.CreatedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCultureRequest req)
    {
        var culture = new Culture
        {
            CommonName       = req.CommonName,
            ScientificName   = req.ScientificName,
            AverageCycleDays = req.AverageCycleDays,
            MinTempCelsius   = req.MinTempCelsius,
            MaxTempCelsius   = req.MaxTempCelsius,
            IdealRainfallMm  = req.IdealRainfallMm
        };
        db.Cultures.Add(culture);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = culture.Id }, new { culture.Id, culture.CommonName });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCultureRequest req)
    {
        var culture = await db.Cultures.FindAsync(id);
        if (culture is null) return NotFound();

        if (req.CommonName is not null)        culture.CommonName       = req.CommonName;
        if (req.ScientificName is not null)    culture.ScientificName   = req.ScientificName;
        if (req.AverageCycleDays.HasValue)     culture.AverageCycleDays = req.AverageCycleDays;
        if (req.MinTempCelsius.HasValue)       culture.MinTempCelsius   = req.MinTempCelsius;
        if (req.MaxTempCelsius.HasValue)       culture.MaxTempCelsius   = req.MaxTempCelsius;
        if (req.IdealRainfallMm.HasValue)      culture.IdealRainfallMm  = req.IdealRainfallMm;
        culture.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { culture.Id, culture.CommonName, culture.ScientificName });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var culture = await db.Cultures.FindAsync(id);
        if (culture is null) return NotFound();
        if (await db.Harvests.AnyAsync(h => h.CultureId == id))
            return BadRequest(new ErrorResponse("Culture in use by harvests"));
        db.Cultures.Remove(culture);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
