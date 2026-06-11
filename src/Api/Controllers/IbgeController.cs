using Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/ibge")]
[Produces("application/json")]
[Authorize]
public class IbgeController(AppDbContext db) : ControllerBase
{
    /// <summary>Lista todas as UFs (cache local do IBGE)</summary>
    [HttpGet("ufs")]
    public async Task<IActionResult> GetUfs() =>
        Ok(await db.Ufs
            .OrderBy(u => u.Nome)
            .Select(u => new { u.Id, u.Sigla, u.Nome })
            .ToListAsync());

    /// <summary>Lista municípios de uma UF pela sigla</summary>
    [HttpGet("ufs/{sigla}/municipios")]
    public async Task<IActionResult> GetMunicipios(string sigla)
    {
        var uf = await db.Ufs.FirstOrDefaultAsync(u =>
            u.Sigla.ToUpper() == sigla.ToUpper());

        if (uf is null) return NotFound(new { error = $"UF '{sigla}' não encontrada" });

        var municipios = await db.Municipios
            .Where(m => m.UfId == uf.Id)
            .OrderBy(m => m.Nome)
            .Select(m => new { m.Id, m.Nome })
            .ToListAsync();

        return Ok(municipios);
    }

    /// <summary>Valida município + UF usando cache local</summary>
    [HttpGet("validate")]
    public async Task<IActionResult> Validate(
        [FromQuery] string municipio,
        [FromQuery] string uf)
    {
        if (string.IsNullOrWhiteSpace(municipio) || string.IsNullOrWhiteSpace(uf))
            return BadRequest(new { error = "municipio e uf são obrigatórios" });

        var ufEntity = await db.Ufs.FirstOrDefaultAsync(u =>
            u.Sigla.ToUpper() == uf.ToUpper());
        if (ufEntity is null)
            return Ok(new { valid = false, error = $"UF '{uf}' não encontrada" });

        var mun = await db.Municipios.FirstOrDefaultAsync(m =>
            m.UfId == ufEntity.Id &&
            m.Nome.ToLower() == municipio.ToLower());

        return Ok(mun is null
            ? new { valid = false, municipioNome = (string?)null, uf = ufEntity.Sigla, ibgeCode = (int?)null, error = $"Município '{municipio}' não encontrado em {uf.ToUpper()}" }
            : new { valid = true,  municipioNome = mun.Nome, uf = ufEntity.Sigla, ibgeCode = (int?)mun.Id, error = (string?)null });
    }
}
