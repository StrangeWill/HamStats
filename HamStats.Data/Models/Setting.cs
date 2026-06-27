namespace HamStats.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Key/value app settings (e.g. TimeZone). Wiped with the rest of the DB on startup.
public class Setting : IEntityTypeConfiguration<Setting>
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.HasKey(s => s.Key);
    }
}
