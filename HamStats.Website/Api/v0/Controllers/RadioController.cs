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
        return Ok(await HamStatsDbContext.Radios
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Operator,
                VFOs = r.VFOs.Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.RxFrequency,
                    v.TxFrequency
                })
            })
            .ToListAsync());
    }
}