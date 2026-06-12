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
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class IrrigationTypesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.IrrigationTypes.OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.Description }).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var e = await db.IrrigationTypes.FindAsync(id);
        return e is null ? NotFound() : Ok(new { e.Id, e.Name, e.Description });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] LookupRequest req)
    {
        var entity = new IrrigationType { Name = req.Name, Description = req.Description };
        db.IrrigationTypes.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id, entity.Name, entity.Description });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] LookupRequest req)
    {
        var entity = await db.IrrigationTypes.FindAsync(id);
        if (entity is null) return NotFound();
        entity.Name = req.Name; entity.Description = req.Description;
        await db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name, entity.Description });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await db.IrrigationTypes.FindAsync(id);
        if (entity is null) return NotFound();
        if (await db.Fields.AnyAsync(f => f.IrrigationTypeId == id))
            return BadRequest(new ErrorResponse("IrrigationType in use by fields"));
        db.IrrigationTypes.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
