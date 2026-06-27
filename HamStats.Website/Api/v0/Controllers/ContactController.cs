using HamStats.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/contacts")]
public class ContactController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }

    public ContactController(HamStatsDbContext hamStatsDbContext)
    {
        HamStatsDbContext = hamStatsDbContext;
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
}