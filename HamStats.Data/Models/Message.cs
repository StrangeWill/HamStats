namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// A short operator-to-operator chat message, tagged with the sending radio.
public class Message : IEntityTypeConfiguration<Message>
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    /// <summary>The radio the sender selected as their identity (client-side, via localStorage).</summary>
    public required string Radio { get; set; }

    public string? Operator { get; set; }

    public required string Text { get; set; }

    public void Configure(EntityTypeBuilder<Message> builder)
    {
    }
}
