namespace HamStats.Data.Models;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class Radio : IEntityTypeConfiguration<Radio>
{
    public Guid Id { get; set; }

    public string? Operator { get; set; }

    public required string Name { get; set; }

    public ICollection<VFO> VFOs { get; set; } = null!;

    public void Configure(EntityTypeBuilder<Radio> builder)
    {
    }
}