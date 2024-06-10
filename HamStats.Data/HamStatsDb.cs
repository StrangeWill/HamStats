namespace HamStats.Data;
using Microsoft.EntityFrameworkCore;
using Models;

public class HamStatsDb : DbContext
{
    public DbSet<Antenna> Antennas => Set<Antenna>();
    public DbSet<AntennaLog> AntennaLogs => Set<AntennaLog>();

    public DbSet<N1MMContact> N1MMContacts => Set<N1MMContact>();

    public DbSet<N1MMRadio> N1MMRadios => Set<N1MMRadio>();

    public DbSet<Radio> Radios => Set<Radio>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<ScoreBreakdown> ScoreBreakdowns => Set<ScoreBreakdown>();


    public HamStatsDb(
        DbContextOptions<HamStatsDb> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(HamStatsDb).Assembly);
    }
}