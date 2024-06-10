namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class N1MMContact : IEntityTypeConfiguration<N1MMContact>
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    public required string FromCall { get; set; }

    public required string ToCall { get; set; }

    public required string Band { get; set; }

    public required string RxFrequency { get; set; }

    public required string TxFrequency { get; set; }

    public string Mode { get; set; }

    public string CountryPrefix { get; set; }

    public string Sent { get; set; }

    public string Receive { get; set; }

    public string Exchange { get; set; }

    public string N1MMId { get; set; }

    public void Configure(EntityTypeBuilder<N1MMContact> builder)
    {
    }
}