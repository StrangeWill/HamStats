namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class N1MMRadio : IEntityTypeConfiguration<N1MMRadio>
{
    public Guid Id { get; set; }

    public Guid? VFOId { get; set; }

    public VFO? VFO { get; set; }

    public required string StationName { get; set; }

    public required string RadioName { get; set; }

    public int RadioNumber { get; set; }

    public string? RxFrequency { get; set; }

    public string? TxFrequency { get; set; }

    public DateTime LastSeen { get; set; }

    public void Configure(EntityTypeBuilder<N1MMRadio> builder)
    {
        builder.HasOne(e => e.VFO).WithOne(e => e.N1MMRadio).HasForeignKey<N1MMRadio>(e => e.VFOId);
    }
}