using HamStats.Website.Logging;
using Microsoft.AspNetCore.Mvc;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/loglevel")]
public class LogLevelController : Controller
{
    // Watcher logs raw packets at Trace, message summaries at Debug.
    private const string WatcherCategory = "HamStats.Website.HostedServices";

    protected RuntimeLogLevelProvider Provider { get; }

    protected IConfiguration Configuration { get; }

    public LogLevelController(RuntimeLogLevelProvider provider, IConfiguration configuration)
    {
        Provider = provider;
        Configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        // Level currently captured for the watcher category.
        var effective = Configuration[$"Logging:AsioAppLog:LogLevel:{WatcherCategory}"]
            ?? Configuration["Logging:AsioAppLog:LogLevel:Default"]
            ?? "Information";
        return Ok(new { category = WatcherCategory, level = effective });
    }

    [HttpPut]
    public IActionResult Set([FromBody] LogLevelRequest request)
    {
        if (!Enum.TryParse<LogLevel>(request.Level, ignoreCase: true, out _))
        {
            return BadRequest($"'{request.Level}' is not a valid log level.");
        }

        Provider.Set(WatcherCategory, request.Level);
        return NoContent();
    }
}

public class LogLevelRequest
{
    public required string Level { get; set; }
}
