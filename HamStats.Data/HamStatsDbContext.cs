namespace HamStats.Data;
using System;
using Microsoft.EntityFrameworkCore;
using Models;

public class HamStatsDbContext : DbContext
{
    public DbSet<Antenna> Antennas => Set<Antenna>();
    public DbSet<AntennaLog> AntennaLogs => Set<AntennaLog>();

    public DbSet<CallsignEntry> Callsigns => Set<CallsignEntry>();

    public DbSet<CallsignPrefix> CallsignPrefixes => Set<CallsignPrefix>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<N1MMContact> N1MMContacts => Set<N1MMContact>();

    public DbSet<N1MMRadio> N1MMRadios => Set<N1MMRadio>();

    public DbSet<Radio> Radios => Set<Radio>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<ScoreBreakdown> ScoreBreakdowns => Set<ScoreBreakdown>();

    public DbSet<Setting> Settings => Set<Setting>();


    public HamStatsDbContext(
        DbContextOptions<HamStatsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(HamStatsDbContext).Assembly);

        // SQLite loses DateTimeKind on read (comes back Unspecified). All stored times are UTC,
        // so mark them UTC on read — this makes the API serialize them with a trailing 'Z' so the
        // client can convert to the configured display time zone correctly.
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcConverter);
                }
            }
        }
    }
}