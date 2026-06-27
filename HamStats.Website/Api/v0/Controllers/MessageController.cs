using HamStats.Data;
using HamStats.Data.Models;
using HamStats.Website.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HamStats.Website.Api.v0.MapControllers;

[Route("/api/v0/messages")]
public class MessageController : Controller
{
    protected HamStatsDbContext HamStatsDbContext { get; }
    protected DashboardNotifier Notifier { get; }

    public MessageController(HamStatsDbContext hamStatsDbContext, DashboardNotifier notifier)
    {
        HamStatsDbContext = hamStatsDbContext;
        Notifier = notifier;
    }

    public class PostMessage
    {
        public string Radio { get; set; } = "";
        public string? Operator { get; set; }
        public string Text { get; set; } = "";
    }

    // Most recent messages, returned oldest-first so the client can append straight to the log.
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var messages = await HamStatsDbContext.Messages
            .OrderByDescending(m => m.Date)
            .Take(take)
            .Select(m => new { m.Id, m.Date, m.Radio, m.Operator, m.Text })
            .ToListAsync();
        messages.Reverse();
        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PostMessage input)
    {
        var text = input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Empty message.");
        if (string.IsNullOrWhiteSpace(input.Radio))
            return BadRequest("No radio selected.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            Radio = input.Radio.Trim(),
            Operator = string.IsNullOrWhiteSpace(input.Operator) ? null : input.Operator!.Trim(),
            Text = text.Length > 500 ? text[..500] : text,
        };
        HamStatsDbContext.Messages.Add(message);
        await HamStatsDbContext.SaveChangesAsync();
        await Notifier.Changed(DashboardNotifier.Messages);

        return Ok(new { message.Id, message.Date, message.Radio, message.Operator, message.Text });
    }
}
