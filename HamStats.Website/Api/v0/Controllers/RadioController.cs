using HamStats.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/radios")]
public class RadioController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }

    public RadioController(HamStatsDbContext hamStatsDbContext)
    {
        HamStatsDbContext = hamStatsDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        // Run rates like N1MM: Last-10 / Last-100 QSO rates (QSOs/hour) plus rolling 15 / 60 min counts.
        var now = DateTime.UtcNow;
        var since15 = now.AddMinutes(-15);
        var since60 = now.AddMinutes(-60);

        var radios = await HamStatsDbContext.Radios
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