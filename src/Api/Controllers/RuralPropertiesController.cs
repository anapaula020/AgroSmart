using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record AddressRequest(
    [Required] string Cep,
    [Required] string Logradouro,
    string? Complemento,
    [Required] string Bairro,
    [Required] string Municipio,
    [Required, StringLength(2, MinimumLength = 2)] string Uf,
    int? IbgeCode
);

public record CreatePropertyRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Required] AddressRequest Address,
    string? CarNumber,
    [Range(0, double.MaxValue)] decimal TotalAreaHa,
    [Range(0, double.MaxValue)] decimal VegetationAreaHa
);

public record UpdatePropertyRequest(
    string? Name,
    string? CarNumber,
    decimal? TotalAreaHa,
    decimal? VegetationAreaHa
);

[ApiController]
[Route("api/v1/rural-properties")]
[Produces("application/json")]
[Authorize]
public class RuralPropertiesController(AppDbContext db, Api.Services.IbgeService ibge) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = db.RuralProperties.Include(p => p.Address).Include(p => p.Fields).AsQueryable();
        if (!User.IsManager()) query = query.Where(p => p.OwnerId == UserId);

        var result = await query.OrderBy(p => p.Name).Select(p => new {
            p.Id, p.Name, p.CarNumber, p.TotalAreaHa, p.VegetationAreaHa,
            p.OwnerId, p.CreatedAt,
            FieldCount = p.Fields.Count,
            Address = p.Address == null ? null : new {
                p.Address.Municipio, p.Address.Uf, p.Address.Cep
            }
        }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var p = await db.RuralProperties
            .Include(x => x.Address)
            .Include(x => x.Fields).ThenInclude(f => f.SoilType)
            .Include(x => x.Fields).ThenInclude(f => f.IrrigationType)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        return Ok(new {
            p.Id, p.Name, p.CarNumber, p.TotalAreaHa, p.VegetationAreaHa, p.OwnerId, p.CreatedAt,
            Address = p.Address is null ? null : new {
                p.Address.Id, p.Address.Cep, p.Address.Logradouro, p.Address.Complemento,
                p.Address.Bairro, p.Address.Municipio, p.Address.Uf,
                p.Address.IbgeCode
            },
            Fields = p.Fields.Select(f => new {
                f.Id, f.Name, f.AreaHa,
                SoilType       = f.SoilType?.Name,
                IrrigationType = f.IrrigationType?.Name
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        // Valida município/UF via IBGE (não-bloqueante se API indisponível)
        var ibgeResult = await ibge.ValidateMunicipioAsync(db, req.Address.Municipio, req.Address.Uf);
        if (!ibgeResult.Valid && ibgeResult.Error is not null && !ibgeResult.Error.Contains("unavailable"))
            return BadRequest(new ErrorResponse(ibgeResult.Error));

        var address = new Address
        {
            Cep = req.Address.Cep, Logradouro = req.Address.Logradouro,
            Complemento = req.Address.Complemento, Bairro = req.Address.Bairro,
            Municipio = req.Address.Municipio, Uf = req.Address.Uf,
            IbgeCode  = ibgeResult.IbgeCode ?? req.Address.IbgeCode
        };
        db.Addresses.Add(address);
        await db.SaveChangesAsync();

        var property = new RuralProperty
        {
            OwnerId           = UserId,
            AddressId         = address.Id,
            Name              = req.Name,
            CarNumber         = req.CarNumber,
            TotalAreaHa       = req.TotalAreaHa,
            VegetationAreaHa  = req.VegetationAreaHa
        };
        db.RuralProperties.Add(property);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = property.Id },
            new { property.Id, property.Name });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePropertyRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        var p = await db.RuralProperties.FindAsync(id);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        if (req.Name is not null)             p.Name             = req.Name;
        if (req.CarNumber is not null)        p.CarNumber        = req.CarNumber;
        if (req.TotalAreaHa.HasValue)         p.TotalAreaHa      = req.TotalAreaHa.Value;
        if (req.VegetationAreaHa.HasValue)    p.VegetationAreaHa = req.VegetationAreaHa.Value;
        p.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { p.Id, p.Name });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!User.IsManager()) return Forbid();
        var p = await db.RuralProperties.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();
        if (p.Fields.Any()) return BadRequest(new ErrorResponse("Remove all fields before deleting the property"));

        db.RuralProperties.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Fields (sub-resource) ─────────────────────────────────────────────────
    [HttpGet("{propertyId:guid}/fields")]
    public async Task<IActionResult> GetFields(Guid propertyId)
    {
        var p = await db.RuralProperties.FindAsync(propertyId);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        var fields = await db.Fields
            .Include(f => f.SoilType).Include(f => f.IrrigationType)
            .Where(f => f.PropertyId == propertyId)
            .Select(f => new {
                f.Id, f.Name, f.AreaHa, f.PolygonGeoJson,
                SoilTypeId = f.SoilTypeId, SoilType = f.SoilType!.Name,
                IrrigationTypeId = f.IrrigationTypeId, IrrigationType = f.IrrigationType!.Name,
                f.CreatedAt
            }).ToListAsync();

        return Ok(fields);
    }

    [HttpPost("{propertyId:guid}/fields")]
    public async Task<IActionResult> CreateField(Guid propertyId, [FromBody] CreateFieldRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        var p = await db.RuralProperties.FindAsync(propertyId);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        if (!await db.SoilTypes.AnyAsync(s => s.Id == req.SoilTypeId))
            return BadRequest(new ErrorResponse("SoilType not found"));
        if (!await db.IrrigationTypes.AnyAsync(i => i.Id == req.IrrigationTypeId))
            return BadRequest(new ErrorResponse("IrrigationType not found"));

        var field = new Field
        {
            PropertyId       = propertyId,
            SoilTypeId       = req.SoilTypeId,
            IrrigationTypeId = req.IrrigationTypeId,
            Name             = req.Name,
            AreaHa           = req.AreaHa,
            PolygonGeoJson   = req.PolygonGeoJson
        };
        db.Fields.Add(field);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetFields), new { propertyId }, new { field.Id, field.Name });
    }

    [HttpPut("{propertyId:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid propertyId, Guid fieldId, [FromBody] UpdateFieldRequest req)
    {
        if (!User.CanWrite()) return Forbid();
        var p = await db.RuralProperties.FindAsync(propertyId);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        var field = await db.Fields.FirstOrDefaultAsync(f => f.Id == fieldId && f.PropertyId == propertyId);
        if (field is null) return NotFound();

        if (req.Name is not null)                  field.Name             = req.Name;
        if (req.AreaHa.HasValue)                   field.AreaHa           = req.AreaHa.Value;
        if (req.PolygonGeoJson is not null)        field.PolygonGeoJson   = req.PolygonGeoJson;
        if (req.SoilTypeId.HasValue)               field.SoilTypeId       = req.SoilTypeId.Value;
        if (req.IrrigationTypeId.HasValue)         field.IrrigationTypeId = req.IrrigationTypeId.Value;
        field.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { field.Id, field.Name });
    }

    [HttpDelete("{propertyId:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteField(Guid propertyId, Guid fieldId)
    {
        if (!User.IsManager()) return Forbid();
        var p = await db.RuralProperties.FindAsync(propertyId);
        if (p is null) return NotFound();
        if (!User.IsManager() && p.OwnerId != UserId) return Forbid();

        var field = await db.Fields.Include(f => f.Harvests)
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.PropertyId == propertyId);
        if (field is null) return NotFound();
        if (field.Harvests.Any()) return BadRequest(new ErrorResponse("Field has harvests, remove them first"));

        db.Fields.Remove(field);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateFieldRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Required] Guid SoilTypeId,
    [Required] Guid IrrigationTypeId,
    [Range(0, double.MaxValue)] decimal AreaHa,
    string? PolygonGeoJson
);

public record UpdateFieldRequest(
    string? Name,
    decimal? AreaHa,
    string? PolygonGeoJson,
    Guid? SoilTypeId,
    Guid? IrrigationTypeId
);
