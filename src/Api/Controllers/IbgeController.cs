using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/ibge")]
[Produces("application/json")]
[Authorize]
public class IbgeController(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config) : ControllerBase
{
    private record IbgeMunicipioResponse(int Id, string Nome);

    /// <summary>Lista todas as UFs (cache local do IBGE)</summary>
    [HttpGet("ufs")]
    public async Task<IActionResult> GetUfs() =>
        Ok(await db.Ufs
            .OrderBy(u => u.Nome)
            .Select(u => new { u.Id, u.Sigla, u.Nome })
            .ToListAsync());

    /// <summary>Lista municípios de uma UF pela sigla; busca IBGE API se só a capital está em cache</summary>
    [HttpGet("ufs/{sigla}/municipios")]
    public async Task<IActionResult> GetMunicipios(string sigla)
    {
        var uf = await db.Ufs.FirstOrDefaultAsync(u =>
            u.Sigla.ToUpper() == sigla.ToUpper());

        if (uf is null)
            return NotFound(new { error = $"UF '{sigla}' não encontrada" });

        var municipios = await db.Municipios
            .Where(m => m.UfId == uf.Id)
            .OrderBy(m => m.Nome)
            .ToListAsync();

        if (municipios.Count <= 1)
        {
            var baseUrl = config["Ibge:BaseUrl"] ?? "https://servicodados.ibge.gov.br/api/v1";

            try
            {
                var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var ibgeMunicipios = await client.GetFromJsonAsync<IbgeMunicipioResponse[]>(
                    $"{baseUrl}/localidades/estados/{sigla}/municipios");

                if (ibgeMunicipios is not null)
                {
                    var existingIds = municipios.Select(m => m.Id).ToHashSet();

                    foreach (var m in ibgeMunicipios)
                    {
                        if (!existingIds.Contains(m.Id))
                        {
                            db.Municipios.Add(new Municipio
                            {
                                Id = m.Id,
                                Nome = m.Nome,
                                UfId = uf.Id
                            });
                        }
                    }

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch
                    {
                        // ignore duplicates on concurrent requests
                    }

                    municipios = await db.Municipios
                        .Where(m => m.UfId == uf.Id)
                        .OrderBy(m => m.Nome)
                        .ToListAsync();
                }
            }
            catch
            {
                // IBGE API unavailable — return what we have
            }
        }

        return Ok(municipios.Select(m => new { m.Id, m.Nome }));
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
            return Ok(new
            {
                valid = false,
                error = $"UF '{uf}' não encontrada"
            });

        var mun = await db.Municipios.FirstOrDefaultAsync(m =>
            m.UfId == ufEntity.Id &&
            m.Nome.ToLower() == municipio.ToLower());

        return Ok(mun is null
            ? new
            {
                valid = false,
                municipioNome = (string?)null,
                uf = ufEntity.Sigla,
                ibgeCode = (int?)null,
                error = $"Município '{municipio}' não encontrado em {uf.ToUpper()}"
            }
            : new
            {
                valid = true,
                municipioNome = mun.Nome,
                uf = ufEntity.Sigla,
                ibgeCode = (int?)mun.Id,
                error = (string?)null
            });
    }
}