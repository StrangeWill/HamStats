using HamStats.Data;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Resolves a worked station's Maidenhead grid for mapping. Used both live by the N1MM watcher as
/// contacts arrive and by the settings-page re-resolve admin action over already-stored contacts.
/// </summary>
public class GridResolver
{
    /// <summary>
    /// Resolves the worked station's Maidenhead grid: prefer what N1MM supplied, otherwise fall back
    /// to the offline <see cref="CallsignEntry"/> lookup table (populated by the Hangfire import job),
    /// and finally — for Field Day stations that exchange an ARRL/RAC section but no grid — to the
    /// section's centroid.
    /// </summary>
    public async Task<string?> ResolveGrid(HamStatsDbContext hamStatsDbContext, string? provided, string? call, string? section)
    {
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided;
        }

        if (string.IsNullOrWhiteSpace(call))
        {
            return ArrlSections.GridFor(section);
        }

        var key = call.ToUpperInvariant();

        // Prefer a precise per-licensee grid (FCC/ISED postal lookup) — but Field Day is about the section
        // a station reports, not where they live. If the licensee's home state contradicts the section
        // (they travelled for the event), snap to the section centroid so the dot lands where the contact
        // actually counts. Canadian licences carry no Region, so they keep their precise grid.
        var entry = await hamStatsDbContext.Callsigns
            .Where(c => c.Callsign == key)
            .Select(c => new { c.Grid, c.Region })
            .FirstOrDefaultAsync();
        if (entry?.Grid is not null)
        {
            var sectionState = ArrlSections.StateFor(section);
            if (sectionState is not null && !string.IsNullOrWhiteSpace(entry.Region)
                && !string.Equals(sectionState, entry.Region.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return ArrlSections.GridFor(section) ?? entry.Grid;
            }

            return entry.Grid;
        }

        // Fall back to the cty.dat prefix table for DX coverage: an exact-callsign exception wins,
        // otherwise the longest matching leading prefix. Naive leading match (ignores portable
        // indicators) but sufficient to place a contact at its DXCC entity.
        var prefixGrid = await hamStatsDbContext.CallsignPrefixes
            .Where(p => (p.IsExact && p.Prefix == key) || (!p.IsExact && EF.Functions.Like(key, p.Prefix + "%")))
            .OrderByDescending(p => p.IsExact)
            .ThenByDescending(p => p.Prefix.Length)
            .Select(p => p.Grid)
            .FirstOrDefaultAsync();
        if (prefixGrid is not null)
        {
            return prefixGrid;
        }

        // Last resort: Field Day stations exchange an ARRL/RAC section, not a grid, and many won't be
        // in the callsign tables — place them at the section's centroid so they still map.
        return ArrlSections.GridFor(section);
    }
}
