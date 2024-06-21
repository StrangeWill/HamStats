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
        return Ok(await HamStatsDbContext.Contacts
            .Select(c => new
            {
                c.Id,
                c.Band,
                c.Class,
                c.Date,
                c.FromCall,
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