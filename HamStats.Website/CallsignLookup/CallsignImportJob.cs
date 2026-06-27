using Hangfire;
using Microsoft.Data.Sqlite;
using RoushTech.Asio;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Hangfire job that (re)builds the offline callsign → grid table. Downloads the license dumps and
/// GeoNames postal data (cached on disk so it can rebuild offline), geocodes every licensee to a
/// Maidenhead grid, and bulk-replaces the <c>Callsigns</c> table in a single transaction.
/// </summary>
public class CallsignImportJob
{
    private readonly IEnumerable<ICallsignSource> _sources;
    private readonly GeoNamesGeocoder _geocoder;
    private readonly CtyDataSource _cty;
    private readonly CallsignLookupOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly JobSessionService _jobSessions;
    private readonly ILogger<CallsignImportJob> _logger;

    public CallsignImportJob(
        IEnumerable<ICallsignSource> sources,
        GeoNamesGeocoder geocoder,
        CtyDataSource cty,
        CallsignLookupOptions options,
        IConfiguration configuration,
        IHostEnvironment environment,
        JobSessionService jobSessions,
        ILogger<CallsignImportJob> logger)
    {
        _sources = sources;
        _geocoder = geocoder;
        _cty = cty;
        _options = options;
        _configuration = configuration;
        _environment = environment;
        _jobSessions = jobSessions;
        _logger = logger;
    }

    /// <param name="offline">
    /// When true, sources only use already-cached archives (no network). Used for the build-on-startup
    /// run so the table repopulates after a dev wipe without requiring internet.
    /// </param>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task RunAsync(Guid sessionId, bool offline, CancellationToken cancellationToken)
    {
        // A non-empty session id streams this run's logs to watching clients (Asio job session).
        var tracked = sessionId != Guid.Empty;
        if (tracked)
        {
            JobSessionService.ActivateSession(sessionId);
        }

        var hasError = false;
        try
        {
            await RunCore(offline, cancellationToken);
        }
        catch (Exception exception)
        {
            hasError = true;
            _logger.LogError(exception, "Callsign import failed");
            throw;
        }
        finally
        {
            if (tracked)
            {
                await _jobSessions.CompleteSession(sessionId, hasError);
            }
        }
    }

    private async Task RunCore(bool offline, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Callsign import disabled; skipping");
            return;
        }

        var cacheDirectory = ResolveCacheDirectory();
        _logger.LogInformation("Callsign import starting (offline={Offline}, cache={Cache})", offline, cacheDirectory);

        var timestamp = DateTime.UtcNow;

        var archives = new List<(ICallsignSource Source, string Path)>();
        foreach (var source in _sources)
        {
            var path = await source.DownloadAsync(cacheDirectory, offline, cancellationToken);
            if (path is not null)
            {
                archives.Add((source, path));
            }
        }

        // cty.dat (worldwide prefixes) carries its own coordinates, so it builds independently of the
        // postal datasets — DX coverage survives even when GeoNames is unavailable.
        var prefixRows = BuildPrefixRows(await _cty.DownloadAsync(cacheDirectory, offline, cancellationToken), timestamp);

        await _geocoder.LoadAsync(cacheDirectory, offline, cancellationToken);
        var rows = new Dictionary<string, CallsignRow>();
        if (_geocoder.Count == 0)
        {
            _logger.LogWarning("No GeoNames data available; skipping per-licensee geocoding");
        }
        else
        {
            foreach (var (source, path) in archives)
            {
                var matched = 0;
                var total = 0;
                foreach (var license in source.Parse(path))
                {
                    total++;
                    var grid = _geocoder.GridFor(license.Country, license.PostalCode);
                    if (grid is null)
                    {
                        continue;
                    }

                    matched++;
                    rows[license.Callsign] = new CallsignRow(
                        license.Callsign, grid, license.Name, license.Region, license.Country, source.Source, timestamp);
                }

                _logger.LogInformation("{Source}: geocoded {Matched:N0}/{Total:N0} licensees", source.Source, matched, total);
            }
        }

        if (rows.Count == 0 && prefixRows.Count == 0)
        {
            _logger.LogWarning("Import produced no rows; leaving existing tables untouched");
            return;
        }

        await WriteAsync(rows.Values, prefixRows.Values, cancellationToken);
        _logger.LogInformation("Callsign import complete: {Calls:N0} callsigns, {Prefixes:N0} prefixes",
            rows.Count, prefixRows.Count);
    }

    private Dictionary<string, PrefixRow> BuildPrefixRows(string? ctyPath, DateTime timestamp)
    {
        var prefixRows = new Dictionary<string, PrefixRow>();
        if (ctyPath is null)
        {
            return prefixRows;
        }

        foreach (var entry in _cty.Parse(ctyPath))
        {
            // Key by exactness + prefix so an exact callsign and a like-named prefix can't collide.
            var key = (entry.IsExact ? "=" : "") + entry.Prefix;
            prefixRows[key] = new PrefixRow(
                entry.Prefix, entry.Grid, entry.Country, entry.Continent, entry.IsExact, _cty.Source, timestamp);
        }

        _logger.LogInformation("CTY: {Count:N0} prefixes/exceptions", prefixRows.Count);
        return prefixRows;
    }

    private async Task WriteAsync(
        ICollection<CallsignRow> rows, ICollection<PrefixRow> prefixRows, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("Default");
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Each table is refreshed only when its dataset produced rows, so a failed download can't wipe
        // an otherwise-good table.
        if (rows.Count > 0)
        {
            await WriteCallsignsAsync(connection, rows, cancellationToken);
        }

        if (prefixRows.Count > 0)
        {
            await WritePrefixesAsync(connection, prefixRows, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteCallsignsAsync(
        SqliteConnection connection, IEnumerable<CallsignRow> rows, CancellationToken cancellationToken)
    {
        // Full refresh: clear stale callsigns, then repopulate, so readers see a consistent set.
        var clear = connection.CreateCommand();
        clear.CommandText = "DELETE FROM Callsigns";
        await clear.ExecuteNonQueryAsync(cancellationToken);

        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT OR REPLACE INTO Callsigns (Callsign, Grid, Name, Region, Country, Source, UpdatedAt)
            VALUES ($callsign, $grid, $name, $region, $country, $source, $updatedAt)
            """;
        var pCallsign = insert.CreateParameter(); pCallsign.ParameterName = "$callsign"; insert.Parameters.Add(pCallsign);
        var pGrid = insert.CreateParameter(); pGrid.ParameterName = "$grid"; insert.Parameters.Add(pGrid);
        var pName = insert.CreateParameter(); pName.ParameterName = "$name"; insert.Parameters.Add(pName);
        var pRegion = insert.CreateParameter(); pRegion.ParameterName = "$region"; insert.Parameters.Add(pRegion);
        var pCountry = insert.CreateParameter(); pCountry.ParameterName = "$country"; insert.Parameters.Add(pCountry);
        var pSource = insert.CreateParameter(); pSource.ParameterName = "$source"; insert.Parameters.Add(pSource);
        var pUpdatedAt = insert.CreateParameter(); pUpdatedAt.ParameterName = "$updatedAt"; insert.Parameters.Add(pUpdatedAt);

        foreach (var row in rows)
        {
            pCallsign.Value = row.Callsign;
            pGrid.Value = row.Grid;
            pName.Value = (object?)row.Name ?? DBNull.Value;
            pRegion.Value = (object?)row.Region ?? DBNull.Value;
            pCountry.Value = row.Country;
            pSource.Value = row.Source;
            pUpdatedAt.Value = row.UpdatedAt.ToString("o");
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task WritePrefixesAsync(
        SqliteConnection connection, IEnumerable<PrefixRow> prefixRows, CancellationToken cancellationToken)
    {
        var clear = connection.CreateCommand();
        clear.CommandText = "DELETE FROM CallsignPrefixes";
        await clear.ExecuteNonQueryAsync(cancellationToken);

        var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO CallsignPrefixes (Prefix, Grid, Country, Continent, IsExact, Source, UpdatedAt)
            VALUES ($prefix, $grid, $country, $continent, $isExact, $source, $updatedAt)
            """;
        var pPrefix = insert.CreateParameter(); pPrefix.ParameterName = "$prefix"; insert.Parameters.Add(pPrefix);
        var pGrid = insert.CreateParameter(); pGrid.ParameterName = "$grid"; insert.Parameters.Add(pGrid);
        var pCountry = insert.CreateParameter(); pCountry.ParameterName = "$country"; insert.Parameters.Add(pCountry);
        var pContinent = insert.CreateParameter(); pContinent.ParameterName = "$continent"; insert.Parameters.Add(pContinent);
        var pIsExact = insert.CreateParameter(); pIsExact.ParameterName = "$isExact"; insert.Parameters.Add(pIsExact);
        var pSource = insert.CreateParameter(); pSource.ParameterName = "$source"; insert.Parameters.Add(pSource);
        var pUpdatedAt = insert.CreateParameter(); pUpdatedAt.ParameterName = "$updatedAt"; insert.Parameters.Add(pUpdatedAt);

        foreach (var row in prefixRows)
        {
            pPrefix.Value = row.Prefix;
            pGrid.Value = row.Grid;
            pCountry.Value = row.Country;
            pContinent.Value = (object?)row.Continent ?? DBNull.Value;
            pIsExact.Value = row.IsExact ? 1 : 0;
            pSource.Value = row.Source;
            pUpdatedAt.Value = row.UpdatedAt.ToString("o");
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private string ResolveCacheDirectory() =>
        Path.IsPathRooted(_options.CacheDirectory)
            ? _options.CacheDirectory
            : Path.Combine(_environment.ContentRootPath, _options.CacheDirectory);

    private record CallsignRow(
        string Callsign, string Grid, string? Name, string? Region, string Country, string Source, DateTime UpdatedAt);

    private record PrefixRow(
        string Prefix, string Grid, string Country, string? Continent, bool IsExact, string Source, DateTime UpdatedAt);
}
