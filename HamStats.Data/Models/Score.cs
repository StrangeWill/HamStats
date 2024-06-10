namespace HamStats.Data.Models;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class Score : IEntityTypeConfiguration<Score>
{
    public Guid Id { get; set; }

    public string? Contest { get; set; }

    public string? Call { get; set; }

    public string? Ops { get; set; }

    public string? Power { get; set; }

    public string? Assisted { get; set; }

    public string? Transmitter { get; set; }

    public string? Bands { get; set; }

    public string? Mode { get; set; }

    public string? Overlay { get; set; }

    public string? Club { get; set; }

    public string? DxccCountry { get; set; }

    public string? CqZone { get; set; }

    public string? Iaruzone { get; set; }

    public string? ArrlSection { get; set; }

    public string? Stprvoth { get; set; }

    public string? Grid6 { get; set; }

    public int? Value { get; set; }

    public ICollection<ScoreBreakdown> Breakdown { get; set; }

    public void Configure(EntityTypeBuilder<Score> builder)
    {
    }
}