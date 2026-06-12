using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Data;

/// <summary>
/// Popula o banco com dados de demonstração.
/// Idempotente: verifica existência antes de inserir.
/// Roda apenas em Development ou se SEED_DATA=true.
/// </summary>
public static class SeedDataService
{
    // ── IDs fixos para garantir idempotência ──────────────────────────────────
    static class Ids
    {
        // Usuários
        public static readonly string User1 = "seed-user-0001-0001-000000000001";
        public static readonly string User2 = "seed-user-0001-0001-000000000002";

        // SoilTypes
        public static readonly Guid SoilLatossolo    = new("aaaa0001-0000-0000-0000-000000000001");
        public static readonly Guid SoilArgissolo    = new("aaaa0001-0000-0000-0000-000000000002");
        public static readonly Guid SoilNeossolo     = new("aaaa0001-0000-0000-0000-000000000003");

        // IrrigationTypes
        public static readonly Guid IrrigGotejamento = new("bbbb0001-0000-0000-0000-000000000001");
        public static readonly Guid IrrigPivot       = new("bbbb0001-0000-0000-0000-000000000002");
        public static readonly Guid IrrigSequeiro    = new("bbbb0001-0000-0000-0000-000000000003");

        // Cultures
        public static readonly Guid CultureSoja      = new("cccc0001-0000-0000-0000-000000000001");
        public static readonly Guid CultureMilho     = new("cccc0001-0000-0000-0000-000000000002");
        public static readonly Guid CultureAlgodao   = new("cccc0001-0000-0000-0000-000000000003");
        public static readonly Guid CultureFeijao    = new("cccc0001-0000-0000-0000-000000000004");

        // InputProducts
        public static readonly Guid InputUreiaGranulada  = new("dddd0001-0000-0000-0000-000000000001");
        public static readonly Guid InputGlifosato       = new("dddd0001-0000-0000-0000-000000000002");
        public static readonly Guid InputFungicidaOpera  = new("dddd0001-0000-0000-0000-000000000003");
        public static readonly Guid InputInseticidaDecis = new("dddd0001-0000-0000-0000-000000000004");
        public static readonly Guid InputSementeSoja     = new("dddd0001-0000-0000-0000-000000000005");
        public static readonly Guid InputSementeMilho    = new("dddd0001-0000-0000-0000-000000000006");

        // Addresses
        public static readonly Guid AddrProp1 = new("eeee0001-0000-0000-0000-000000000001");
        public static readonly Guid AddrProp2 = new("eeee0001-0000-0000-0000-000000000002");

        // RuralProperties
        public static readonly Guid Prop1 = new("ffff0001-0000-0000-0000-000000000001");
        public static readonly Guid Prop2 = new("ffff0001-0000-0000-0000-000000000002");

        // Fields (Prop1)
        public static readonly Guid FieldP1A = new("1111f001-0000-0000-0000-000000000001");
        public static readonly Guid FieldP1B = new("1111f001-0000-0000-0000-000000000002");
        // Fields (Prop2)
        public static readonly Guid FieldP2A = new("2222f001-0000-0000-0000-000000000001");

        // Harvests
        public static readonly Guid Harvest1 = new("1111a001-0000-0000-0000-000000000001");
        public static readonly Guid Harvest2 = new("1111a001-0000-0000-0000-000000000002");
        public static readonly Guid Harvest3 = new("1111a001-0000-0000-0000-000000000003");

        // StockItems
        public static readonly Guid Stock1 = new("55550001-0000-0000-0000-000000000001");
        public static readonly Guid Stock2 = new("55550001-0000-0000-0000-000000000002");
        public static readonly Guid Stock3 = new("55550001-0000-0000-0000-000000000003");
        public static readonly Guid Stock4 = new("55550001-0000-0000-0000-000000000004");

        // StockMovements
        public static readonly Guid Mov1 = new("66660001-0000-0000-0000-000000000001");
        public static readonly Guid Mov2 = new("66660001-0000-0000-0000-000000000002");
        public static readonly Guid Mov3 = new("66660001-0000-0000-0000-000000000003");
        public static readonly Guid Mov4 = new("66660001-0000-0000-0000-000000000004");
        public static readonly Guid Mov5 = new("66660001-0000-0000-0000-000000000005");

        // HarvestInputs
        public static readonly Guid HInput1 = new("77770001-0000-0000-0000-000000000001");
        public static readonly Guid HInput2 = new("77770001-0000-0000-0000-000000000002");

        // ProductivityRecords
        public static readonly Guid ProdRec1 = new("88880001-0000-0000-0000-000000000001");

        // Alerts
        public static readonly Guid Alert1 = new("9999a001-0000-0000-0000-000000000001");
        public static readonly Guid Alert2 = new("9999a001-0000-0000-0000-000000000002");
        public static readonly Guid Alert3 = new("9999a001-0000-0000-0000-000000000003");
    }

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<IdentityUser> userManager)
    {
        // Guard: se já existe qualquer propriedade rural semeada, encerra
        var prop1Id = Ids.Prop1;
        if (await db.RuralProperties.AnyAsync(p => p.Id == prop1Id))
        {
            Log.Information("Seed data already present — skipping.");
            return;
        }

        Log.Information("Seeding demonstration data...");

        var user1 = await EnsureUser(userManager, Ids.User1, "fazendeiro@demo.com", "Demo@1234!", "User");
        var user2 = await EnsureUser(userManager, Ids.User2, "gestor@demo.com",     "Demo@1234!", "User");

        await SeedLookups(db);
        await db.SaveChangesAsync(); // SoilType, IrrigationType, Culture, InputProduct

        await SeedProperties(db, user1.Id, user2.Id);
        await db.SaveChangesAsync(); // Address, RuralProperty, Field

        await SeedHarvests(db, user1.Id);
        await db.SaveChangesAsync(); // Harvest, ProductivityRecord

        await SeedStock(db, user1.Id);
        await db.SaveChangesAsync(); // StockItem, StockMovement, HarvestInput

        await SeedAlerts(db, user1.Id);
        await db.SaveChangesAsync(); // Alert

        Log.Information("Seed data inserted successfully.");
    }

    // ── Usuários ──────────────────────────────────────────────────────────────
    static async Task<IdentityUser> EnsureUser(
        UserManager<IdentityUser> um, string id, string email, string password, string role)
    {
        var existing = await um.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new IdentityUser
        {
            Id             = id,
            UserName       = email,
            Email          = email,
            EmailConfirmed = true,
            SecurityStamp  = Guid.NewGuid().ToString()
        };
        var result = await um.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new Exception($"Falha ao criar usuário seed '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await um.AddToRoleAsync(user, role);
        Log.Information("Seed user created: {Email}", email);
        return user;
    }

    // ── Lookups (SoilType, IrrigationType, Culture, InputProduct) ─────────────
    static async Task SeedLookups(AppDbContext db)
    {
        var soilLatossoloId = Ids.SoilLatossolo;
        if (!await db.SoilTypes.AnyAsync(s => s.Id == soilLatossoloId))
        {
            db.SoilTypes.AddRange(
                new SoilType { Id = Ids.SoilLatossolo, Name = "Latossolo Vermelho-Amarelo",  Description = "Alta profundidade, boa drenagem, predominante no Cerrado." },
                new SoilType { Id = Ids.SoilArgissolo, Name = "Argissolo Vermelho",           Description = "Textura argilosa, boa retenção de nutrientes." },
                new SoilType { Id = Ids.SoilNeossolo,  Name = "Neossolo Quartzarênico",       Description = "Solo arenoso, baixa fertilidade natural." }
            );
        }

        var irrigGotejamentoId = Ids.IrrigGotejamento;
        if (!await db.IrrigationTypes.AnyAsync(i => i.Id == irrigGotejamentoId))
        {
            db.IrrigationTypes.AddRange(
                new IrrigationType { Id = Ids.IrrigGotejamento, Name = "Gotejamento",          Description = "Alta eficiência hídrica, ideal para horticultura e fruticultura." },
                new IrrigationType { Id = Ids.IrrigPivot,       Name = "Pivô Central",         Description = "Cobertura de grandes áreas, comum em grãos." },
                new IrrigationType { Id = Ids.IrrigSequeiro,    Name = "Sequeiro (sem irrigação)", Description = "Dependente de chuvas naturais." }
            );
        }

        var cultureSojaId = Ids.CultureSoja;
        if (!await db.Cultures.AnyAsync(c => c.Id == cultureSojaId))
        {
            db.Cultures.AddRange(
                new Culture { Id = Ids.CultureSoja,    CommonName = "Soja",    ScientificName = "Glycine max",          AverageCycleDays = 120, MinTempCelsius = 10, MaxTempCelsius = 40, IdealRainfallMm = 700 },
                new Culture { Id = Ids.CultureMilho,   CommonName = "Milho",   ScientificName = "Zea mays",             AverageCycleDays = 150, MinTempCelsius = 10, MaxTempCelsius = 38, IdealRainfallMm = 600 },
                new Culture { Id = Ids.CultureAlgodao, CommonName = "Algodão", ScientificName = "Gossypium hirsutum",   AverageCycleDays = 180, MinTempCelsius = 15, MaxTempCelsius = 35, IdealRainfallMm = 700 },
                new Culture { Id = Ids.CultureFeijao,  CommonName = "Feijão",  ScientificName = "Phaseolus vulgaris",   AverageCycleDays = 90,  MinTempCelsius = 15, MaxTempCelsius = 30, IdealRainfallMm = 350 }
            );
        }

        var inputUreiaId = Ids.InputUreiaGranulada;
        if (!await db.InputProducts.AnyAsync(i => i.Id == inputUreiaId))
        {
            db.InputProducts.AddRange(
                new InputProduct { Id = Ids.InputUreiaGranulada,  Name = "Ureia Granulada 46%",       Type = InputType.Fertilizante, Unit = "kg",  ActiveIngredient = "Nitrogênio 46%",              RegistrationNumber = "BR-FERT-00123" },
                new InputProduct { Id = Ids.InputGlifosato,       Name = "Glifosato 480 g/L",         Type = InputType.Herbicida,    Unit = "L",   ActiveIngredient = "Glifosato",                   RegistrationNumber = "BR-HERB-00456" },
                new InputProduct { Id = Ids.InputFungicidaOpera,  Name = "Opera (Piraclostrobina)",   Type = InputType.Fungicida,    Unit = "L",   ActiveIngredient = "Piraclostrobina + Epoxiconazol", RegistrationNumber = "BR-FUNG-00789" },
                new InputProduct { Id = Ids.InputInseticidaDecis, Name = "Decis 25 EC",               Type = InputType.Inseticida,   Unit = "L",   ActiveIngredient = "Deltametrina 25 g/L",         RegistrationNumber = "BR-INSE-00321" },
                new InputProduct { Id = Ids.InputSementeSoja,     Name = "Semente Soja TMG 7062 IPRO",Type = InputType.Semente,      Unit = "sc",  ActiveIngredient = null },
                new InputProduct { Id = Ids.InputSementeMilho,    Name = "Semente Milho DKB 390",     Type = InputType.Semente,      Unit = "sc",  ActiveIngredient = null }
            );
        }
    }

    // ── Propriedades, Endereços e Talhões ─────────────────────────────────────
    static async Task SeedProperties(AppDbContext db, string ownerId1, string ownerId2)
    {
        var addrProp1Id = Ids.AddrProp1;
        if (await db.Addresses.AnyAsync(a => a.Id == addrProp1Id)) return;

        db.Addresses.AddRange(
            new Address
            {
                Id = Ids.AddrProp1, Cep = "77000-000",
                Logradouro = "Rodovia TO-050, Km 38", Bairro = "Zona Rural",
                Municipio = "Palmas", Uf = "TO", IbgeCode = 1721000,
                Latitude = -10.184m, Longitude = -48.334m
            },
            new Address
            {
                Id = Ids.AddrProp2, Cep = "77500-000",
                Logradouro = "Estrada Municipal, s/n", Bairro = "Zona Rural",
                Municipio = "Porto Nacional", Uf = "TO", IbgeCode = 1718204,
                Latitude = -10.706m, Longitude = -48.417m
            }
        );

        db.RuralProperties.AddRange(
            new RuralProperty
            {
                Id = Ids.Prop1, OwnerId = ownerId1, AddressId = Ids.AddrProp1,
                Name = "Fazenda Cerrado Verde", CarNumber = "TO-1721000-ABCD.1234.5678.9012",
                TotalAreaHa = 850.0m, VegetationAreaHa = 170.0m
            },
            new RuralProperty
            {
                Id = Ids.Prop2, OwnerId = ownerId2, AddressId = Ids.AddrProp2,
                Name = "Sítio Boa Esperança", CarNumber = "TO-1718204-XYZW.9876.5432.1098",
                TotalAreaHa = 120.5m, VegetationAreaHa = 30.0m
            }
        );

        db.Fields.AddRange(
            new Field
            {
                Id = Ids.FieldP1A, PropertyId = Ids.Prop1,
                Name = "Talhão A — Soja", AreaHa = 320.0m,
                SoilTypeId = Ids.SoilLatossolo, IrrigationTypeId = Ids.IrrigPivot
            },
            new Field
            {
                Id = Ids.FieldP1B, PropertyId = Ids.Prop1,
                Name = "Talhão B — Milho", AreaHa = 280.0m,
                SoilTypeId = Ids.SoilArgissolo, IrrigationTypeId = Ids.IrrigPivot
            },
            new Field
            {
                Id = Ids.FieldP2A, PropertyId = Ids.Prop2,
                Name = "Área Principal", AreaHa = 85.0m,
                SoilTypeId = Ids.SoilNeossolo, IrrigationTypeId = Ids.IrrigSequeiro
            }
        );
    }

    // ── Safras e Registros de Produtividade ───────────────────────────────────
    static async Task SeedHarvests(AppDbContext db, string userId)
    {
        var harvest1Id = Ids.Harvest1;
        if (await db.Harvests.AnyAsync(h => h.Id == harvest1Id)) return;

        db.Harvests.AddRange(
            new Harvest
            {
                Id = Ids.Harvest1, FieldId = Ids.FieldP1A, CultureId = Ids.CultureSoja,
                ResponsibleUserId = userId,
                Name = "Safra Soja 2024/25 — Talhão A",
                PlantingDate = new DateOnly(2024, 10, 15),
                ExpectedHarvestDate = new DateOnly(2025, 2, 20),
                ActualHarvestDate   = new DateOnly(2025, 2, 25),
                Status = HarvestStatus.Harvested,
                EstimatedYieldTons = 1152.0m,  // 3,6 t/ha × 320 ha
                ActualYieldTons    = 1184.0m   // 3,7 t/ha × 320 ha
            },
            new Harvest
            {
                Id = Ids.Harvest2, FieldId = Ids.FieldP1B, CultureId = Ids.CultureMilho,
                ResponsibleUserId = userId,
                Name = "Safra Milho 2024/25 — Talhão B",
                PlantingDate = new DateOnly(2024, 11, 5),
                ExpectedHarvestDate = new DateOnly(2025, 4, 10),
                Status = HarvestStatus.InProgress,
                EstimatedYieldTons = 3360.0m   // 12 t/ha × 280 ha
            },
            new Harvest
            {
                Id = Ids.Harvest3, FieldId = Ids.FieldP2A, CultureId = Ids.CultureFeijao,
                ResponsibleUserId = userId,
                Name = "Safra Feijão 2025 — Área Principal",
                PlantingDate = new DateOnly(2025, 1, 20),
                ExpectedHarvestDate = new DateOnly(2025, 4, 20),
                Status = HarvestStatus.Planned,
                EstimatedYieldTons = 127.5m    // 1,5 t/ha × 85 ha
            }
        );

        db.ProductivityRecords.Add(new ProductivityRecord
        {
            Id = Ids.ProdRec1, HarvestId = Ids.Harvest1,
            RecordedAt = new DateTime(2025, 2, 25, 12, 0, 0, DateTimeKind.Utc),
            YieldTonsPerHa = 3.70m,
            Notes = "Colheita dentro do prazo. Umidade dos grãos: 14,2%. Sem perdas por clima."
        });
    }

    // ── Estoque, Movimentações e Insumos Aplicados ────────────────────────────
    static async Task SeedStock(AppDbContext db, string userId)
    {
        var stock1Id = Ids.Stock1;
        if (await db.StockItems.AnyAsync(s => s.Id == stock1Id)) return;

        db.StockItems.AddRange(
            new StockItem
            {
                Id = Ids.Stock1, PropertyId = Ids.Prop1,
                InputProductId = Ids.InputUreiaGranulada,
                QuantityInStock = 4200.0m, MinimumStock = 1000.0m, UnitCost = 3.20m
            },
            new StockItem
            {
                Id = Ids.Stock2, PropertyId = Ids.Prop1,
                InputProductId = Ids.InputGlifosato,
                QuantityInStock = 320.0m, MinimumStock = 100.0m, UnitCost = 28.50m
            },
            new StockItem
            {
                Id = Ids.Stock3, PropertyId = Ids.Prop1,
                InputProductId = Ids.InputSementeSoja,
                QuantityInStock = 48.0m, MinimumStock = 200.0m, UnitCost = 420.0m  // abaixo do mínimo → alerta
            },
            new StockItem
            {
                Id = Ids.Stock4, PropertyId = Ids.Prop2,
                InputProductId = Ids.InputFungicidaOpera,
                QuantityInStock = 55.0m, MinimumStock = 20.0m, UnitCost = 95.0m
            }
        );

        // Entradas de estoque
        db.StockMovements.AddRange(
            new StockMovement
            {
                Id = Ids.Mov1, StockItemId = Ids.Stock1, UserId = userId,
                Type = MovementType.Entrada, Quantity = 5000.0m,
                Reason = "Compra NF 4521 — Agrostore Palmas",
                MovedAt = new DateTime(2024, 9, 10, 8, 0, 0, DateTimeKind.Utc)
            },
            new StockMovement
            {
                Id = Ids.Mov2, StockItemId = Ids.Stock2, UserId = userId,
                Type = MovementType.Entrada, Quantity = 400.0m,
                Reason = "Compra NF 4522 — Agrostore Palmas",
                MovedAt = new DateTime(2024, 9, 10, 8, 30, 0, DateTimeKind.Utc)
            },
            new StockMovement
            {
                Id = Ids.Mov3, StockItemId = Ids.Stock3, UserId = userId,
                Type = MovementType.Entrada, Quantity = 320.0m,
                Reason = "Compra sementes safra 24/25",
                MovedAt = new DateTime(2024, 9, 15, 10, 0, 0, DateTimeKind.Utc)
            },
            // Saída usada na safra de soja (HarvestInput)
            new StockMovement
            {
                Id = Ids.Mov4, StockItemId = Ids.Stock1, UserId = userId,
                Type = MovementType.Saida, Quantity = 800.0m,
                Reason = "Aplicação adubação de cobertura — Talhão A",
                MovedAt = new DateTime(2024, 12, 5, 7, 0, 0, DateTimeKind.Utc)
            },
            new StockMovement
            {
                Id = Ids.Mov5, StockItemId = Ids.Stock3, UserId = userId,
                Type = MovementType.Saida, Quantity = 272.0m,
                Reason = "Uso no plantio — Talhão A (320 ha × 0,85 sc/ha)",
                MovedAt = new DateTime(2024, 10, 14, 6, 0, 0, DateTimeKind.Utc)
            }
        );

        // Insumos aplicados vinculados à safra
        db.HarvestInputs.AddRange(
            new HarvestInput
            {
                Id = Ids.HInput1, HarvestId = Ids.Harvest1, StockMovementId = Ids.Mov5,
                AppliedAt = new DateTime(2024, 10, 14, 6, 0, 0, DateTimeKind.Utc),
                QuantityUsed = 272.0m, ApplicationMethod = "Plantio mecanizado — plantadeira 24 linhas"
            },
            new HarvestInput
            {
                Id = Ids.HInput2, HarvestId = Ids.Harvest1, StockMovementId = Ids.Mov4,
                AppliedAt = new DateTime(2024, 12, 5, 7, 0, 0, DateTimeKind.Utc),
                QuantityUsed = 800.0m, ApplicationMethod = "Aplicação aérea — adubação nitrogenada de cobertura"
            }
        );
    }

    // ── Alertas ───────────────────────────────────────────────────────────────
    static async Task SeedAlerts(AppDbContext db, string userId)
    {
        var alert1Id = Ids.Alert1;
        if (await db.Alerts.AnyAsync(a => a.Id == alert1Id)) return;

        db.Alerts.AddRange(
            new Alert
            {
                Id = Ids.Alert1, CreatedByUserId = userId,
                Type = AlertType.StockLow, Severity = AlertSeverity.High,
                Title = "Estoque crítico de sementes de soja",
                Message = "O estoque de 'Semente Soja TMG 7062 IPRO' na Fazenda Cerrado Verde está em 48 sc, abaixo do mínimo de 200 sc. Realize reposição antes do próximo plantio.",
                PropertyId = Ids.Prop1, StockItemId = Ids.Stock3, IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new Alert
            {
                Id = Ids.Alert2, CreatedByUserId = userId,
                Type = AlertType.HarvestStatus, Severity = AlertSeverity.Medium,
                Title = "Safra de milho em andamento",
                Message = "A safra 'Safra Milho 2024/25 — Talhão B' está em progresso. Colheita prevista para 10/04/2025. Monitorar umidade dos grãos na fase final.",
                PropertyId = Ids.Prop1, HarvestId = Ids.Harvest2, IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new Alert
            {
                Id = Ids.Alert3, CreatedByUserId = userId,
                Type = AlertType.System, Severity = AlertSeverity.Low,
                Title = "Bem-vindo ao AgroAdmin",
                Message = "Dados de demonstração carregados com sucesso. Explore as funcionalidades: propriedades rurais, safras, controle de estoque e alertas.",
                IsRead = false, CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        );
    }
}
