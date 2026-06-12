namespace Api.Models;

public enum AlertSeverity { Low, Medium, High, Critical }
public enum AlertType     { StockLow, HarvestStatus, WeatherWarning, System, Custom }

public class Alert : BaseEntity
{
    public string        CreatedByUserId { get; set; } = string.Empty;
    public AlertType     Type            { get; set; }
    public AlertSeverity Severity        { get; set; }
    public string        Title           { get; set; } = string.Empty;
    public string        Message         { get; set; } = string.Empty;
    public bool          IsRead          { get; set; }
    public DateTime?     ReadAt          { get; set; }
    public DateTime?     ExpiresAt       { get; set; }

    // Referências opcionais - um alerta pode estar ligado a propriedade e/ou safra
    public Guid?         PropertyId      { get; set; }
    public Guid?         HarvestId       { get; set; }
    public Guid?         StockItemId     { get; set; }

    public RuralProperty? Property   { get; set; }
    public Harvest?       Harvest    { get; set; }
    public StockItem?     StockItem  { get; set; }
}
