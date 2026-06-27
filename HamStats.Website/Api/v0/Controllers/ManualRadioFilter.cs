using HamStats.Data;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

// N1MM renames a radio to "Manual" when it loses its CAT connection. Those entries are noise on the
// dashboard, so an operator-toggleable setting (on by default) hides them from the read APIs.
public static class ManualRadioFilter
{
    public const string Name = "Manual";
    public const string SettingKey = "FilterManualRadios";
    public const bool Default = true;

    public static async Task<bool> IsEnabled(HamStatsDbContext context)
    {
        var value = await context.Settings
            .Where(s => s.Key == SettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        return value is null ? Default : value == "true";
    }
}
