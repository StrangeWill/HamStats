namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class VFO : IEntityTypeConfiguration<VFO>
{
    public Guid Id { get; set; }

    public Guid? RadioId { get; set; }

    public Radio? Radio { get; set; }

    public string? Name { get; set; }

    public string? RxFrequency { get; set; }

    public string? TxFrequency { get; set; }

    public N1MMRadio? N1MMRadio { get; set; }

    public void Configure(EntityTypeBuilder<VFO> builder)
    {
    }
}