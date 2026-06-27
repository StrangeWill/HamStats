using System.Globalization;
using System.IO.Compression;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Maps a (country, postal code) to a coordinate using the free GeoNames postal datasets, then to a
/// Maidenhead grid. Note: GeoNames only publishes the 3-character FSA for Canada, so CA lookups key
/// on the first three postal characters.
/// </summary>
public class GeoNamesGeocoder
{
    // GeoNames postal file (tab-delimited) column indexes.
    private const int CountryCol = 0;
    private const int PostalCol = 1;
    private const int LatCol = 9;
    private const int LonCol = 10;

    private readonly HttpClient _httpClient;
    private readonly CallsignLookupOptions _options;
    private readonly ILogger<GeoNamesGeocoder> _logger;
    private readonly Dictionary<string, (double Lat, double Lon)> _coordinates = new();

    public GeoNamesGeocoder(HttpClient httpClient, CallsignLookupOptions options, ILogger<GeoNamesGeocoder> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public int Count => _coordinates.Count;

    /// <summary>Downloads (cached) and loads the US + CA postal datasets into memory.</summary>
    public async Task LoadAsync(string cacheDirectory, bool offline, CancellationToken cancellationToken)
    {
        await LoadCountryAsync(_options.GeoNamesUsUrl, cacheDirectory, "geonames-US.zip", offline, cancellationToken);
        await LoadCountryAsync(_options.GeoNamesCaUrl, cacheDirectory, "geonames-CA.zip", offline, cancellationToken);
        _logger.LogInformation("GeoNames: {Count:N0} postal coordinates loaded", _coordinates.Count);
    }

    private async Task LoadCountryAsync(string url, string cacheDirectory, string fileName, bool offline, CancellationToken cancellationToken)
    {
        var path = await CachedDownloader.EnsureAsync(_httpClient, url, cacheDirectory, fileName,
            _options.MaxCacheAge, offline, _logger, cancellationToken);
        if (path is null)
        {
            return;
        }

        using var archive = ZipFile.OpenRead(path);
        // GeoNames zips bundle a readme.txt alongside the data file (e.g. US.txt) — skip the readme.
        var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
            !e.Name.Equals("readme.txt", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            _logger.LogWarning("GeoNames: no data file in {File}", fileName);
            return;
        }

        using var reader = new StreamReader(entry.Open());
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split('\t');
            if (fields.Length <= LonCol)
            {
                continue;
            }

            if (!double.TryParse(fields[LatCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(fields[LonCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                continue;
            }

            var key = Key(fields[CountryCol].Trim(), fields[PostalCol].Trim());
            // First occurrence wins; multiple place names can share a postal code.
            _coordinates.TryAdd(key, (lat, lon));
        }
    }

    /// <summary>Returns the Maidenhead grid for a licensee's postal code, or null if not found.</summary>
    public string? GridFor(string country, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        return _coordinates.TryGetValue(Key(country, postalCode), out var coord)
            ? Maidenhead.Encode(coord.Lat, coord.Lon)
            : null;
    }

    private static string Key(string country, string postalCode)
    {
        var normalized = postalCode.Replace(" ", "").ToUpperInvariant();
        // GeoNames only has the 3-char FSA for Canada.
        if (country == "CA" && normalized.Length > 3)
        {
            normalized = normalized[..3];
        }

        return $"{country}:{normalized}";
    }
}
