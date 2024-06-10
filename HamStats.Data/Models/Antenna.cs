namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class Antenna : IEntityTypeConfiguration<Antenna>
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public void Configure(EntityTypeBuilder<Antenna> builder)
    {
    }
}