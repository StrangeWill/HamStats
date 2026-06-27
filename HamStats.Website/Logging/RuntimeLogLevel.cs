namespace HamStats.Website.Logging;

/// <summary>In-memory config source overriding Logging:AsioAppLog:LogLevel at runtime; Set/Reset raise a reload token so the AsioAppLog provider re-filters without a restart.</summary>
public sealed class RuntimeLogLevelSource : IConfigurationSource
{
    public RuntimeLogLevelProvider Provider { get; } = new();

    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}

public sealed class RuntimeLogLevelProvider : ConfigurationProvider
{
    private const string Prefix = "Logging:AsioAppLog:LogLevel:";

    public void Set(string category, string level)
    {
        Data[Prefix + category] = level;
        OnReload();
    }

    public void Reset(string category)
    {
        Data.Remove(Prefix + category);
        OnReload();
    }
}
