namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ScoreBreakdown : IEntityTypeConfiguration<ScoreBreakdown>
{
    public Guid Id { get; set; }

    public Guid ScoreId { get; set; }

    public Score Score { get; set; }

    public string Band { get; set; }

    public string Mode { get; set; }

    public int QSOs { get; set; }

    public int Points { get; set; }

    public void Configure(EntityTypeBuilder<ScoreBreakdown> builder)
    {
    }
}