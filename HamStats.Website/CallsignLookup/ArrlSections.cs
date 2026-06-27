namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Maps ARRL/RAC sections (the Field Day exchange) to a representative coordinate so a contact that
/// sends a section instead of a grid — and isn't in the callsign tables — can still be placed on the
/// map. Coordinates are a central city per section; the grid is therefore section-level, not precise.
/// </summary>
public static class ArrlSections
{
    // Section abbreviation → (latitude, longitude) of a central point.
    private static readonly Dictionary<string, (double Lat, double Lon)> Centroids = new(StringComparer.OrdinalIgnoreCase)
    {
        // New England
        ["CT"] = (41.76, -72.67),
        ["EMA"] = (42.36, -71.06),
        ["ME"] = (44.31, -69.78),
        ["NH"] = (43.21, -71.54),
        ["RI"] = (41.82, -71.41),
        ["VT"] = (44.26, -72.58),
        ["WMA"] = (42.10, -72.59),
        // Hudson
        ["ENY"] = (42.65, -73.75),
        ["NLI"] = (40.73, -73.50),
        ["NNJ"] = (40.74, -74.17),
        // Atlantic
        ["NNY"] = (44.30, -75.00),
        ["SNJ"] = (39.66, -74.80),
        ["WNY"] = (42.89, -78.88),
        ["DE"] = (39.16, -75.52),
        ["EPA"] = (40.60, -75.70),
        ["MDC"] = (39.29, -76.61),
        ["WPA"] = (40.44, -79.99),
        // Central
        ["IL"] = (39.78, -89.65),
        ["IN"] = (39.77, -86.16),
        ["WI"] = (43.07, -89.40),
        // Dakota
        ["MN"] = (44.95, -93.09),
        ["ND"] = (46.81, -100.78),
        ["SD"] = (44.37, -100.35),
        // Delta
        ["AR"] = (34.74, -92.33),
        ["LA"] = (30.46, -91.15),
        ["MS"] = (32.30, -90.18),
        ["TN"] = (36.16, -86.78),
        // Great Lakes
        ["KY"] = (38.20, -84.87),
        ["MI"] = (42.73, -84.56),
        ["OH"] = (39.96, -82.99),
        // Midwest
        ["IA"] = (41.59, -93.62),
        ["KS"] = (39.05, -95.69),
        ["MO"] = (38.58, -92.17),
        ["NE"] = (40.81, -96.68),
        // New England (rest) handled above; Northwestern
        ["AK"] = (61.22, -149.90),
        ["EWA"] = (47.66, -117.43),
        ["ID"] = (43.62, -116.20),
        ["MT"] = (46.59, -112.04),
        ["OR"] = (44.94, -123.04),
        ["WWA"] = (47.61, -122.33),
        // Pacific
        ["EB"] = (37.80, -122.27),
        ["SF"] = (37.77, -122.42),
        ["SCV"] = (37.34, -121.89),
        ["SJV"] = (36.74, -119.77),
        ["SV"] = (38.58, -121.49),
        ["NV"] = (39.16, -119.77),
        ["PAC"] = (21.31, -157.86),
        // Roanoke
        ["NC"] = (35.78, -78.64),
        ["SC"] = (34.00, -81.03),
        ["VA"] = (37.54, -77.44),
        ["WV"] = (38.35, -81.63),
        // Rocky Mountain
        ["CO"] = (39.74, -104.99),
        ["NM"] = (35.08, -106.65),
        ["UT"] = (40.76, -111.89),
        ["WY"] = (41.14, -104.82),
        // Southeastern
        ["AL"] = (32.38, -86.30),
        ["GA"] = (33.75, -84.39),
        ["NFL"] = (30.33, -81.66),
        ["SFL"] = (25.76, -80.19),
        ["WCF"] = (27.95, -82.46),
        ["PR"] = (18.47, -66.10),
        ["VI"] = (18.34, -64.93),
        // Southwestern
        ["AZ"] = (33.45, -112.07),
        ["LAX"] = (34.05, -118.24),
        ["ORG"] = (33.75, -117.87),
        ["SB"] = (34.42, -119.70),
        ["SDG"] = (32.72, -117.16),
        // West Gulf
        ["NTX"] = (32.78, -96.80),
        ["OK"] = (35.47, -97.52),
        ["STX"] = (30.27, -97.74),
        ["WTX"] = (31.99, -102.08),
        // Canada (RAC)
        ["AB"] = (51.05, -114.07),
        ["BC"] = (49.28, -123.12),
        ["GTA"] = (43.65, -79.38),
        ["MB"] = (49.90, -97.14),
        ["NL"] = (47.56, -52.71),
        ["MAR"] = (44.65, -63.58),
        ["NT"] = (62.45, -114.37),
        ["ONE"] = (45.42, -75.70),
        ["ONN"] = (46.49, -80.99),
        ["ONS"] = (42.98, -81.25),
        ["QC"] = (45.50, -73.57),
        ["SK"] = (50.45, -104.62),
    };

    /// <summary>Returns a section-level Maidenhead grid for the section, or null if unknown (e.g. "DX").</summary>
    public static string? GridFor(string? section)
    {
        if (string.IsNullOrWhiteSpace(section) || !Centroids.TryGetValue(section.Trim(), out var c))
        {
            return null;
        }

        return Maidenhead.Encode(c.Lat, c.Lon);
    }
}
