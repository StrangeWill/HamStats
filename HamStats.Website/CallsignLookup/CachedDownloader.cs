namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Shared download-with-disk-cache helper. Sources use this so a venue with no internet can rebuild
/// the lookup table from the last cached archives.
/// </summary>
public static class CachedDownloader
{
    /// <summary>
    /// Ensures <paramref name="fileName"/> exists in <paramref name="cacheDirectory"/>. Downloads from
    /// <paramref name="url"/> unless a cached copy is younger than <paramref name="maxCacheAge"/> or
    /// <paramref name="offline"/> is set. Returns the cached path, or null when nothing is available
    /// (offline/failed with no cache). Downloads stream to a temp file and atomically replace the
    /// cache entry so a half-written archive never poisons the cache.
    /// </summary>
    public static async Task<string?> EnsureAsync(
        HttpClient httpClient,
        string url,
        string cacheDirectory,
        string fileName,
        TimeSpan maxCacheAge,
        bool offline,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheDirectory);
        var cachedPath = Path.Combine(cacheDirectory, fileName);
        var cacheExists = File.Exists(cachedPath);
        var cacheFresh = cacheExists && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachedPath) < maxCacheAge;

        if (offline || cacheFresh)
        {
            if (cacheExists)
            {
                logger.LogInformation("Using cached {File} ({Reason})", fileName, offline ? "offline" : "fresh");
                return cachedPath;
            }

            logger.LogWarning("No cached {File} available and download skipped ({Reason})",
                fileName, offline ? "offline" : "fresh-check");
            return null;
        }

        try
        {
            logger.LogInformation("Downloading {Url} → {File}", url, fileName);
            var tempPath = cachedPath + ".tmp";
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var destination = File.Create(tempPath);
                await source.CopyToAsync(destination, cancellationToken);
            }

            File.Move(tempPath, cachedPath, overwrite: true);
            logger.LogInformation("Downloaded {File} ({Bytes:N0} bytes)", fileName, new FileInfo(cachedPath).Length);
            return cachedPath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed downloading {Url}", url);
            if (cacheExists)
            {
                logger.LogWarning("Falling back to stale cached {File}", fileName);
                return cachedPath;
            }

            return null;
        }
    }
}
