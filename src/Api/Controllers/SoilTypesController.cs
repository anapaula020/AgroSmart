using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class SoilTypesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.SoilTypes.OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Description }).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var s = await db.SoilTypes.FindAsync(id);
        return s is null ? NotFound() : Ok(new { s.Id, s.Name, s.Description });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LookupRequest req)
    {
        var entity = new SoilType { Name = req.Name, Description = req.Description };
        db.SoilTypes.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id, entity.Name, entity.Description });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LookupRequest req)
    {
        var entity = await db.SoilTypes.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Name = req.Name; entity.Description = req.Description;
        await db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name, entity.Description });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await db.SoilTypes.FindAsync(id);
        if (entity is null) return NotFound();
        if (await db.Fields.AnyAsync(f => f.SoilTypeId == id))
            return BadRequest(new ErrorResponse("SoilType in use by fields"));
        db.SoilTypes.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
