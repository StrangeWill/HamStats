using System.IO.Compression;
using System.Text.RegularExpressions;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Canadian amateur licenses from the ISED delimited dump (amateur_delim.zip). The file is
/// comma-delimited but address fields can themselves contain commas, so rather than trust column
/// positions past the callsign we scan each row for the Canadian postal-code token.
/// </summary>
public partial class IsedAmateurSource : ICallsignSource
{
    private readonly HttpClient _httpClient;
    private readonly CallsignLookupOptions _options;
    private readonly ILogger<IsedAmateurSource> _logger;

    public IsedAmateurSource(HttpClient httpClient, CallsignLookupOptions options, ILogger<IsedAmateurSource> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string Source => "ISED";

    public Task<string?> DownloadAsync(string cacheDirectory, bool offline, CancellationToken cancellationToken) =>
        CachedDownloader.EnsureAsync(_httpClient, _options.IsedAmateurUrl, cacheDirectory, "amateur_delim.zip",
            _options.MaxCacheAge, offline, _logger, cancellationToken);

    public IEnumerable<RawLicense> Parse(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault();
        if (entry is null)
        {
            _logger.LogWarning("ISED: no data file in {Archive}", archivePath);
            yield break;
        }

        using var reader = new StreamReader(entry.Open());
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split(',');
            if (fields.Length < 2)
            {
                continue;
            }

            var call = fields[0].Trim().Trim('"').ToUpperInvariant();
            // Skip a header row or blank callsigns.
            if (call.Length == 0 || call.Equals("CALLSIGN", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var postalMatch = PostalCode().Match(line);
            if (!postalMatch.Success)
            {
                continue;
            }

            var postal = postalMatch.Value.Replace(" ", "").ToUpperInvariant();
            var name = $"{fields[1].Trim().Trim('"')} {(fields.Length > 2 ? fields[2].Trim().Trim('"') : "")}".Trim();

            yield return new RawLicense(call, "CA", postal, name.Length > 0 ? name : null, null);
        }
    }

    [GeneratedRegex(@"[A-Za-z]\d[A-Za-z]\s?\d[A-Za-z]\d")]
    private static partial Regex PostalCode();
}
