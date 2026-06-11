using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record CreateStationRequest(
    [Required] Guid PropertyId,
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    string? Model,
    [Required] decimal Latitude,
    [Required] decimal Longitude
);

[ApiController]
[Route("api/v1/weather")]
[Produces("application/json")]
[Authorize]
public class WeatherController(AppDbContext db, OpenWeatherService weather) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin  => User.IsInRole("Admin");

    private async Task<bool> CanAccessProperty(Guid propertyId)
    {
        var p = await db.RuralProperties.FindAsync(propertyId);
        return p is not null && (IsAdmin || p.OwnerId == UserId);
    }

    // ── Stations ──────────────────────────────────────────────────────────────
    [HttpGet("stations")]
    public async Task<IActionResult> GetStations([FromQuery] Guid? propertyId = null)
    {
        var query = db.WeatherStations.Include(s => s.Property).AsQueryable();
        if (!IsAdmin) query = query.Where(s => s.Property!.OwnerId == UserId);
        if (propertyId.HasValue) query = query.Where(s => s.PropertyId == propertyId);

        return Ok(await query.OrderBy(s => s.Name).Select(s => new {
            s.Id, s.Name, s.Model, s.Latitude, s.Longitude, s.IsActive,
            Property     = new { s.Property!.Id, s.Property.Name },
            ReadingCount = db.WeatherReadings.Count(r => r.StationId == s.Id)
        }).ToListAsync());
    }

    [HttpGet("stations/{id:guid}")]
    public async Task<IActionResult> GetStation(Guid id)
    {
        var s = await db.WeatherStations.Include(x => x.Property)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        var latest = await db.WeatherReadings
            .Where(r => r.StationId == id)
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync();

        return Ok(new {
            s.Id, s.Name, s.Model, s.Latitude, s.Longitude, s.IsActive, s.CreatedAt,
            Property = new { s.Property!.Id, s.Property.Name },
            LatestReading = latest is null ? null : new {
                latest.Temperature, latest.Humidity, latest.Rainfall,
                latest.WindSpeedKmh, latest.WindDirection, latest.RecordedAt
            },
            OpenWeatherConfigured = weather.IsConfigured
        });
    }

    [HttpPost("stations")]
    public async Task<IActionResult> CreateStation([FromBody] CreateStationRequest req)
    {
        if (!await CanAccessProperty(req.PropertyId)) return Forbid();

        var station = new WeatherStation
        {
            PropertyId = req.PropertyId,
            Name       = req.Name,
            Model      = req.Model,
            Latitude   = req.Latitude,
            Longitude  = req.Longitude
        };
        db.WeatherStations.Add(station);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetStation), new { id = station.Id }, new { station.Id, station.Name });
    }

    [HttpDelete("stations/{id:guid}")]
    public async Task<IActionResult> DeleteStation(Guid id)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();
        db.WeatherStations.Remove(s);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Readings ──────────────────────────────────────────────────────────────
    [HttpGet("stations/{stationId:guid}/readings")]
    public async Task<IActionResult> GetReadings(
        Guid stationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null,
        [FromQuery] int limit      = 100)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == stationId);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        var query = db.WeatherReadings.Where(r => r.StationId == stationId);
        if (from.HasValue) query = query.Where(r => r.RecordedAt >= from);
        if (to.HasValue)   query = query.Where(r => r.RecordedAt <= to);

        var readings = await query.OrderByDescending(r => r.RecordedAt).Take(limit)
            .Select(r => new {
                r.Id, r.RecordedAt, r.Temperature, r.Humidity, r.Rainfall,
                r.WindSpeedKmh, r.WindDirection, r.PressureHpa, r.SolarRadiation
            }).ToListAsync();

        return Ok(readings);
    }

    // ── Fetch current from OpenWeather and save ───────────────────────────────
    [HttpPost("stations/{stationId:guid}/readings/fetch")]
    public async Task<IActionResult> FetchCurrent(Guid stationId)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == stationId);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        if (!weather.IsConfigured)
            return BadRequest(new ErrorResponse("OpenWeather API key not configured. Set OPENWEATHER_API_KEY in .env"));

        var reading = await weather.FetchCurrentAsync(stationId, s.Latitude, s.Longitude);
        if (reading is null)
            return StatusCode(502, new ErrorResponse("Failed to fetch data from OpenWeather"));

        db.WeatherReadings.Add(reading);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetReadings), new { stationId }, new {
            reading.Id, reading.Temperature, reading.Humidity,
            reading.Rainfall, reading.WindSpeedKmh, reading.RecordedAt
        });
    }

    // ── Manual reading ────────────────────────────────────────────────────────
    [HttpPost("stations/{stationId:guid}/readings")]
    public async Task<IActionResult> AddReading(Guid stationId, [FromBody] AddReadingRequest req)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == stationId);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        var reading = new WeatherReading
        {
            StationId      = stationId,
            RecordedAt     = req.RecordedAt ?? DateTime.UtcNow,
            Temperature    = req.Temperature,
            Humidity       = req.Humidity,
            Rainfall       = req.Rainfall,
            WindSpeedKmh   = req.WindSpeedKmh,
            WindDirection  = req.WindDirection,
            PressureHpa    = req.PressureHpa,
            SolarRadiation = req.SolarRadiation
        };
        db.WeatherReadings.Add(reading);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetReadings), new { stationId }, new { reading.Id, reading.RecordedAt });
    }

    // ── Forecast ──────────────────────────────────────────────────────────────
    [HttpGet("stations/{stationId:guid}/forecast")]
    public async Task<IActionResult> GetForecast(Guid stationId)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == stationId);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        var forecasts = await db.WeatherForecasts
            .Where(f => f.StationId == stationId && f.ForecastDate >= DateTime.UtcNow.Date)
            .OrderBy(f => f.ForecastDate)
            .Select(f => new {
                f.Id, f.ForecastDate, f.TempMin, f.TempMax,
                f.RainfallMm, f.HumidityPct, f.Condition, f.Source
            }).ToListAsync();

        return Ok(forecasts);
    }

    [HttpPost("stations/{stationId:guid}/forecast/fetch")]
    public async Task<IActionResult> FetchForecast(Guid stationId)
    {
        var s = await db.WeatherStations.Include(x => x.Property).FirstOrDefaultAsync(x => x.Id == stationId);
        if (s is null) return NotFound();
        if (!IsAdmin && s.Property!.OwnerId != UserId) return Forbid();

        if (!weather.IsConfigured)
            return BadRequest(new ErrorResponse("OpenWeather API key not configured. Set OPENWEATHER_API_KEY in .env"));

        var forecasts = await weather.FetchForecastAsync(stationId, s.Latitude, s.Longitude);
        if (forecasts.Count == 0)
            return StatusCode(502, new ErrorResponse("Failed to fetch forecast from OpenWeather"));

        // Remove previsões futuras antigas e insere as novas
        var old = db.WeatherForecasts.Where(f => f.StationId == stationId && f.ForecastDate >= DateTime.UtcNow.Date);
        db.WeatherForecasts.RemoveRange(old);
        db.WeatherForecasts.AddRange(forecasts);
        await db.SaveChangesAsync();

        return Ok(new { Saved = forecasts.Count, Days = forecasts.Select(f => f.ForecastDate.Date) });
    }
}

public record AddReadingRequest(
    decimal? Temperature, decimal? Humidity, decimal? Rainfall,
    decimal? WindSpeedKmh, string? WindDirection,
    decimal? PressureHpa, decimal? SolarRadiation,
    DateTime? RecordedAt
);
