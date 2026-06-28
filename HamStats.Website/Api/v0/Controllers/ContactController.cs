using HamStats.Data;
using HamStats.Website.CallsignLookup;
using HamStats.Website.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/contacts")]
public class ContactController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }

    protected GridResolver GridResolver { get; }

    protected DashboardNotifier Notifier { get; }

    public ContactController(HamStatsDbContext hamStatsDbContext, GridResolver gridResolver, DashboardNotifier notifier)
    {
        HamStatsDbContext = hamStatsDbContext;
        GridResolver = gridResolver;
        Notifier = notifier;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var hideManual = await ManualRadioFilter.IsEnabled(HamStatsDbContext);

        return Ok(await HamStatsDbContext.Contacts
            .Where(c => !hideManual || c.Radio.Name != ManualRadioFilter.Name)
            .Select(c => new
            {
                c.Id,
                c.Band,
                c.Class,
                c.Date,
                c.FromCall,
                c.Gridsquare,
                c.Mode,
                c.RxFrequency,
                c.Section,
                c.ToCall,
                c.TxFrequency,
                Radio = c.Radio.Name,
                Operator = c.Operator
            })
            .ToListAsync());
    }

    /// <summary>
    /// Recomputes every stored contact's grid square through the current resolver (so changes to the
    /// section-vs-home-state logic or a freshly rebuilt callsign database apply to history). Uses the
    /// raw N1MM grid kept on the audit copy when present, so an operator-supplied grid isn't lost.
    /// </summary>
    [HttpPost("reresolve-grids")]
    public async Task<IActionResult> ReresolveGrids()
    {
        var contacts = await HamStatsDbContext.Contacts
            .Include(c => c.N1MMContact)
            .ToListAsync();

        var changed = 0;
        foreach (var contact in contacts)
        {
            var grid = await GridResolver.ResolveGrid(
                HamStatsDbContext, contact.N1MMContact?.Gridsquare, contact.ToCall, contact.Section);
            if (grid != contact.Gridsquare)
            {
                contact.Gridsquare = grid;
                changed++;
            }
        }

        await HamStatsDbContext.SaveChangesAsync();
        await Notifier.Changed(DashboardNotifier.Contacts);
        return Ok(new { total = contacts.Count, changed });
    }
}