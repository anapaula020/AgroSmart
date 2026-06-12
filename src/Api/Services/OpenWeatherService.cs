using System.Globalization;
using System.Text.Json;
using Api.Models;

namespace Api.Services;

public class OpenWeatherService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<OpenWeatherService> logger)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private HttpClient Client => httpFactory.CreateClient("openweather");

    private string ApiKey => (config["OpenWeather:ApiKey"] ?? "").Trim();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    // ── Current weather → WeatherReading ──────────────────────────────────────
    public async Task<WeatherReading?> FetchCurrentAsync(Guid stationId, decimal lat, decimal lon)
    {
        if (!IsConfigured) return null;

        try
        {
            var latStr = lat.ToString(CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(CultureInfo.InvariantCulture);
            var url    = $"weather?lat={latStr}&lon={lonStr}&appid={ApiKey}&units=metric&lang=pt_br";
            var resp   = await Client.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                logger.LogWarning("OpenWeather current HTTP {Status}: {Body}",
                    (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                return null;
            }

            using var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            return new WeatherReading
            {
                StationId     = stationId,
                RecordedAt    = DateTime.UtcNow,
                Temperature   = GetDecimal(root, "main", "temp"),
                Humidity      = GetDecimal(root, "main", "humidity"),
                Rainfall      = root.TryGetProperty("rain", out var rain) && rain.TryGetProperty("1h", out var r1h)
                                    ? (decimal?)r1h.GetDouble() : 0,
                WindSpeedKmh  = GetDecimal(root, "wind", "speed") is decimal ms ? Math.Round(ms * 3.6m, 2) : null,
                WindDirection = GetWindDir(GetDecimal(root, "wind", "deg")),
                PressureHpa   = GetDecimal(root, "main", "pressure"),
            };
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("OpenWeather current timed out after {Timeout}s", Client.Timeout.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenWeather current failed: {Type} — {Msg}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    // ── 5-day forecast → List<WeatherForecast> ────────────────────────────────
    public async Task<List<WeatherForecast>> FetchForecastAsync(Guid stationId, decimal lat, decimal lon)
    {
        if (!IsConfigured) return [];

        try
        {
            var latStr = lat.ToString(CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(CultureInfo.InvariantCulture);
            var url    = $"forecast?lat={latStr}&lon={lonStr}&appid={ApiKey}&units=metric&lang=pt_br&cnt=40";
            var resp   = await Client.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                logger.LogWarning("OpenWeather forecast HTTP {Status}: {Body}",
                    (int)resp.StatusCode, body.Length > 300 ? body[..300] : body);
                return [];
            }

            using var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var list = doc.RootElement.GetProperty("list");

            // Agrupa por dia, pega min/max temp e soma chuva
            var byDay = new Dictionary<DateOnly, (decimal? tMin, decimal? tMax, decimal rain, decimal? hum, string? cond)>();

            foreach (var item in list.EnumerateArray())
            {
                var dt   = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).UtcDateTime;
                var day  = DateOnly.FromDateTime(dt);
                var temp = GetDecimal(item, "main", "temp");
                var hum  = GetDecimal(item, "main", "humidity");
                var rain = item.TryGetProperty("rain", out var r) && r.TryGetProperty("3h", out var r3h)
                               ? (decimal)r3h.GetDouble() : 0m;
                var cond = item.TryGetProperty("weather", out var w) && w.GetArrayLength() > 0
                               ? w[0].GetProperty("description").GetString() : null;

                if (!byDay.TryGetValue(day, out var existing))
                    byDay[day] = (temp, temp, rain, hum, cond);
                else
                    byDay[day] = (
                        temp < existing.tMin ? temp : existing.tMin,
                        temp > existing.tMax ? temp : existing.tMax,
                        existing.rain + rain,
                        hum,
                        existing.cond ?? cond
                    );
            }

            return byDay.Select(kv => new WeatherForecast
            {
                StationId    = stationId,
                ForecastDate = kv.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                TempMin      = kv.Value.tMin,
                TempMax      = kv.Value.tMax,
                RainfallMm   = kv.Value.rain,
                HumidityPct  = kv.Value.hum,
                Condition    = kv.Value.cond,
                Source       = "openweathermap"
            }).OrderBy(f => f.ForecastDate).ToList();
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("OpenWeather forecast timed out after {Timeout}s", Client.Timeout.TotalSeconds);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogWarning("OpenWeather forecast failed: {Type} — {Msg}", ex.GetType().Name, ex.Message);
            return [];
        }
    }

    private static decimal? GetDecimal(JsonElement root, string key1, string key2)
    {
        if (root.TryGetProperty(key1, out var obj) && obj.TryGetProperty(key2, out var val))
            return val.ValueKind == JsonValueKind.Number ? (decimal)val.GetDouble() : null;
        return null;
    }

    private static string? GetWindDir(decimal? deg) => deg switch
    {
        null => null,
        var d when d < 22.5m  => "N",
        var d when d < 67.5m  => "NE",
        var d when d < 112.5m => "L",
        var d when d < 157.5m => "SE",
        var d when d < 202.5m => "S",
        var d when d < 247.5m => "SO",
        var d when d < 292.5m => "O",
        var d when d < 337.5m => "NO",
        _ => "N"
    };
}
