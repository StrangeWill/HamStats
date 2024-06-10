namespace HamStats.Data.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class Contact : IEntityTypeConfiguration<Contact>
{
    public Guid Id { get; set; }

    public void Configure(EntityTypeBuilder<Contact> builder)
    {
    }
}