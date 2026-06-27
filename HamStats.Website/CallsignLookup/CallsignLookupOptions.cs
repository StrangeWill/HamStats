namespace HamStats.Website.CallsignLookup;

/// <summary>Bound from the "CallsignLookup" section of appsettings.json.</summary>
public class CallsignLookupOptions
{
    public const string SectionName = "CallsignLookup";

    /// <summary>Master switch for the import job + recurring schedule.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Where downloaded dumps are cached. Relative paths resolve under the content root.</summary>
    public string CacheDirectory { get; set; } = "callsign-cache";

    /// <summary>Re-download a cached archive only once it is older than this.</summary>
    public TimeSpan MaxCacheAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Cron for the recurring refresh job. Default: weekly, Sunday 03:00.</summary>
    public string RefreshCron { get; set; } = "0 3 * * 0";

    /// <summary>Enqueue a build-from-cache on startup so the table repopulates offline after a wipe.</summary>
    public bool BuildOnStartup { get; set; } = true;

    public string FccAmateurUrl { get; set; } = "https://data.fcc.gov/download/pub/uls/complete/l_amat.zip";

    public string IsedAmateurUrl { get; set; } = "https://apc-cap.ic.gc.ca/datafiles/amateur_delim.zip";

    public string GeoNamesUsUrl { get; set; } = "https://download.geonames.org/export/zip/US.zip";

    public string GeoNamesCaUrl { get; set; } = "https://download.geonames.org/export/zip/CA.zip";

    /// <summary>AD1C country files — prefix → DXCC entity grid for worldwide ("DX") callsigns.</summary>
    public string CtyUrl { get; set; } = "https://www.country-files.com/cty/cty.dat";
}
