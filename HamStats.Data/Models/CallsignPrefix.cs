namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Callsign-prefix → grid table built from the AD1C country files (cty.dat). Unlike
/// <see cref="CallsignEntry"/> (one precise row per licensee), each row covers a whole DXCC entity
/// at its representative coordinate, so any worldwide ("DX") callsign resolves to at least a
/// country-level grid by longest-prefix match. Exact-callsign exceptions in cty.dat set
/// <see cref="IsExact"/> and match the full callsign only.
/// </summary>
public class CallsignPrefix : IEntityTypeConfiguration<CallsignPrefix>
{
    public int Id { get; set; }

    /// <summary>Uppercased prefix, or full callsign when <see cref="IsExact"/>.</summary>
    public required string Prefix { get; set; }

    /// <summary>Maidenhead grid of the DXCC entity's representative coordinate.</summary>
    public required string Grid { get; set; }

    /// <summary>DXCC entity name, e.g. "Germany".</summary>
    public required string Country { get; set; }

    /// <summary>Continent code, e.g. "EU".</summary>
    public string? Continent { get; set; }

    /// <summary>True when this row is a full-callsign exception, not a leading prefix.</summary>
    public bool IsExact { get; set; }

    /// <summary>Origin dataset, e.g. "CTY".</summary>
    public required string Source { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void Configure(EntityTypeBuilder<CallsignPrefix> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.Prefix);
    }
}
