namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class Contact : IEntityTypeConfiguration<Contact>
{
    public Guid Id { get; set; }

    public Guid RadioId { get; set; }

    public Radio Radio { get; set; }

    public Guid N1MMContactId { get; set; }

    public N1MMContact N1MMContact { get; set; }

    public DateTime Date { get; set; }


    public required string FromCall { get; set; }

    public required string ToCall { get; set; }

    public required string Band { get; set; }

    public required string RxFrequency { get; set; }

    public required string TxFrequency { get; set; }

    public string? Mode { get; set; }

    public string? Class { get; set; }

    public string? Section { get; set; }

    public string? Operator { get; set; }


    public void Configure(EntityTypeBuilder<Contact> builder)
    {
    }
}