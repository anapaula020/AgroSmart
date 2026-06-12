namespace Api.Models;

public class WeatherStation : BaseEntity
{
    public Guid    PropertyId  { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Model       { get; set; }
    public decimal Latitude    { get; set; }
    public decimal Longitude   { get; set; }
    public bool    IsActive    { get; set; } = true;

    public RuralProperty?               Property { get; set; }
    public ICollection<WeatherReading>  Readings  { get; set; } = [];
    public ICollection<WeatherForecast> Forecasts { get; set; } = [];
}

public class WeatherReading : BaseEntity
{
    public Guid     StationId    { get; set; }
    public DateTime RecordedAt   { get; set; } = DateTime.UtcNow;
    public decimal? Temperature  { get; set; } // °C
    public decimal? Humidity     { get; set; } // %
    public decimal? Rainfall     { get; set; } // mm
    public decimal? WindSpeedKmh { get; set; }
    public string?  WindDirection { get; set; }
    public decimal? PressureHpa  { get; set; }
    public decimal? SolarRadiation { get; set; } // W/m²

    public WeatherStation? Station { get; set; }
}

public class WeatherForecast : BaseEntity
{
    public Guid     StationId    { get; set; }
    public DateTime ForecastDate { get; set; }
    public decimal? TempMin      { get; set; }
    public decimal? TempMax      { get; set; }
    public decimal? RainfallMm   { get; set; }
    public decimal? HumidityPct  { get; set; }
    public string?  Condition    { get; set; } // sunny, cloudy, rain, etc.
    public string?  Source       { get; set; } // inmet, openweather, etc.

    public WeatherStation? Station { get; set; }
}
