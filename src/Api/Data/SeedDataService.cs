using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Data;

public static class SeedDataService
{
    // ── IDs fixos para garantir idempotência ──────────────────────────────────
    static class Ids
    {
        // Usuários
        public static readonly string User1 = "seed-user-0001-0001-000000000001";
        public static readonly string User2 = "seed-user-0001-0001-000000000002";

        // SoilTypes
        public static readonly Guid SoilLatossolo = new("aaaa0001-0000-0000-0000-000000000001");
        public static readonly Guid SoilArgissolo = new("aaaa0001-0000-0000-0000-000000000002");
        public static readonly Guid SoilNeossolo  = new("aaaa0001-0000-0000-0000-000000000003");

        // IrrigationTypes
        public static readonly Guid IrrigGotejamento = new("bbbb0001-0000-0000-0000-000000000001");
        public static readonly Guid IrrigPivot        = new("bbbb0001-0000-0000-0000-000000000002");
        public static readonly Guid IrrigSequeiro     = new("bbbb0001-0000-0000-0000-000000000003");

        // Cultures
        public static readonly Guid CultureSoja    = new("cccc0001-0000-0000-0000-000000000001");
        public static readonly Guid CultureMilho   = new("cccc0001-0000-0000-0000-000000000002");
        public static readonly Guid CultureAlgodao = new("cccc0001-0000-0000-0000-000000000003");
        public static readonly Guid CultureFeijao  = new("cccc0001-0000-0000-0000-000000000004");

        // InputProducts (nomes amigáveis)
        public static readonly Guid InputFertNitrogenado = new("dddd0001-0000-0000-0000-000000000001");
        public static readonly Guid InputHerbicida        = new("dddd0001-0000-0000-0000-000000000002");
        public static readonly Guid InputFungicida        = new("dddd0001-0000-0000-0000-000000000003");
        public static readonly Guid InputInseticida       = new("dddd0001-0000-0000-0000-000000000004");
        public static readonly Guid InputSementeSoja      = new("dddd0001-0000-0000-0000-000000000005");
        public static readonly Guid InputSementeMilho     = new("dddd0001-0000-0000-0000-000000000006");
        public static readonly Guid InputSementeAlgodao   = new("dddd0001-0000-0000-0000-000000000007");

        // Addresses
        public static readonly Guid AddrProp1 = new("eeee0001-0000-0000-0000-000000000001");
        public static readonly Guid AddrProp2 = new("eeee0001-0000-0000-0000-000000000002");
        public static readonly Guid AddrProp3 = new("eeee0001-0000-0000-0000-000000000003");

        // RuralProperties
        public static readonly Guid Prop1 = new("ffff0001-0000-0000-0000-000000000001");
        public static readonly Guid Prop2 = new("ffff0001-0000-0000-0000-000000000002");
        public static readonly Guid Prop3 = new("ffff0001-0000-0000-0000-000000000003");

        // Fields — Prop1
        public static readonly Guid FieldP1A = new("1111f001-0000-0000-0000-000000000001");
        public static readonly Guid FieldP1B = new("1111f001-0000-0000-0000-000000000002");
        public static readonly Guid FieldP1C = new("1111f001-0000-0000-0000-000000000003");
        // Fields — Prop2
        public static readonly Guid FieldP2A = new("2222f001-0000-0000-0000-000000000001");
        public static readonly Guid FieldP2B = new("2222f001-0000-0000-0000-000000000002");
        // Fields — Prop3
        public static readonly Guid FieldP3A = new("3333f001-0000-0000-0000-000000000001");
        public static readonly Guid FieldP3B = new("3333f001-0000-0000-0000-000000000002");
        public static readonly Guid FieldP3C = new("3333f001-0000-0000-0000-000000000003");

        // Harvests
        public static readonly Guid Harvest1 = new("1111a001-0000-0000-0000-000000000001");
        public static readonly Guid Harvest2 = new("1111a001-0000-0000-0000-000000000002");
        public static readonly Guid Harvest3 = new("1111a001-0000-0000-0000-000000000003");
        public static readonly Guid Harvest4 = new("1111a001-0000-0000-0000-000000000004");
        public static readonly Guid Harvest5 = new("1111a001-0000-0000-0000-000000000005");
        public static readonly Guid Harvest6 = new("1111a001-0000-0000-0000-000000000006");

        // ProductivityRecords
        public static readonly Guid ProdRec1 = new("88880001-0000-0000-0000-000000000001");
        public static readonly Guid ProdRec2 = new("88880001-0000-0000-0000-000000000002");

        // StockItems
        public static readonly Guid Stock1 = new("55550001-0000-0000-0000-000000000001");
        public static readonly Guid Stock2 = new("55550001-0000-0000-0000-000000000002");
        public static readonly Guid Stock3 = new("55550001-0000-0000-0000-000000000003");
        public static readonly Guid Stock4 = new("55550001-0000-0000-0000-000000000004");
        public static readonly Guid Stock5 = new("55550001-0000-0000-0000-000000000005");
        public static readonly Guid Stock6 = new("55550001-0000-0000-0000-000000000006");
        public static readonly Guid Stock7 = new("55550001-0000-0000-0000-000000000007");
        public static readonly Guid Stock8 = new("55550001-0000-0000-0000-000000000008");
        public static readonly Guid Stock9 = new("55550001-0000-0000-0000-000000000009");

        // StockMovements
        public static readonly Guid Mov1  = new("66660001-0000-0000-0000-000000000001");
        public static readonly Guid Mov2  = new("66660001-0000-0000-0000-000000000002");
        public static readonly Guid Mov3  = new("66660001-0000-0000-0000-000000000003");
        public static readonly Guid Mov4  = new("66660001-0000-0000-0000-000000000004");
        public static readonly Guid Mov5  = new("66660001-0000-0000-0000-000000000005");
        public static readonly Guid Mov6  = new("66660001-0000-0000-0000-000000000006");
        public static readonly Guid Mov7  = new("66660001-0000-0000-0000-000000000007");
        public static readonly Guid Mov8  = new("66660001-0000-0000-0000-000000000008");
        public static readonly Guid Mov9  = new("66660001-0000-0000-0000-000000000009");
        public static readonly Guid Mov10 = new("66660001-0000-0000-0000-000000000010");

        // HarvestInputs
        public static readonly Guid HInput1 = new("77770001-0000-0000-0000-000000000001");
        public static readonly Guid HInput2 = new("77770001-0000-0000-0000-000000000002");

        // WeatherStation + Readings
        public static readonly Guid WStation1  = new("aa110001-0000-0000-0000-000000000001");
        public static readonly Guid WReading1  = new("aa220001-0000-0000-0000-000000000001");
        public static readonly Guid WReading2  = new("aa220001-0000-0000-0000-000000000002");
        public static readonly Guid WReading3  = new("aa220001-0000-0000-0000-000000000003");
        public static readonly Guid WReading4  = new("aa220001-0000-0000-0000-000000000004");
        public static readonly Guid WReading5  = new("aa220001-0000-0000-0000-000000000005");
        public static readonly Guid WReading6  = new("aa220001-0000-0000-0000-000000000006");

        // Alerts
        public static readonly Guid Alert1 = new("9999a001-0000-0000-0000-000000000001");
        public static readonly Guid Alert2 = new("9999a001-0000-0000-0000-000000000002");
        public static readonly Guid Alert3 = new("9999a001-0000-0000-0000-000000000003");
        public static readonly Guid Alert4 = new("9999a001-0000-0000-0000-000000000004");
        public static readonly Guid Alert5 = new("9999a001-0000-0000-0000-000000000005");

        // Workspaces
        public static readonly Guid Workspace1      = new("bb110001-0000-0000-0000-000000000001");
        public static readonly Guid Workspace2      = new("bb110001-0000-0000-0000-000000000002");
        public static readonly Guid WsMember1       = new("bb220001-0000-0000-0000-000000000001");
        public static readonly Guid WsMember2       = new("bb220001-0000-0000-0000-000000000002");
        public static readonly Guid WsMember3       = new("bb220001-0000-0000-0000-000000000003");
        public static readonly Guid WsInvite1       = new("bb330001-0000-0000-0000-000000000001");
    }

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<IdentityUser> userManager)
    {
        var prop1Id = Ids.Prop1;
        if (await db.RuralProperties.AnyAsync(p => p.Id == prop1Id))
        {
            Log.Information("Seed data already present - skipping.");
            return;
        }

        Log.Information("Seeding demonstration data...");

        var user1 = await EnsureUser(userManager, Ids.User1, "gestor@demo.com",   "Demo@1234!", Api.Roles.Gestor);
        var user2 = await EnsureUser(userManager, Ids.User2, "operador@demo.com", "Demo@1234!", Api.Roles.Operador);

        await SeedLookups(db);
        await db.SaveChangesAsync();

        await SeedProperties(db, user1.Id, user2.Id);
        await db.SaveChangesAsync();

        await SeedHarvests(db, user1.Id);
        await db.SaveChangesAsync();

        await SeedStock(db, user1.Id);
        await db.SaveChangesAsync();

        await SeedWeather(db);
        await db.SaveChangesAsync();

        await SeedAlerts(db, user1.Id);
        await db.SaveChangesAsync();

        await SeedWorkspaces(db, user1.Id, user2.Id);
        await db.SaveChangesAsync();

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
            Id            = id,
            UserName      = email,
            Email         = email,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var result = await um.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new Exception($"Falha ao criar usuário seed '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await um.AddToRoleAsync(user, role);
        Log.Information("Seed user created: {Email}", email);
        return user;
    }

    // ── Lookups ───────────────────────────────────────────────────────────────
    static async Task SeedLookups(AppDbContext db)
    {
        if (!await db.SoilTypes.AnyAsync(s => s.Id == Ids.SoilLatossolo))
        {
            db.SoilTypes.AddRange(
                new SoilType { Id = Ids.SoilLatossolo, Name = "Latossolo Vermelho-Amarelo", Description = "Alta profundidade, boa drenagem, predominante no Cerrado." },
                new SoilType { Id = Ids.SoilArgissolo, Name = "Argissolo Vermelho",          Description = "Textura argilosa, boa retenção de nutrientes." },
                new SoilType { Id = Ids.SoilNeossolo,  Name = "Neossolo Quartzarênico",      Description = "Solo arenoso, baixa fertilidade natural." }
            );
        }

        if (!await db.IrrigationTypes.AnyAsync(i => i.Id == Ids.IrrigGotejamento))
        {
            db.IrrigationTypes.AddRange(
                new IrrigationType { Id = Ids.IrrigGotejamento, Name = "Gotejamento",              Description = "Alta eficiência hídrica, ideal para horticultura e fruticultura." },
                new IrrigationType { Id = Ids.IrrigPivot,       Name = "Pivô Central",              Description = "Cobertura de grandes áreas, comum em grãos." },
                new IrrigationType { Id = Ids.IrrigSequeiro,    Name = "Sequeiro (sem irrigação)",  Description = "Dependente de chuvas naturais." }
            );
        }

        if (!await db.Cultures.AnyAsync(c => c.Id == Ids.CultureSoja))
        {
            db.Cultures.AddRange(
                new Culture { Id = Ids.CultureSoja,    CommonName = "Soja",    ScientificName = "Glycine max",        AverageCycleDays = 120, MinTempCelsius = 10, MaxTempCelsius = 40, IdealRainfallMm = 700 },
                new Culture { Id = Ids.CultureMilho,   CommonName = "Milho",   ScientificName = "Zea mays",           AverageCycleDays = 150, MinTempCelsius = 10, MaxTempCelsius = 38, IdealRainfallMm = 600 },
                new Culture { Id = Ids.CultureAlgodao, CommonName = "Algodão", ScientificName = "Gossypium hirsutum", AverageCycleDays = 180, MinTempCelsius = 15, MaxTempCelsius = 35, IdealRainfallMm = 700 },
                new Culture { Id = Ids.CultureFeijao,  CommonName = "Feijão",  ScientificName = "Phaseolus vulgaris", AverageCycleDays = 90,  MinTempCelsius = 15, MaxTempCelsius = 30, IdealRainfallMm = 350 }
            );
        }

        if (!await db.InputProducts.AnyAsync(i => i.Id == Ids.InputFertNitrogenado))
        {
            db.InputProducts.AddRange(
                new InputProduct { Id = Ids.InputFertNitrogenado, Name = "Fertilizante Nitrogenado", Type = InputType.Fertilizante, Unit = "kg", ActiveIngredient = "Nitrogênio 46%",         RegistrationNumber = "BR-FERT-00123" },
                new InputProduct { Id = Ids.InputHerbicida,        Name = "Herbicida Pós-Emergente",  Type = InputType.Herbicida,    Unit = "L",  ActiveIngredient = "Glifosato",              RegistrationNumber = "BR-HERB-00456" },
                new InputProduct { Id = Ids.InputFungicida,        Name = "Fungicida Agrícola",       Type = InputType.Fungicida,    Unit = "L",  ActiveIngredient = "Piraclostrobina 13%",    RegistrationNumber = "BR-FUNG-00789" },
                new InputProduct { Id = Ids.InputInseticida,       Name = "Inseticida Piretroide",    Type = InputType.Inseticida,   Unit = "L",  ActiveIngredient = "Deltametrina 25 g/L",   RegistrationNumber = "BR-INSE-00321" },
                new InputProduct { Id = Ids.InputSementeSoja,      Name = "Semente de Soja",          Type = InputType.Semente,      Unit = "sc", ActiveIngredient = null },
                new InputProduct { Id = Ids.InputSementeMilho,     Name = "Semente de Milho",         Type = InputType.Semente,      Unit = "sc", ActiveIngredient = null },
                new InputProduct { Id = Ids.InputSementeAlgodao,   Name = "Semente de Algodão",       Type = InputType.Semente,      Unit = "sc", ActiveIngredient = null }
            );
        }
    }

    // ── Propriedades, Endereços e Talhões ─────────────────────────────────────
    static async Task SeedProperties(AppDbContext db, string ownerId1, string ownerId2)
    {
        if (await db.Addresses.AnyAsync(a => a.Id == Ids.AddrProp1)) return;

        db.Addresses.AddRange(
            new Address { Id = Ids.AddrProp1, Cep = "77000-000", Logradouro = "Rodovia TO-050, Km 38",   Bairro = "Zona Rural", Municipio = "Palmas",        Uf = "TO", IbgeCode = 1721000 },
            new Address { Id = Ids.AddrProp2, Cep = "77500-000", Logradouro = "Estrada Municipal, s/n",  Bairro = "Zona Rural", Municipio = "Porto Nacional", Uf = "TO", IbgeCode = 1718204 },
            new Address { Id = Ids.AddrProp3, Cep = "77800-000", Logradouro = "Rodovia BR-153, Km 127",  Bairro = "Zona Rural", Municipio = "Araguaína",      Uf = "TO", IbgeCode = 1702109 }
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
            },
            new RuralProperty
            {
                Id = Ids.Prop3, OwnerId = ownerId1, AddressId = Ids.AddrProp3,
                Name = "Fazenda Rio Branco", CarNumber = "TO-1702109-RBRN.1122.3344.5566",
                TotalAreaHa = 430.0m, VegetationAreaHa = 86.0m
            }
        );

        db.Fields.AddRange(
            // Prop1 — 3 talhões
            new Field { Id = Ids.FieldP1A, PropertyId = Ids.Prop1, Name = "Talhão Norte",        AreaHa = 320.0m, SoilTypeId = Ids.SoilLatossolo, IrrigationTypeId = Ids.IrrigPivot },
            new Field { Id = Ids.FieldP1B, PropertyId = Ids.Prop1, Name = "Talhão Sul",          AreaHa = 280.0m, SoilTypeId = Ids.SoilArgissolo, IrrigationTypeId = Ids.IrrigPivot },
            new Field { Id = Ids.FieldP1C, PropertyId = Ids.Prop1, Name = "Talhão Leste",        AreaHa = 180.0m, SoilTypeId = Ids.SoilLatossolo, IrrigationTypeId = Ids.IrrigSequeiro },
            // Prop2 — 2 talhões
            new Field { Id = Ids.FieldP2A, PropertyId = Ids.Prop2, Name = "Área Principal",      AreaHa = 85.0m,  SoilTypeId = Ids.SoilNeossolo,  IrrigationTypeId = Ids.IrrigSequeiro },
            new Field { Id = Ids.FieldP2B, PropertyId = Ids.Prop2, Name = "Área de Irrigação",   AreaHa = 25.0m,  SoilTypeId = Ids.SoilArgissolo, IrrigationTypeId = Ids.IrrigGotejamento },
            // Prop3 — 3 talhões
            new Field { Id = Ids.FieldP3A, PropertyId = Ids.Prop3, Name = "Módulo A",            AreaHa = 200.0m, SoilTypeId = Ids.SoilLatossolo, IrrigationTypeId = Ids.IrrigPivot },
            new Field { Id = Ids.FieldP3B, PropertyId = Ids.Prop3, Name = "Módulo B",            AreaHa = 150.0m, SoilTypeId = Ids.SoilLatossolo, IrrigationTypeId = Ids.IrrigPivot },
            new Field { Id = Ids.FieldP3C, PropertyId = Ids.Prop3, Name = "Módulo C - Sequeiro", AreaHa = 60.0m,  SoilTypeId = Ids.SoilNeossolo,  IrrigationTypeId = Ids.IrrigSequeiro }
        );
    }

    // ── Safras ────────────────────────────────────────────────────────────────
    static async Task SeedHarvests(AppDbContext db, string userId)
    {
        if (await db.Harvests.AnyAsync(h => h.Id == Ids.Harvest1)) return;

        db.Harvests.AddRange(
            new Harvest
            {
                Id = Ids.Harvest1, FieldId = Ids.FieldP1A, CultureId = Ids.CultureSoja, ResponsibleUserId = userId,
                Name = "Safra Soja 2024/25 — Talhão Norte",
                PlantingDate = new DateOnly(2024, 10, 15), ExpectedHarvestDate = new DateOnly(2025, 2, 20),
                ActualHarvestDate = new DateOnly(2025, 2, 25), Status = HarvestStatus.Harvested,
                EstimatedYieldTons = 1152.0m, ActualYieldTons = 1184.0m
            },
            new Harvest
            {
                Id = Ids.Harvest2, FieldId = Ids.FieldP1B, CultureId = Ids.CultureMilho, ResponsibleUserId = userId,
                Name = "Safra Milho 2024/25 — Talhão Sul",
                PlantingDate = new DateOnly(2024, 11, 5), ExpectedHarvestDate = new DateOnly(2025, 4, 10),
                Status = HarvestStatus.InProgress, EstimatedYieldTons = 3360.0m
            },
            new Harvest
            {
                Id = Ids.Harvest3, FieldId = Ids.FieldP2A, CultureId = Ids.CultureFeijao, ResponsibleUserId = userId,
                Name = "Safra Feijão 2025 — Área Principal",
                PlantingDate = new DateOnly(2025, 1, 20), ExpectedHarvestDate = new DateOnly(2025, 4, 20),
                Status = HarvestStatus.Planned, EstimatedYieldTons = 127.5m
            },
            new Harvest
            {
                Id = Ids.Harvest4, FieldId = Ids.FieldP3A, CultureId = Ids.CultureAlgodao, ResponsibleUserId = userId,
                Name = "Safra Algodão 2024/25 — Módulo A",
                PlantingDate = new DateOnly(2024, 11, 20), ExpectedHarvestDate = new DateOnly(2025, 5, 30),
                Status = HarvestStatus.InProgress, EstimatedYieldTons = 540.0m
            },
            new Harvest
            {
                Id = Ids.Harvest5, FieldId = Ids.FieldP1C, CultureId = Ids.CultureSoja, ResponsibleUserId = userId,
                Name = "Safra Soja 2024/25 — Talhão Leste",
                PlantingDate = new DateOnly(2024, 10, 20), ExpectedHarvestDate = new DateOnly(2025, 2, 28),
                ActualHarvestDate = new DateOnly(2025, 3, 3), Status = HarvestStatus.Harvested,
                EstimatedYieldTons = 612.0m, ActualYieldTons = 594.0m
            },
            new Harvest
            {
                Id = Ids.Harvest6, FieldId = Ids.FieldP3B, CultureId = Ids.CultureMilho, ResponsibleUserId = userId,
                Name = "Safra Milho 2025/26 — Módulo B",
                PlantingDate = new DateOnly(2025, 10, 1), ExpectedHarvestDate = new DateOnly(2026, 3, 15),
                Status = HarvestStatus.Planned, EstimatedYieldTons = 1800.0m
            }
        );

        db.ProductivityRecords.AddRange(
            new ProductivityRecord
            {
                Id = Ids.ProdRec1, HarvestId = Ids.Harvest1,
                RecordedAt = new DateTime(2025, 2, 25, 12, 0, 0, DateTimeKind.Utc),
                YieldTonsPerHa = 3.70m,
                Notes = "Colheita dentro do prazo. Umidade dos grãos: 14,2%. Sem perdas por clima."
            },
            new ProductivityRecord
            {
                Id = Ids.ProdRec2, HarvestId = Ids.Harvest5,
                RecordedAt = new DateTime(2025, 3, 3, 10, 0, 0, DateTimeKind.Utc),
                YieldTonsPerHa = 3.30m,
                Notes = "Produtividade ligeiramente abaixo do esperado. Déficit hídrico em fevereiro."
            }
        );
    }

    // ── Estoque e Movimentações ────────────────────────────────────────────────
    static async Task SeedStock(AppDbContext db, string userId)
    {
        if (await db.StockItems.AnyAsync(s => s.Id == Ids.Stock1)) return;

        db.StockItems.AddRange(
            // Prop1
            new StockItem { Id = Ids.Stock1, PropertyId = Ids.Prop1, InputProductId = Ids.InputFertNitrogenado, QuantityInStock = 4200.0m, MinimumStock = 1000.0m, UnitCost = 3.20m },
            new StockItem { Id = Ids.Stock2, PropertyId = Ids.Prop1, InputProductId = Ids.InputHerbicida,        QuantityInStock = 320.0m,  MinimumStock = 100.0m,  UnitCost = 28.50m },
            new StockItem { Id = Ids.Stock3, PropertyId = Ids.Prop1, InputProductId = Ids.InputSementeSoja,      QuantityInStock = 48.0m,   MinimumStock = 200.0m,  UnitCost = 420.0m },  // abaixo do mínimo
            new StockItem { Id = Ids.Stock4, PropertyId = Ids.Prop1, InputProductId = Ids.InputFungicida,        QuantityInStock = 95.0m,   MinimumStock = 30.0m,   UnitCost = 95.0m },
            new StockItem { Id = Ids.Stock5, PropertyId = Ids.Prop1, InputProductId = Ids.InputInseticida,       QuantityInStock = 40.0m,   MinimumStock = 15.0m,   UnitCost = 78.0m },
            // Prop2
            new StockItem { Id = Ids.Stock6, PropertyId = Ids.Prop2, InputProductId = Ids.InputFertNitrogenado, QuantityInStock = 800.0m,  MinimumStock = 200.0m,  UnitCost = 3.20m },
            // Prop3
            new StockItem { Id = Ids.Stock7, PropertyId = Ids.Prop3, InputProductId = Ids.InputFertNitrogenado, QuantityInStock = 6000.0m, MinimumStock = 1500.0m, UnitCost = 3.10m },
            new StockItem { Id = Ids.Stock8, PropertyId = Ids.Prop3, InputProductId = Ids.InputSementeAlgodao,  QuantityInStock = 180.0m,  MinimumStock = 100.0m,  UnitCost = 310.0m },
            new StockItem { Id = Ids.Stock9, PropertyId = Ids.Prop3, InputProductId = Ids.InputHerbicida,        QuantityInStock = 210.0m,  MinimumStock = 80.0m,   UnitCost = 28.50m }
        );

        db.StockMovements.AddRange(
            new StockMovement { Id = Ids.Mov1,  StockItemId = Ids.Stock1, UserId = userId, Type = MovementType.Entrada, Quantity = 5000.0m, Reason = "Compra NF 4521 — Agrostore Palmas",        MovedAt = new DateTime(2024, 9,  10, 8,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov2,  StockItemId = Ids.Stock2, UserId = userId, Type = MovementType.Entrada, Quantity = 400.0m,  Reason = "Compra NF 4522 — Agrostore Palmas",        MovedAt = new DateTime(2024, 9,  10, 8, 30, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov3,  StockItemId = Ids.Stock3, UserId = userId, Type = MovementType.Entrada, Quantity = 320.0m,  Reason = "Compra sementes safra 24/25",              MovedAt = new DateTime(2024, 9,  15, 10, 0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov4,  StockItemId = Ids.Stock1, UserId = userId, Type = MovementType.Saida,   Quantity = 800.0m,  Reason = "Adubação de cobertura — Talhão Norte",     MovedAt = new DateTime(2024, 12,  5, 7,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov5,  StockItemId = Ids.Stock3, UserId = userId, Type = MovementType.Saida,   Quantity = 272.0m,  Reason = "Plantio — Talhão Norte (320 ha × 0,85 sc/ha)", MovedAt = new DateTime(2024, 10, 14, 6,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov6,  StockItemId = Ids.Stock7, UserId = userId, Type = MovementType.Entrada, Quantity = 6000.0m, Reason = "Compra NF 7810 — Distribuidora Araguaína", MovedAt = new DateTime(2024, 10,  5, 9,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov7,  StockItemId = Ids.Stock8, UserId = userId, Type = MovementType.Entrada, Quantity = 200.0m,  Reason = "Compra sementes algodão safra 24/25",      MovedAt = new DateTime(2024, 10, 18, 8,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov8,  StockItemId = Ids.Stock8, UserId = userId, Type = MovementType.Saida,   Quantity = 20.0m,   Reason = "Plantio — Módulo A (200 ha × 0,1 sc/ha)",  MovedAt = new DateTime(2024, 11, 20, 6,  0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov9,  StockItemId = Ids.Stock6, UserId = userId, Type = MovementType.Entrada, Quantity = 800.0m,  Reason = "Compra NF 2203 — Cooperativa Porto Nacional", MovedAt = new DateTime(2024, 12, 10, 9, 0, 0, DateTimeKind.Utc) },
            new StockMovement { Id = Ids.Mov10, StockItemId = Ids.Stock4, UserId = userId, Type = MovementType.Entrada, Quantity = 100.0m,  Reason = "Compra NF 4601 — Agrostore Palmas",        MovedAt = new DateTime(2024, 11,  3, 14, 0, 0, DateTimeKind.Utc) }
        );

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

    // ── Estação Meteorológica ─────────────────────────────────────────────────
    static async Task SeedWeather(AppDbContext db)
    {
        if (await db.WeatherStations.AnyAsync(s => s.Id == Ids.WStation1)) return;

        db.WeatherStations.Add(new WeatherStation
        {
            Id = Ids.WStation1, PropertyId = Ids.Prop1,
            Name = "Estação Cerrado Verde", Model = "Davis Vantage Pro2",
            Latitude = -10.1689m, Longitude = -48.3317m, IsActive = true
        });

        var now = DateTime.UtcNow;
        db.WeatherReadings.AddRange(
            new WeatherReading { Id = Ids.WReading1, StationId = Ids.WStation1, RecordedAt = now.AddHours(-5),  Temperature = 31.2m, Humidity = 72m, Rainfall = 0m,    WindSpeedKmh = 12m, WindDirection = "NE", PressureHpa = 1012m },
            new WeatherReading { Id = Ids.WReading2, StationId = Ids.WStation1, RecordedAt = now.AddHours(-4),  Temperature = 33.5m, Humidity = 65m, Rainfall = 0m,    WindSpeedKmh = 15m, WindDirection = "NE", PressureHpa = 1011m },
            new WeatherReading { Id = Ids.WReading3, StationId = Ids.WStation1, RecordedAt = now.AddHours(-3),  Temperature = 35.8m, Humidity = 58m, Rainfall = 0m,    WindSpeedKmh = 18m, WindDirection = "E",  PressureHpa = 1010m },
            new WeatherReading { Id = Ids.WReading4, StationId = Ids.WStation1, RecordedAt = now.AddHours(-2),  Temperature = 36.1m, Humidity = 55m, Rainfall = 0m,    WindSpeedKmh = 14m, WindDirection = "E",  PressureHpa = 1009m },
            new WeatherReading { Id = Ids.WReading5, StationId = Ids.WStation1, RecordedAt = now.AddHours(-1),  Temperature = 34.7m, Humidity = 62m, Rainfall = 2.4m,  WindSpeedKmh = 22m, WindDirection = "SE", PressureHpa = 1008m },
            new WeatherReading { Id = Ids.WReading6, StationId = Ids.WStation1, RecordedAt = now.AddMinutes(-30), Temperature = 29.3m, Humidity = 80m, Rainfall = 8.6m, WindSpeedKmh = 25m, WindDirection = "S",  PressureHpa = 1007m }
        );
    }

    // ── Alertas ───────────────────────────────────────────────────────────────
    static async Task SeedAlerts(AppDbContext db, string userId)
    {
        if (await db.Alerts.AnyAsync(a => a.Id == Ids.Alert1)) return;

        db.Alerts.AddRange(
            new Alert
            {
                Id = Ids.Alert1, CreatedByUserId = userId,
                Type = AlertType.StockLow, Severity = AlertSeverity.High,
                Title = "Estoque crítico: Semente de Soja",
                Message = "O estoque de Semente de Soja na Fazenda Cerrado Verde está em 48 sc, abaixo do mínimo de 200 sc. Realize reposição antes do próximo plantio.",
                PropertyId = Ids.Prop1, StockItemId = Ids.Stock3, IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new Alert
            {
                Id = Ids.Alert2, CreatedByUserId = userId,
                Type = AlertType.HarvestStatus, Severity = AlertSeverity.Medium,
                Title = "Safra de milho em progresso",
                Message = "A safra Milho 2024/25 no Talhão Sul está em andamento. Colheita prevista para 10/04/2025. Monitore a umidade dos grãos na fase final.",
                PropertyId = Ids.Prop1, HarvestId = Ids.Harvest2, IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                ExpiresAt = new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new Alert
            {
                Id = Ids.Alert3, CreatedByUserId = userId,
                Type = AlertType.System, Severity = AlertSeverity.Low,
                Title = "Bem-vindo ao AgroSmart",
                Message = "Dados de demonstração carregados. Explore propriedades, safras, estoque, clima e workspaces compartilhados.",
                IsRead = false, CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new Alert
            {
                Id = Ids.Alert4, CreatedByUserId = userId,
                Type = AlertType.WeatherWarning, Severity = AlertSeverity.Medium,
                Title = "Chuva intensa registrada — Cerrado Verde",
                Message = "A estação meteorológica registrou 8,6 mm de chuva na última meia hora. Verifique as condições de campo antes de realizar operações mecanizadas.",
                PropertyId = Ids.Prop1, IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new Alert
            {
                Id = Ids.Alert5, CreatedByUserId = userId,
                Type = AlertType.HarvestStatus, Severity = AlertSeverity.Low,
                Title = "Colheita de soja concluída — Talhão Leste",
                Message = "Safra Soja 2024/25 no Talhão Leste encerrada em 03/03/2025. Produtividade: 3,30 t/ha (meta: 3,40 t/ha). Déficit hídrico em fevereiro impactou resultado.",
                PropertyId = Ids.Prop1, HarvestId = Ids.Harvest5, IsRead = true,
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            }
        );
    }

    // ── Workspaces ────────────────────────────────────────────────────────────
    static async Task SeedWorkspaces(AppDbContext db, string ownerId1, string ownerId2)
    {
        if (await db.Workspaces.AnyAsync(w => w.Id == Ids.Workspace1)) return;

        db.Workspaces.AddRange(
            new Workspace
            {
                Id = Ids.Workspace1, OwnerId = ownerId1,
                Name = "Grupo Agrícola Cerrado", Slug = "grupo-agricola-cerrado",
                Description = "Workspace compartilhado entre gestor e operador para a Fazenda Cerrado Verde."
            },
            new Workspace
            {
                Id = Ids.Workspace2, OwnerId = ownerId1,
                Name = "Fazenda Rio Branco — Equipe", Slug = "fazenda-rio-branco-equipe",
                Description = "Equipe de operação da Fazenda Rio Branco."
            }
        );

        db.WorkspaceMembers.AddRange(
            new WorkspaceMember { Id = Ids.WsMember1, WorkspaceId = Ids.Workspace1, UserId = ownerId1,  Role = WorkspaceRole.Owner,    JoinedAt = DateTime.UtcNow.AddDays(-30) },
            new WorkspaceMember { Id = Ids.WsMember2, WorkspaceId = Ids.Workspace1, UserId = ownerId2,  Role = WorkspaceRole.Operador, JoinedAt = DateTime.UtcNow.AddDays(-25) },
            new WorkspaceMember { Id = Ids.WsMember3, WorkspaceId = Ids.Workspace2, UserId = ownerId1,  Role = WorkspaceRole.Owner,    JoinedAt = DateTime.UtcNow.AddDays(-15) }
        );

        // Convite pendente para novo parceiro no Workspace1
        db.WorkspaceInvites.Add(new WorkspaceInvite
        {
            Id = Ids.WsInvite1, WorkspaceId = Ids.Workspace1,
            InvitedEmail = "parceiro@exemplo.com.br",
            Role = WorkspaceRole.Consulta,
            Token = "DEMO-INVITE-TOKEN-CERRADO-VERDE01",
            Status = InviteStatus.Pending,
            InvitedByUserId = ownerId1,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        // Vincula propriedades aos workspaces
        var prop1 = await db.RuralProperties.FindAsync(Ids.Prop1);
        var prop3 = await db.RuralProperties.FindAsync(Ids.Prop3);
        if (prop1 is not null) prop1.WorkspaceId = Ids.Workspace1;
        if (prop3 is not null) prop3.WorkspaceId = Ids.Workspace2;
    }
}
