namespace Api.Models;

public enum InputType    { Fertilizante, Herbicida, Fungicida, Inseticida, Semente }
public enum MovementType { Entrada, Saida, Ajuste }

public class InputProduct : BaseEntity
{
    public string     Name                 { get; set; } = string.Empty;
    public InputType  Type                 { get; set; }
    public string     Unit                 { get; set; } = string.Empty;
    public string?    ActiveIngredient     { get; set; }
    public string?    RegistrationNumber   { get; set; }
    public ICollection<StockItem> StockItems { get; set; } = [];
}

public class StockItem : BaseEntity
{
    public Guid     PropertyId      { get; set; }
    public Guid     InputProductId  { get; set; }
    public decimal  QuantityInStock { get; set; }
    public decimal  MinimumStock    { get; set; }
    public decimal  UnitCost        { get; set; }
    public DateTime UpdatedAt       { get; set; } = DateTime.UtcNow;

    public RuralProperty?  Property      { get; set; }
    public InputProduct?   InputProduct  { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = [];
}

public class StockMovement : BaseEntity
{
    public Guid         StockItemId { get; set; }
    public string       UserId      { get; set; } = string.Empty; // IdentityUser.Id
    public MovementType Type        { get; set; }
    public decimal      Quantity    { get; set; }
    public string?      Reason      { get; set; }
    public DateTime     MovedAt     { get; set; } = DateTime.UtcNow;

    public StockItem?     StockItem     { get; set; }
    public HarvestInput?  HarvestInput  { get; set; }
}

public class HarvestInput : BaseEntity
{
    public Guid     HarvestId         { get; set; }
    public Guid     StockMovementId   { get; set; }
    public DateTime AppliedAt         { get; set; } = DateTime.UtcNow;
    public decimal  QuantityUsed      { get; set; }
    public string?  ApplicationMethod { get; set; }

    public Harvest?       Harvest       { get; set; }
    public StockMovement? StockMovement { get; set; }
}
