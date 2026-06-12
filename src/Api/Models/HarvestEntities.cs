namespace Api.Models;

public enum HarvestStatus { Planned, InProgress, Harvested, Lost }

public class Culture : BaseEntity
{
    public string  CommonName        { get; set; } = string.Empty;
    public string? ScientificName    { get; set; }
    public int?    AverageCycleDays  { get; set; }
    public decimal? MinTempCelsius   { get; set; }
    public decimal? MaxTempCelsius   { get; set; }
    public decimal? IdealRainfallMm  { get; set; }
    public ICollection<Harvest> Harvests { get; set; } = [];
}

public class Harvest : BaseEntity
{
    public Guid          FieldId             { get; set; }
    public Guid          CultureId           { get; set; }
    public string        ResponsibleUserId   { get; set; } = string.Empty; // IdentityUser.Id
    public string        Name                { get; set; } = string.Empty;
    public DateOnly      PlantingDate        { get; set; }
    public DateOnly      ExpectedHarvestDate { get; set; }
    public DateOnly?     ActualHarvestDate   { get; set; }
    public HarvestStatus Status              { get; set; } = HarvestStatus.Planned;
    public decimal       EstimatedYieldTons  { get; set; }
    public decimal?      ActualYieldTons     { get; set; }

    public Field?    Field   { get; set; }
    public Culture?  Culture { get; set; }
    public ICollection<ProductivityRecord> ProductivityRecords { get; set; } = [];
    public ICollection<HarvestInput>       HarvestInputs       { get; set; } = [];
}

public class ProductivityRecord : BaseEntity
{
    public Guid      HarvestId       { get; set; }
    public DateTime  RecordedAt      { get; set; } = DateTime.UtcNow;
    public decimal   YieldTonsPerHa  { get; set; }
    public string?   Notes           { get; set; }
    public Harvest?  Harvest         { get; set; }
}
