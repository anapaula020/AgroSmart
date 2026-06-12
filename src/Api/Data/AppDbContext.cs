using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    // Property
    public DbSet<Address>       Addresses      => Set<Address>();
    public DbSet<RuralProperty> RuralProperties => Set<RuralProperty>();
    public DbSet<SoilType>      SoilTypes      => Set<SoilType>();
    public DbSet<IrrigationType> IrrigationTypes => Set<IrrigationType>();
    public DbSet<Field>         Fields         => Set<Field>();

    // Harvest
    public DbSet<Culture>             Cultures             => Set<Culture>();
    public DbSet<Harvest>             Harvests             => Set<Harvest>();
    public DbSet<ProductivityRecord>  ProductivityRecords  => Set<ProductivityRecord>();

    // Stock
    public DbSet<InputProduct>  InputProducts  => Set<InputProduct>();
    public DbSet<StockItem>     StockItems     => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<HarvestInput>  HarvestInputs  => Set<HarvestInput>();

    // Auth extras
    public DbSet<Profile>     Profiles     => Set<Profile>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ApiKey>      ApiKeys      => Set<ApiKey>();

    // Alerts
    public DbSet<Alert> Alerts => Set<Alert>();

    // Localidades (cache IBGE)
    public DbSet<Uf>        Ufs        => Set<Uf>();
    public DbSet<Municipio> Municipios => Set<Municipio>();

    // Weather
    public DbSet<WeatherStation>  WeatherStations  => Set<WeatherStation>();
    public DbSet<WeatherReading>  WeatherReadings  => Set<WeatherReading>();
    public DbSet<WeatherForecast> WeatherForecasts => Set<WeatherForecast>();

    // Workspaces
    public DbSet<Workspace>       Workspaces       => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvite> WorkspaceInvites => Set<WorkspaceInvite>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // ── Helpers ───────────────────────────────────────────────────────────
        static void GuidPk<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e)
            where T : class
            => e.Property("Id").HasDefaultValueSql("NEWSEQUENTIALID()");

        // ── Address ───────────────────────────────────────────────────────────
        m.Entity<Address>(e => {
            GuidPk(e);
            e.Property(a => a.Cep).HasMaxLength(10);
            e.Property(a => a.Uf).HasMaxLength(2);
        });

        // ── RuralProperty ─────────────────────────────────────────────────────
        m.Entity<RuralProperty>(e => {
            GuidPk(e);
            e.Property(p => p.TotalAreaHa).HasColumnType("decimal(12,4)");
            e.Property(p => p.VegetationAreaHa).HasColumnType("decimal(12,4)");
            e.HasOne(p => p.Address).WithMany().HasForeignKey(p => p.AddressId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Workspace).WithMany(w => w.Properties).HasForeignKey(p => p.WorkspaceId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // ── SoilType / IrrigationType ─────────────────────────────────────────
        m.Entity<SoilType>(e => { GuidPk(e); e.Property(s => s.Name).HasMaxLength(100); });
        m.Entity<IrrigationType>(e => { GuidPk(e); e.Property(i => i.Name).HasMaxLength(100); });

        // ── Field ─────────────────────────────────────────────────────────────
        m.Entity<Field>(e => {
            GuidPk(e);
            e.Property(f => f.AreaHa).HasColumnType("decimal(12,4)");
            e.HasOne(f => f.Property).WithMany(p => p.Fields).HasForeignKey(f => f.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.SoilType).WithMany(s => s.Fields).HasForeignKey(f => f.SoilTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.IrrigationType).WithMany(i => i.Fields).HasForeignKey(f => f.IrrigationTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Culture ───────────────────────────────────────────────────────────
        m.Entity<Culture>(e => {
            GuidPk(e);
            e.Property(c => c.CommonName).HasMaxLength(150);
            e.Property(c => c.MinTempCelsius).HasColumnType("decimal(5,2)");
            e.Property(c => c.MaxTempCelsius).HasColumnType("decimal(5,2)");
            e.Property(c => c.IdealRainfallMm).HasColumnType("decimal(8,2)");
        });

        // ── Harvest ───────────────────────────────────────────────────────────
        m.Entity<Harvest>(e => {
            GuidPk(e);
            e.Property(h => h.EstimatedYieldTons).HasColumnType("decimal(12,4)");
            e.Property(h => h.ActualYieldTons).HasColumnType("decimal(12,4)");
            e.Property(h => h.Status).HasConversion<string>();
            e.HasOne(h => h.Field).WithMany(f => f.Harvests).HasForeignKey(h => h.FieldId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(h => h.Culture).WithMany(c => c.Harvests).HasForeignKey(h => h.CultureId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ProductivityRecord ────────────────────────────────────────────────
        m.Entity<ProductivityRecord>(e => {
            GuidPk(e);
            e.Property(p => p.YieldTonsPerHa).HasColumnType("decimal(12,4)");
            e.HasOne(p => p.Harvest).WithMany(h => h.ProductivityRecords).HasForeignKey(p => p.HarvestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── InputProduct ──────────────────────────────────────────────────────
        m.Entity<InputProduct>(e => {
            GuidPk(e);
            e.Property(i => i.Type).HasConversion<string>();
            e.Property(i => i.Name).HasMaxLength(200);
        });

        // ── StockItem ─────────────────────────────────────────────────────────
        m.Entity<StockItem>(e => {
            GuidPk(e);
            e.Property(s => s.QuantityInStock).HasColumnType("decimal(12,4)");
            e.Property(s => s.MinimumStock).HasColumnType("decimal(12,4)");
            e.Property(s => s.UnitCost).HasColumnType("decimal(12,4)");
            e.HasOne(s => s.Property).WithMany(p => p.StockItems).HasForeignKey(s => s.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.InputProduct).WithMany(i => i.StockItems).HasForeignKey(s => s.InputProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => new { s.PropertyId, s.InputProductId }).IsUnique();
        });

        // ── StockMovement ─────────────────────────────────────────────────────
        m.Entity<StockMovement>(e => {
            GuidPk(e);
            e.Property(s => s.Quantity).HasColumnType("decimal(12,4)");
            e.Property(s => s.Type).HasConversion<string>();
            e.HasOne(s => s.StockItem).WithMany(i => i.Movements).HasForeignKey(s => s.StockItemId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── HarvestInput ──────────────────────────────────────────────────────
        m.Entity<HarvestInput>(e => {
            GuidPk(e);
            e.Property(h => h.QuantityUsed).HasColumnType("decimal(12,4)");
            e.HasOne(h => h.Harvest).WithMany(hv => hv.HarvestInputs).HasForeignKey(h => h.HarvestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.StockMovement).WithOne(s => s.HarvestInput).HasForeignKey<HarvestInput>(h => h.StockMovementId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Workspace ─────────────────────────────────────────────────────────
        m.Entity<Workspace>(e => {
            GuidPk(e);
            e.Property(w => w.Name).HasMaxLength(200).IsRequired();
            e.Property(w => w.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(w => w.Slug).IsUnique();
        });

        m.Entity<WorkspaceMember>(e => {
            GuidPk(e);
            e.Property(wm => wm.Role).HasConversion<string>();
            e.HasOne(wm => wm.Workspace).WithMany(w => w.Members).HasForeignKey(wm => wm.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(wm => new { wm.WorkspaceId, wm.UserId }).IsUnique();
        });

        m.Entity<WorkspaceInvite>(e => {
            GuidPk(e);
            e.Property(wi => wi.Role).HasConversion<string>();
            e.Property(wi => wi.Status).HasConversion<string>();
            e.Property(wi => wi.InvitedEmail).HasMaxLength(256).IsRequired();
            e.Property(wi => wi.Token).HasMaxLength(64).IsRequired();
            e.HasIndex(wi => wi.Token).IsUnique();
            e.HasOne(wi => wi.Workspace).WithMany(w => w.Invites).HasForeignKey(wi => wi.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<ApiKey>(e => {
            e.HasOne(k => k.Workspace).WithMany(w => w.ApiKeys)
                .HasForeignKey(k => k.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        // ── WeatherStation ────────────────────────────────────────────────────
        m.Entity<WeatherStation>(e => {
            GuidPk(e);
            e.Property(s => s.Latitude).HasColumnType("decimal(9,6)");
            e.Property(s => s.Longitude).HasColumnType("decimal(9,6)");
            e.HasOne(s => s.Property).WithMany().HasForeignKey(s => s.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── WeatherReading ────────────────────────────────────────────────────
        m.Entity<WeatherReading>(e => {
            GuidPk(e);
            e.Property(r => r.Temperature).HasColumnType("decimal(5,2)");
            e.Property(r => r.Humidity).HasColumnType("decimal(5,2)");
            e.Property(r => r.Rainfall).HasColumnType("decimal(8,2)");
            e.Property(r => r.WindSpeedKmh).HasColumnType("decimal(6,2)");
            e.Property(r => r.PressureHpa).HasColumnType("decimal(7,2)");
            e.Property(r => r.SolarRadiation).HasColumnType("decimal(8,2)");
            e.HasOne(r => r.Station).WithMany(s => s.Readings).HasForeignKey(r => r.StationId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── WeatherForecast ───────────────────────────────────────────────────
        m.Entity<WeatherForecast>(e => {
            GuidPk(e);
            e.Property(f => f.TempMin).HasColumnType("decimal(5,2)");
            e.Property(f => f.TempMax).HasColumnType("decimal(5,2)");
            e.Property(f => f.RainfallMm).HasColumnType("decimal(8,2)");
            e.Property(f => f.HumidityPct).HasColumnType("decimal(5,2)");
            e.HasOne(f => f.Station).WithMany(s => s.Forecasts).HasForeignKey(f => f.StationId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Uf / Municipio (IDs IBGE - não são auto-incremento) ──────────────
        m.Entity<Uf>(e => {
            e.Property(u => u.Id).ValueGeneratedNever();
            e.Property(u => u.Sigla).HasMaxLength(2).IsRequired();
            e.Property(u => u.Nome).HasMaxLength(100).IsRequired();
        });

        m.Entity<Municipio>(e => {
            e.Property(mu => mu.Id).ValueGeneratedNever();
            e.Property(mu => mu.Nome).HasMaxLength(200).IsRequired();
            e.HasOne(mu => mu.Uf).WithMany(u => u.Municipios)
                .HasForeignKey(mu => mu.UfId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
