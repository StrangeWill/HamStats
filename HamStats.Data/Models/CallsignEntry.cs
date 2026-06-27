namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Offline callsign → Maidenhead grid lookup, built from bulk license dumps (FCC/ISED) joined to
/// postal-code coordinates. Populated by the Hangfire import job; read by the ingestion pipeline to
/// backfill a contact's grid when N1MM doesn't supply one (e.g. no internet at the venue).
/// </summary>
public class CallsignEntry : IEntityTypeConfiguration<CallsignEntry>
{
    /// <summary>Uppercased callsign. Primary key.</summary>
    public required string Callsign { get; set; }

    /// <summary>Maidenhead grid (typically 6 characters), derived from the licensee's postal code.</summary>
    public required string Grid { get; set; }

    public string? Name { get; set; }

    /// <summary>State (US) or province (CA).</summary>
    public string? Region { get; set; }

    /// <summary>ISO-ish country tag, e.g. "US" or "CA".</summary>
    public required string Country { get; set; }

    /// <summary>Origin dataset, e.g. "FCC" or "ISED".</summary>
    public required string Source { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void Configure(EntityTypeBuilder<CallsignEntry> builder)
    {
        builder.HasKey(c => c.Callsign);
    }
}
