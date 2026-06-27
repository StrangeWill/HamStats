using Microsoft.AspNetCore.SignalR;

namespace HamStats.Website.Hubs;

// Pushes lightweight "this dataset changed" nudges to dashboard clients so they refetch only the
// affected dataset, only when something actually changed (instead of polling on a fixed interval).
public class DashboardNotifier
{
    public const string Contacts = "contacts";
    public const string Radios = "radios";
    public const string Scores = "scores";
    public const string Messages = "messages";

    private readonly IHubContext<DashboardHub> hub;

    public DashboardNotifier(IHubContext<DashboardHub> hub)
    {
        this.hub = hub;
    }

    public Task Changed(params string[] datasets)
        => hub.Clients.All.SendAsync("DataChanged", datasets);
}
