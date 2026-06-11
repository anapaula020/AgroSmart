using System.Text.Json;
using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public record IbgeValidationResult(bool Valid, string? MunicipioNome, string? Uf, int? IbgeCode, string? Error);

public class IbgeService(
    IHttpClientFactory httpFactory,
    ILogger<IbgeService> logger)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private HttpClient Client => httpFactory.CreateClient("ibge");

    // ── Seed: baixa do IBGE e salva no banco ─────────────────────────────────
    public async Task SeedLocalidadesAsync(AppDbContext db)
    {
        if (await db.Ufs.AnyAsync())
        {
            logger.LogInformation("Localidades já populadas, pulando seed IBGE");
            return;
        }

        logger.LogInformation("Baixando UFs do IBGE...");

        List<IbgeUfDto> ufs;
        try
        {
            var resp = await Client.GetAsync("localidades/estados?orderBy=nome");
            resp.EnsureSuccessStatusCode();
            ufs = JsonSerializer.Deserialize<List<IbgeUfDto>>(
                await resp.Content.ReadAsStringAsync(), _json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning("Falha ao baixar UFs do IBGE: {Msg}", ex.Message);
            return;
        }

        foreach (var uf in ufs)
        {
            db.Ufs.Add(new Uf { Id = uf.Id, Sigla = uf.Sigla, Nome = uf.Nome });
        }
        await db.SaveChangesAsync();

        logger.LogInformation("{Count} UFs salvas. Baixando municípios...", ufs.Count);

        foreach (var uf in ufs)
        {
            try
            {
                var resp = await Client.GetAsync($"localidades/estados/{uf.Id}/municipios");
                if (!resp.IsSuccessStatusCode) continue;

                var muns = JsonSerializer.Deserialize<List<IbgeMunicipioDto>>(
                    await resp.Content.ReadAsStringAsync(), _json) ?? [];

                foreach (var m in muns)
                    db.Municipios.Add(new Municipio { Id = m.Id, Nome = m.Nome, UfId = uf.Id });

                await db.SaveChangesAsync();
                logger.LogDebug("{Uf}: {Count} municípios", uf.Sigla, muns.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Falha municípios {Uf}: {Msg}", uf.Sigla, ex.Message);
            }
        }

        logger.LogInformation("Seed IBGE concluído");
    }

    // ── Validação usando banco local ──────────────────────────────────────────
    public async Task<IbgeValidationResult> ValidateMunicipioAsync(
        AppDbContext db, string municipio, string uf)
    {
        var ufEntity = await db.Ufs
            .FirstOrDefaultAsync(u => u.Sigla.ToUpper() == uf.ToUpper());

        if (ufEntity is null)
            return new(false, null, uf, null, $"UF '{uf}' não encontrada");

        var municipioEntity = await db.Municipios
            .FirstOrDefaultAsync(m =>
                m.UfId == ufEntity.Id &&
                m.Nome.ToLower() == municipio.ToLower());

        if (municipioEntity is null)
            return new(false, null, uf.ToUpper(), null,
                $"Município '{municipio}' não encontrado em {uf.ToUpper()}");

        return new(true, municipioEntity.Nome, ufEntity.Sigla, municipioEntity.Id, null);
    }
}

// DTOs para deserializar a resposta do IBGE
file record IbgeUfDto(int Id, string Sigla, string Nome);
file record IbgeMunicipioDto(int Id, string Nome);
