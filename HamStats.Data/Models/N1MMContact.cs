namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class N1MMContact : IEntityTypeConfiguration<N1MMContact>
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    public Contact Contact { get; set; }

    public required string FromCall { get; set; }

    public required string ToCall { get; set; }

    public required string Band { get; set; }

    public required string RxFrequency { get; set; }

    public required string TxFrequency { get; set; }

    public string Mode { get; set; }

    public string? CountryPrefix { get; set; }

    public string? Sent { get; set; }

    public string? Receive { get; set; }

    public string? Exchange { get; set; }

    public string? Section { get; set; }

    // Raw grid N1MM sent (usually empty for Field Day). Kept verbatim so the resolved Contact.Gridsquare
    // can be recomputed later without losing an operator-supplied grid to a lookup guess.
    public string? Gridsquare { get; set; }

    public string? Operator { get; set; }

    public string N1MMId { get; set; }

    public void Configure(EntityTypeBuilder<N1MMContact> builder)
    {
        builder.HasOne(e => e.Contact).WithOne(e => e.N1MMContact).HasForeignKey<Contact>(e => e.N1MMContactId);
    }
}