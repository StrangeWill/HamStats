namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Converts a latitude/longitude into a Maidenhead locator (grid square). We emit 6 characters
/// (field + square + subsquare), which is ~3×4 mi — plenty given we geocode from postal-code
/// centroids.
/// </summary>
public static class Maidenhead
{
    /// <summary>
    /// Returns the 6-character Maidenhead locator for a coordinate, e.g. (42.36, -71.06) → "FN42kj".
    /// </summary>
    public static string Encode(double latitude, double longitude)
    {
        // Shift to all-positive ranges: longitude 0..360, latitude 0..180.
        var lon = longitude + 180.0;
        var lat = latitude + 90.0;

        var grid = new char[6];

        // Field: 20° lon / 10° lat, letters A-R.
        grid[0] = (char)('A' + (int)(lon / 20.0));
        grid[1] = (char)('A' + (int)(lat / 10.0));
        lon %= 20.0;
        lat %= 10.0;

        // Square: 2° lon / 1° lat, digits 0-9.
        grid[2] = (char)('0' + (int)(lon / 2.0));
        grid[3] = (char)('0' + (int)(lat / 1.0));
        lon %= 2.0;
        lat %= 1.0;

        // Subsquare: 5' lon / 2.5' lat, letters a-x.
        grid[4] = (char)('a' + (int)(lon / (2.0 / 24.0)));
        grid[5] = (char)('a' + (int)(lat / (1.0 / 24.0)));

        return new string(grid);
    }
}
