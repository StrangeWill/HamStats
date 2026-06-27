using Hangfire.Dashboard;

namespace HamStats.Website.CallsignLookup;

/// <summary>
/// Allows unauthenticated access to the Hangfire dashboard. HamStats is a LAN tool with no auth
/// (mirrors the SignalR log hub), and Hangfire otherwise blocks non-local requests by default.
/// </summary>
public class AllowAllDashboardAuthorization : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
