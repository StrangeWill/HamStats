using HamStats.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/scores")]
public class ScoreController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }

    public ScoreController(HamStatsDbContext hamStatsDbContext)
    {
        HamStatsDbContext = hamStatsDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await HamStatsDbContext.Scores
            .Include(s => s.Breakdown)
            .Select(s => new
            {
                s.ArrlSection,
                s.Assisted,
                s.Bands,
                s.Call,
                s.Club,
                s.Contest,
                s.CqZone,
                s.Grid6,
                s.Mode,
                s.Overlay,
                s.Ops,
                s.Power,
                s.Id,
                s.Value,
                Breakdown = s.Breakdown.Select(b => new
                {
                    b.Band,
                    b.Id,
                    b.Mode,
                    b.Points,
                    b.QSOs
                })
            })
            .FirstOrDefaultAsync());
    }
}