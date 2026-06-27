using Hangfire;
using HamStats.Data;
using HamStats.Website.CallsignLookup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoushTech.Asio;

namespace HamStats.Website.Api.v0.MapControllers;

/// <summary>Trigger and inspect the offline callsign → grid lookup table.</summary>
[Route("/api/v0/callsigns")]
public class CallsignController : Controller
{
    protected HamStatsDbContext DbContext { get; }

    protected JobSessionService JobSessions { get; }

    public CallsignController(HamStatsDbContext dbContext, JobSessionService jobSessions)
    {
        DbContext = dbContext;
        JobSessions = jobSessions;
    }

    /// <summary>
    /// Queues an online refresh (download fresh dumps and rebuild the table). Returns a job-session id
    /// the client can <c>Watch</c> on <c>/hubs/job-session</c> for live log output.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import()
    {
        var sessionId = Guid.NewGuid();
        // No app auth, so no owner (the hub's owner check is disabled).
        await JobSessions.CreateSession(sessionId, "Callsign import", ownerName: null);
        var jobId = BackgroundJob.Enqueue<CallsignImportJob>(job => job.RunAsync(sessionId, false, CancellationToken.None));
        return Accepted(new { jobId, sessionId });
    }

    /// <summary>Row count and last-built time of the lookup table.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var count = await DbContext.Callsigns.CountAsync();
        var prefixCount = await DbContext.CallsignPrefixes.CountAsync();
        var updatedAt = count > 0
            ? await DbContext.Callsigns.MaxAsync(c => (DateTime?)c.UpdatedAt)
            : await DbContext.CallsignPrefixes.MaxAsync(c => (DateTime?)c.UpdatedAt);
        return Ok(new { count, prefixCount, updatedAt });
    }
}
