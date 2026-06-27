using System.Text.RegularExpressions;
using HamStats.Data;
using HamStats.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/settings")]
public partial class SettingsController : Controller
{
    private const string TimeZoneKey = "TimeZone";
    private const string DefaultTimeZone = "America/New_York";
    private const string StationGridKey = "StationGrid";
    private const string CycleSecondsKey = "CycleSeconds";
    private const int DefaultCycleSeconds = 60;

    // Maidenhead locator: field (A-R) + square (0-9) + optional subsquare (a-x). Our station marker.
    [GeneratedRegex("^[A-R]{2}[0-9]{2}([a-x]{2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex GridPattern();

    // IANA ids — valid cross-platform on .NET (ICU). Label is what the settings dropdown shows.
    private static readonly (string Id, string Label)[] UsTimeZones =
    [
        ("America/New_York", "Eastern (New York)"),
        ("America/Chicago", "Central (Chicago)"),
        ("America/Denver", "Mountain (Denver)"),
        ("America/Phoenix", "Arizona (no DST)"),
        ("America/Los_Angeles", "Pacific (Los Angeles)"),
        ("America/Anchorage", "Alaska (Anchorage)"),
        ("Pacific/Honolulu", "Hawaii (Honolulu)"),
    ];

    protected HamStatsDbContext HamStatsDbContext { get; }

    public SettingsController(HamStatsDbContext hamStatsDbContext)
    {
        HamStatsDbContext = hamStatsDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await HamStatsDbContext.Settings
            .Where(s => s.Key == TimeZoneKey || s.Key == StationGridKey || s.Key == CycleSecondsKey
                || s.Key == ManualRadioFilter.SettingKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        return Ok(new
        {
            timeZone = settings.GetValueOrDefault(TimeZoneKey) ?? DefaultTimeZone,
            stationGrid = settings.GetValueOrDefault(StationGridKey),
            cycleSeconds = int.TryParse(settings.GetValueOrDefault(CycleSecondsKey), out var s) ? s : DefaultCycleSeconds,
            filterManualRadios = settings.TryGetValue(ManualRadioFilter.SettingKey, out var f) ? f == "true" : ManualRadioFilter.Default,
        });
    }

    [HttpGet("timezones")]
    public IActionResult TimeZones()
        => Ok(UsTimeZones.Select(z => new { id = z.Id, label = z.Label }));

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SettingsRequest request)
    {
        if (!UsTimeZones.Any(z => z.Id == request.TimeZone))
        {
            return BadRequest($"'{request.TimeZone}' is not a supported US time zone.");
        }

        await Upsert(TimeZoneKey, request.TimeZone);
        return NoContent();
    }

    [HttpPut("grid")]
    public async Task<IActionResult> SetGrid([FromBody] GridRequest request)
    {
        if (!GridPattern().IsMatch(request.Grid ?? ""))
        {
            return BadRequest($"'{request.Grid}' is not a valid Maidenhead grid square.");
        }

        // Normalize: field/square uppercase, subsquare lowercase (e.g. "fn31PR" → "FN31pr").
        var grid = request.Grid!.Length > 4
            ? request.Grid[..4].ToUpperInvariant() + request.Grid[4..].ToLowerInvariant()
            : request.Grid.ToUpperInvariant();
        await Upsert(StationGridKey, grid);
        return NoContent();
    }

    [HttpPut("cycle")]
    public async Task<IActionResult> SetCycle([FromBody] CycleRequest request)
    {
        if (request.Seconds < 5 || request.Seconds > 600)
        {
            return BadRequest("Auto-cycle dwell must be between 5 and 600 seconds.");
        }

        await Upsert(CycleSecondsKey, request.Seconds.ToString());
        return NoContent();
    }

    [HttpPut("filter-manual")]
    public async Task<IActionResult> SetFilterManual([FromBody] FilterManualRequest request)
    {
        await Upsert(ManualRadioFilter.SettingKey, request.Enabled ? "true" : "false");
        return NoContent();
    }

    private async Task Upsert(string key, string value)
    {
        var setting = await HamStatsDbContext.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            HamStatsDbContext.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await HamStatsDbContext.SaveChangesAsync();
    }
}

public class SettingsRequest
{
    public required string TimeZone { get; set; }
}

public class GridRequest
{
    public required string Grid { get; set; }
}

public class CycleRequest
{
    public int Seconds { get; set; }
}

public class FilterManualRequest
{
    public bool Enabled { get; set; }
}
