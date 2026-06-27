using Microsoft.AspNetCore.SignalR;

namespace HamStats.Website.Hubs;

// Dashboard clients connect here and receive "DataChanged" nudges. No client-to-server calls —
// on connect the client does its initial REST load, then refetches a dataset when nudged.
public class DashboardHub : Hub
{
}
