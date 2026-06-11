using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

// ── Input Products (catálogo) ─────────────────────────────────────────────────
public record CreateInputProductRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Required] InputType Type,
    [Required, StringLength(20)] string Unit,
    string? ActiveIngredient,
    string? RegistrationNumber
);

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class InputProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] InputType? type = null)
    {
        var query = db.InputProducts.AsQueryable();
        if (type.HasValue) query = query.Where(i => i.Type == type);
        return Ok(await query.OrderBy(i => i.Name).Select(i => new {
            i.Id, i.Name, i.Type, i.Unit, i.ActiveIngredient, i.RegistrationNumber, i.CreatedAt
        }).ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var i = await db.InputProducts.FindAsync(id);
        return i is null ? NotFound() : Ok(new {
            i.Id, i.Name, i.Type, i.Unit, i.ActiveIngredient, i.RegistrationNumber, i.CreatedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateInputProductRequest req)
    {
        var product = new InputProduct
        {
            Name               = req.Name,
            Type               = req.Type,
            Unit               = req.Unit,
            ActiveIngredient   = req.ActiveIngredient,
            RegistrationNumber = req.RegistrationNumber
        };
        db.InputProducts.Add(product);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new { product.Id, product.Name });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateInputProductRequest req)
    {
        var product = await db.InputProducts.FindAsync(id);
        if (product is null) return NotFound();
        product.Name = req.Name; product.Type = req.Type; product.Unit = req.Unit;
        product.ActiveIngredient = req.ActiveIngredient; product.RegistrationNumber = req.RegistrationNumber;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { product.Id, product.Name });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await db.InputProducts.FindAsync(id);
        if (product is null) return NotFound();
        if (await db.StockItems.AnyAsync(s => s.InputProductId == id))
            return BadRequest(new ErrorResponse("Product is in stock, remove stock items first"));
        db.InputProducts.Remove(product);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
