using System.IO.Compression;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// US amateur licenses from the weekly FCC ULS complete dump (l_amat.zip). We read active licenses
/// (HD.dat status "A") and join their entity records (EN.dat) for callsign, name and ZIP.
/// </summary>
public class FccAmateurSource : ICallsignSource
{
    // EN.dat (entity) pipe-delimited column indexes.
    private const int EnSystemId = 1;
    private const int EnCallSign = 4;
    private const int EnEntityName = 7;
    private const int EnFirstName = 8;
    private const int EnLastName = 10;
    private const int EnState = 17;
    private const int EnZip = 18;

    // HD.dat (license header) pipe-delimited column indexes.
    private const int HdSystemId = 1;
    private const int HdStatus = 5;

    private readonly HttpClient _httpClient;
    private readonly CallsignLookupOptions _options;
    private readonly ILogger<FccAmateurSource> _logger;

    public FccAmateurSource(HttpClient httpClient, CallsignLookupOptions options, ILogger<FccAmateurSource> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string Source => "FCC";

    public Task<string?> DownloadAsync(string cacheDirectory, bool offline, CancellationToken cancellationToken) =>
        CachedDownloader.EnsureAsync(_httpClient, _options.FccAmateurUrl, cacheDirectory, "l_amat.zip",
            _options.MaxCacheAge, offline, _logger, cancellationToken);

    public IEnumerable<RawLicense> Parse(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        var activeIds = ReadActiveSystemIds(archive);
        _logger.LogInformation("FCC: {Count:N0} active licenses", activeIds.Count);

        var entityEntry = archive.GetEntry("EN.dat");
        if (entityEntry is null)
        {
            _logger.LogWarning("FCC: EN.dat missing from {Archive}", archivePath);
            yield break;
        }

        using var reader = new StreamReader(entityEntry.Open());
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split('|');
            if (fields.Length <= EnZip)
            {
                continue;
            }

            // When HD.dat is absent activeIds is empty — fall back to keeping every entity rather
            // than dropping the whole dataset.
            if (activeIds.Count > 0 && !activeIds.Contains(fields[EnSystemId]))
            {
                continue;
            }

            var call = fields[EnCallSign].Trim().ToUpperInvariant();
            if (call.Length == 0)
            {
                continue;
            }

            var zip = fields[EnZip].Trim();
            if (zip.Length > 5)
            {
                zip = zip[..5];
            }

            var entityName = fields[EnEntityName].Trim();
            var name = entityName.Length > 0
                ? entityName
                : $"{fields[EnFirstName].Trim()} {fields[EnLastName].Trim()}".Trim();

            yield return new RawLicense(call, "US", zip, name.Length > 0 ? name : null, fields[EnState].Trim());
        }
    }

    private HashSet<string> ReadActiveSystemIds(ZipArchive archive)
    {
        var ids = new HashSet<string>();
        var headerEntry = archive.GetEntry("HD.dat");
        if (headerEntry is null)
        {
            _logger.LogWarning("FCC: HD.dat missing; cannot filter to active licenses");
            return ids;
        }

        using var reader = new StreamReader(headerEntry.Open());
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split('|');
            if (fields.Length > HdStatus && fields[HdStatus].Trim() == "A")
            {
                ids.Add(fields[HdSystemId]);
            }
        }

        return ids;
    }
}
