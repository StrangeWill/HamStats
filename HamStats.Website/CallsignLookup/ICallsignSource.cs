namespace HamStats.Website.CallsignLookup;

/// <summary>One licensee row distilled from a bulk dump, before geocoding.</summary>
public record RawLicense(
    string Callsign,
    string Country,
    string? PostalCode,
    string? Name,
    string? Region);

/// <summary>
/// A bulk amateur-license dataset (e.g. FCC, ISED). Implementations download a (cacheable) archive
/// and yield the licensee rows we care about. Add a country by adding an implementation and
/// registering it in <see cref="CallsignImportJob"/>.
/// </summary>
public interface ICallsignSource
{
    /// <summary>Stable identifier stored on each row, e.g. "FCC".</summary>
    string Source { get; }

    /// <summary>
    /// Ensures the source archive is present in <paramref name="cacheDirectory"/>, downloading it
    /// unless a cached copy is still within <see cref="CallsignLookupOptions.MaxCacheAge"/> or we
    /// are <paramref name="offline"/>. Returns the path to the cached archive, or null if nothing
    /// is available (offline with no cache).
    /// </summary>
    Task<string?> DownloadAsync(string cacheDirectory, bool offline, CancellationToken cancellationToken);

    /// <summary>Parses the cached archive into licensee rows.</summary>
    IEnumerable<RawLicense> Parse(string archivePath);
}
