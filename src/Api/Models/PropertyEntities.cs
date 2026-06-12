namespace Api.Models;

// ── Localização ───────────────────────────────────────────────────────────────
public class Address : BaseEntity
{
    public string Cep          { get; set; } = string.Empty;
    public string Logradouro   { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro       { get; set; } = string.Empty;
    public string Municipio    { get; set; } = string.Empty;
    public string Uf           { get; set; } = string.Empty;
    public int?   IbgeCode     { get; set; }
}

// ── Propriedade Rural ─────────────────────────────────────────────────────────
public class RuralProperty : BaseEntity
{
    public string OwnerId           { get; set; } = string.Empty; // IdentityUser.Id
    public Guid   AddressId         { get; set; }
    public string Name              { get; set; } = string.Empty;
    public string? CarNumber        { get; set; }
    public decimal TotalAreaHa      { get; set; }
    public decimal VegetationAreaHa { get; set; }

    public Guid?      WorkspaceId { get; set; }

    public Address?              Address        { get; set; }
    public Workspace?            Workspace      { get; set; }
    public ICollection<Field>    Fields         { get; set; } = [];
    public ICollection<StockItem> StockItems    { get; set; } = [];
}

public class SoilType : BaseEntity
{
    public string Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Field> Fields { get; set; } = [];
}

public class IrrigationType : BaseEntity
{
    public string Name         { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Field> Fields { get; set; } = [];
}

public class Field : BaseEntity
{
    public Guid   PropertyId       { get; set; }
    public Guid   SoilTypeId       { get; set; }
    public Guid   IrrigationTypeId { get; set; }
    public string Name             { get; set; } = string.Empty;
    public decimal AreaHa          { get; set; }
    public string? PolygonGeoJson  { get; set; }

    public RuralProperty?    Property       { get; set; }
    public SoilType?         SoilType       { get; set; }
    public IrrigationType?   IrrigationType { get; set; }
    public ICollection<Harvest> Harvests    { get; set; } = [];
}
