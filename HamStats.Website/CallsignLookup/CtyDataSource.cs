using System.Globalization;
using System.Text;

namespace HamStats.Website.CallsignLookup;

/// <summary>One prefix (or exact-callsign) entry resolved from cty.dat, already gridded.</summary>
public record CtyEntry(string Prefix, string Grid, string Country, string? Continent, bool IsExact);

/// <summary>
/// AD1C "country files" (cty.dat) from country-files.com — the same prefix database N1MM uses. It
/// maps callsign prefixes to a DXCC entity and a representative coordinate, giving worldwide ("DX")
/// coverage that the per-licensee FCC/ISED dumps lack. Unlike <see cref="ICallsignSource"/> this
/// yields already-gridded prefixes (cty.dat carries coordinates, so no GeoNames step is needed).
/// </summary>
public class CtyDataSource
{
    private readonly HttpClient _httpClient;
    private readonly CallsignLookupOptions _options;
    private readonly ILogger<CtyDataSource> _logger;

    public CtyDataSource(HttpClient httpClient, CallsignLookupOptions options, ILogger<CtyDataSource> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string Source => "CTY";

    public Task<string?> DownloadAsync(string cacheDirectory, bool offline, CancellationToken cancellationToken) =>
        CachedDownloader.EnsureAsync(_httpClient, _options.CtyUrl, cacheDirectory, "cty.dat",
            _options.MaxCacheAge, offline, _logger, cancellationToken);

    /// <summary>
    /// Parses cty.dat. Each record is a header line (entity at column 0, fields separated by ':')
    /// followed by indented prefix lines whose comma-separated tokens run until a ';'. Tokens may
    /// carry modifiers: '=' marks an exact callsign, &lt;lat/lon&gt; overrides the coordinate, and
    /// (), [], {}, ~~ carry zone/continent/offset overrides we strip. cty.dat longitude is
    /// positive-west, so the real longitude is its negation.
    /// </summary>
    public IEnumerable<CtyEntry> Parse(string archivePath)
    {
        using var reader = new StreamReader(archivePath);
        string? line;
        string country = "", continent = "";
        double recordLat = 0, recordLon = 0;
        var tokens = new StringBuilder();
        var inRecord = false;

        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // Header lines start at column 0; prefix lines are indented.
            if (!char.IsWhiteSpace(line[0]))
            {
                var fields = line.Split(':');
                if (fields.Length < 8)
                {
                    inRecord = false;
                    continue;
                }

                country = fields[0].Trim();
                continent = fields[3].Trim();
                recordLat = ParseCoord(fields[4]);
                recordLon = ParseCoord(fields[5]);
                tokens.Clear();
                inRecord = true;
                continue;
            }

            if (!inRecord)
            {
                continue;
            }

            tokens.Append(line.Trim());
            if (line.Contains(';'))
            {
                foreach (var entry in ParseTokens(tokens.ToString(), country, continent, recordLat, recordLon))
                {
                    yield return entry;
                }

                inRecord = false;
            }
        }
    }

    private static IEnumerable<CtyEntry> ParseTokens(string list, string country, string continent, double lat, double lon)
    {
        foreach (var raw in list.TrimEnd(';', ' ').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw;
            var exact = token.StartsWith('=');
            if (exact)
            {
                token = token[1..];
            }

            var entryLat = lat;
            var entryLon = lon;
            var open = token.IndexOf('<');
            if (open >= 0)
            {
                var close = token.IndexOf('>', open);
                if (close > open)
                {
                    var coords = token[(open + 1)..close].Split('/');
                    if (coords.Length == 2 && TryParseCoord(coords[0], out var oLat) && TryParseCoord(coords[1], out var oLon))
                    {
                        entryLat = oLat;
                        entryLon = oLon;
                    }
                }
            }

            var prefix = StripModifiers(token).ToUpperInvariant();
            if (prefix.Length == 0)
            {
                continue;
            }

            // cty.dat longitude is positive-west; Maidenhead expects positive-east.
            var grid = Maidenhead.Encode(entryLat, -entryLon);
            yield return new CtyEntry(prefix, grid, country, continent.Length > 0 ? continent : null, exact);
        }
    }

    /// <summary>Removes any (), [], {}, &lt;&gt; and ~~ modifier groups, leaving the bare prefix.</summary>
    private static string StripModifiers(string token)
    {
        var sb = new StringBuilder(token.Length);
        var bracket = 0;
        var inTilde = false;
        foreach (var c in token)
        {
            if (c == '~')
            {
                inTilde = !inTilde;
            }
            else if (c is '(' or '[' or '{' or '<')
            {
                bracket++;
            }
            else if (c is ')' or ']' or '}' or '>')
            {
                if (bracket > 0)
                {
                    bracket--;
                }
            }
            else if (!inTilde && bracket == 0)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static double ParseCoord(string value) => TryParseCoord(value, out var d) ? d : 0;

    private static bool TryParseCoord(string value, out double result) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
