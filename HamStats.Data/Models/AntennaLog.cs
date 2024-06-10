namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AntennaLog : IEntityTypeConfiguration<AntennaLog>
{
    public Guid Id { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public void Configure(EntityTypeBuilder<AntennaLog> builder)
    {
    }
}