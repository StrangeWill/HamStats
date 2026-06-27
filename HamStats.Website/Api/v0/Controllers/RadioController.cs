using HamStats.Data;
using HamStats.Data.Models;
using HamStats.Website.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/radios")]
public class RadioController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }

    protected DashboardNotifier Notifier { get; }

    public RadioController(HamStatsDbContext hamStatsDbContext, DashboardNotifier notifier)
    {
        HamStatsDbContext = hamStatsDbContext;
        Notifier = notifier;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Run rates like N1MM: Last-10 / Last-100 QSO rates (QSOs/hour) plus rolling 15 / 60 min counts.
        var now = DateTime.UtcNow;
        var since15 = now.AddMinutes(-15);
        var since60 = now.AddMinutes(-60);

        var hideManual = await ManualRadioFilter.IsEnabled(HamStatsDbContext);

        var radios = await HamStatsDbContext.Radios
            .Where(r => !hideManual || r.Name != ManualRadioFilter.Name)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Operator,
                VFOs = r.VFOs
                    .Select(v => new { v.Id, v.Name, v.RxFrequency, v.TxFrequency })
                    .OrderBy(v => v.Name)
                    .ToList(),
                Contacts = r.Contacts.Count()
            })
            .ToListAsync();

        // QSO timestamps per radio drive the rates/counts in memory (keeps the SQL trivial).
        var stamps = (await HamStatsDbContext.Contacts
                .Select(c => new { c.RadioId, c.Date })
                .ToListAsync())
            .GroupBy(s => s.RadioId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Date).OrderByDescending(d => d).ToList());

        var result = radios
            .Select(r =>
            {
                var dates = stamps.TryGetValue(r.Id, out var d) ? d : new List<DateTime>();
                return new
                {
                    r.Id,
                    r.Name,
                    r.Operator,
                    r.VFOs,
                    r.Contacts,
                    Rate10 = RateOf(dates, 10, now),
                    Rate100 = RateOf(dates, 100, now),
                    Last15m = dates.Count(x => x >= since15),
                    Last60m = dates.Count(x => x >= since60)
                };
            })
            .OrderByDescending(s => s.Contacts)
            .ToList();

        return Ok(result);
    }

    // Remove a station and everything that belongs only to it: its QSOs (both the normalized Contact
    // and the raw N1MMContact mirror) and its VFOs (plus their N1MMRadio mirrors). Clearing the VFO/
    // N1MMRadio rows lets the station re-register cleanly if N1MM broadcasts radioinfo for it again.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await HamStatsDbContext.Radios.AnyAsync(r => r.Id == id))
        {
            return NotFound();
        }

        await using var transaction = await HamStatsDbContext.Database.BeginTransactionAsync();

        // Contact carries the FK to N1MMContact, so delete it before its mirror.
        var contacts = HamStatsDbContext.Contacts.Where(c => c.RadioId == id);
        var n1mmContactIds = await contacts.Select(c => c.N1MMContactId).ToListAsync();
        await contacts.ExecuteDeleteAsync();
        await HamStatsDbContext.N1MMContacts.Where(n => n1mmContactIds.Contains(n.Id)).ExecuteDeleteAsync();

        // N1MMRadio carries the FK to VFO, so delete it before the VFO. (VFO has no DbSet — reach it via Set<>.)
        var vfos = HamStatsDbContext.Set<VFO>().Where(v => v.RadioId == id);
        var vfoIds = await vfos.Select(v => v.Id).ToListAsync();
        await HamStatsDbContext.N1MMRadios.Where(n => n.VFOId != null && vfoIds.Contains(n.VFOId.Value)).ExecuteDeleteAsync();
        await vfos.ExecuteDeleteAsync();

        await HamStatsDbContext.Radios.Where(r => r.Id == id).ExecuteDeleteAsync();

        await transaction.CommitAsync();
        await Notifier.Changed(DashboardNotifier.Radios, DashboardNotifier.Contacts);
        return NoContent();
    }

    // QSOs/hour over the most recent n QSOs (or all, if fewer). Uses time-to-now, so it decays
    // during a lull just like N1MM's rate meter.
    private static int? RateOf(List<DateTime> datesDesc, int n, DateTime now)
    {
        var count = Math.Min(n, datesDesc.Count);
        if (count < 2) return null;
        var hours = (now - datesDesc[count - 1]).TotalHours;
        return hours > 0 ? (int)Math.Round(count / hours) : null;
    }
}