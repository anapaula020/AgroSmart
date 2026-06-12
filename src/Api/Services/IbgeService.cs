using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public record IbgeValidationResult(bool Valid, string? MunicipioNome, string? Uf, int? IbgeCode, string? Error);

public class IbgeService(ILogger<IbgeService> logger)
{
    // ── Dados estáticos: 27 UFs + capitais ───────────────────────────────────
    // Id = código IBGE oficial da UF / município
    private static readonly (int Id, string Sigla, string Nome, int CapitalId, string CapitalNome)[] _ufs =
    [
        (11, "RO", "Rondônia",              1100205, "Porto Velho"),
        (12, "AC", "Acre",                  1200401, "Rio Branco"),
        (13, "AM", "Amazonas",              1302603, "Manaus"),
        (14, "RR", "Roraima",               1400100, "Boa Vista"),
        (15, "PA", "Pará",                  1501402, "Belém"),
        (16, "AP", "Amapá",                 1600303, "Macapá"),
        (17, "TO", "Tocantins",             1721000, "Palmas"),
        (21, "MA", "Maranhão",              2111300, "São Luís"),
        (22, "PI", "Piauí",                 2211001, "Teresina"),
        (23, "CE", "Ceará",                 2304400, "Fortaleza"),
        (24, "RN", "Rio Grande do Norte",   2408102, "Natal"),
        (25, "PB", "Paraíba",               2507507, "João Pessoa"),
        (26, "PE", "Pernambuco",            2611606, "Recife"),
        (27, "AL", "Alagoas",               2704302, "Maceió"),
        (28, "SE", "Sergipe",               2800308, "Aracaju"),
        (29, "BA", "Bahia",                 2927408, "Salvador"),
        (31, "MG", "Minas Gerais",          3106200, "Belo Horizonte"),
        (32, "ES", "Espírito Santo",        3205309, "Vitória"),
        (33, "RJ", "Rio de Janeiro",        3304557, "Rio de Janeiro"),
        (35, "SP", "São Paulo",             3550308, "São Paulo"),
        (41, "PR", "Paraná",                4106902, "Curitiba"),
        (42, "SC", "Santa Catarina",        4205407, "Florianópolis"),
        (43, "RS", "Rio Grande do Sul",     4314902, "Porto Alegre"),
        (50, "MS", "Mato Grosso do Sul",    5002704, "Campo Grande"),
        (51, "MT", "Mato Grosso",           5103403, "Cuiabá"),
        (52, "GO", "Goiás",                 5208707, "Goiânia"),
        (53, "DF", "Distrito Federal",      5300108, "Brasília"),
    ];

    // ── Seed estático - sem chamada de rede ───────────────────────────────────
    public async Task SeedLocalidadesAsync(AppDbContext db)
    {
        if (await db.Ufs.AnyAsync())
        {
            logger.LogInformation("Localidades já populadas, pulando seed.");
            return;
        }

        logger.LogInformation("Populando UFs e capitais (dados estáticos)...");

        foreach (var (id, sigla, nome, _, _) in _ufs)
            db.Ufs.Add(new Uf { Id = id, Sigla = sigla, Nome = nome });
        await db.SaveChangesAsync();

        foreach (var (id, _, _, capitalId, capitalNome) in _ufs)
            db.Municipios.Add(new Municipio { Id = capitalId, Nome = capitalNome, UfId = id });
        await db.SaveChangesAsync();
        logger.LogInformation("27 UFs e capitais inseridas.");
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
            return new(true, municipio, uf.ToUpper(), null, null);

        return new(true, municipioEntity.Nome, ufEntity.Sigla, municipioEntity.Id, null);
    }
}
