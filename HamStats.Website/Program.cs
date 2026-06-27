
using System.Net.Mime;
using Hangfire;
using Hangfire.MemoryStorage;
using HamStats.Data;
using HamStats.Website.CallsignLookup;
using HamStats.Website.HostedServices;
using HamStats.Website.Hubs;
using HamStats.Website.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using RoushTech.Asio;
using RoushTech.Asio.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Last so it wins: lets the UI override log levels at runtime.
var runtimeLogLevels = new RuntimeLogLevelSource();
((IConfigurationBuilder)builder.Configuration).Add(runtimeLogLevels);

var configuration = builder.Configuration;

builder.Services
    .AddDbContext<HamStatsDbContext>(options => options
        .UseSqlite(configuration.GetConnectionString("Default"),
            o => o
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .EnableSensitiveDataLogging())
    .AddHostedService<N1MMWatcher>()
    .AddOpenApi()
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var result = new BadRequestObjectResult(context.ModelState);
            result.ContentTypes.Add(MediaTypeNames.Application.Json);
            return result;
        };
    });

// Asio job-session logging: per-job log streams (the import job activates one) replayed + live over
// SignalR. The shipped store is Redis-backed; we use an in-process store to stay Redis-free.
builder.Services.AddAsio();
builder.Services.AddSingleton<IJobSessionStore, InMemoryJobSessionStore>();

// Streams ILogger output (incl. N1MM packets) to the dashboard log console; the AsioAppLog section controls what it captures.
builder.Services
    .AddSignalR()
    // No app auth (LAN tool): drop the default owner check so any client can Watch a session id.
    .AddAsioSignalR(options => options.UseDefaultOwnerAuthorization = false)
    .AddAsioAppLogSignalR();

builder.Services.AddSingleton(runtimeLogLevels.Provider);

// Pushes live dashboard updates so the SPA can drop its polling loop.
builder.Services.AddSingleton<DashboardNotifier>();

// Offline callsign → grid lookup: downloads (and disk-caches) license + postal data via a Hangfire
// job, then backfills contact grids when N1MM doesn't supply one. See HamStats.Website/CallsignLookup.
var callsignOptions = configuration.GetSection(CallsignLookupOptions.SectionName).Get<CallsignLookupOptions>()
    ?? new CallsignLookupOptions();
builder.Services.AddSingleton(callsignOptions);

// Dumps are large; give the download clients a generous timeout.
builder.Services.AddHttpClient<FccAmateurSource>(c => c.Timeout = TimeSpan.FromMinutes(20));
builder.Services.AddHttpClient<IsedAmateurSource>(c => c.Timeout = TimeSpan.FromMinutes(20));
builder.Services.AddHttpClient<GeoNamesGeocoder>(c => c.Timeout = TimeSpan.FromMinutes(20));
builder.Services.AddHttpClient<CtyDataSource>(c => c.Timeout = TimeSpan.FromMinutes(20));
builder.Services.AddTransient<ICallsignSource>(sp => sp.GetRequiredService<FccAmateurSource>());
builder.Services.AddTransient<ICallsignSource>(sp => sp.GetRequiredService<IsedAmateurSource>());
builder.Services.AddTransient<CallsignImportJob>();

builder.Services.AddHangfire(config => config.UseMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

var routes = string.Join('|', [
    "dashboard",
    "settings"
]);
var options = new RewriteOptions()
    .AddRewrite($"^({routes}).*", "index.html", skipRemainingRules: true);

app
    .UseDeveloperExceptionPage()
    .UseForwardedHeaders()
    .UseDefaultFiles()
    .UseRewriter(options)
    .UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = (context) =>
        {
            if (context.File.Name.EndsWith(".json") ||
                context.File.Name.EndsWith(".html"))
            {
                context.Context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(0)
                };
            }
        }
    })
    .UseRouting()
    .UseAuthorization()
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapOpenApi();
        // LAN tool, no auth; hubs default to RequireAuthorization.
        endpoints.MapAsioAppLogHub(requireAuthorization: false);
        // Per-job log sessions (e.g. the callsign import); clients Watch(sessionId).
        endpoints.MapAsioHub("/hubs/job-session", requireAuthorization: false);
        // Live dashboard "DataChanged" nudges.
        endpoints.MapHub<DashboardHub>("/hubs/dashboard");
    });

// LAN tool, no auth (mirrors the log hub); lets you watch/trigger the callsign import job.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new AllowAllDashboardAuthorization()],
    // Hangfire's "Back to site" link returns to the Settings page (which links here).
    AppPath = "/settings"
});

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<HamStatsDbContext>();
    context.Database.SetCommandTimeout((int)TimeSpan.FromMinutes(120).TotalSeconds);
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}

if (callsignOptions.Enabled)
{
    // Weekly refresh while online; the table is rebuilt from freshly downloaded dumps.
    // Guid.Empty = unattended run with no log session (no one is watching a scheduled/startup run).
    RecurringJob.AddOrUpdate<CallsignImportJob>(
        "callsign-refresh",
        job => job.RunAsync(Guid.Empty, false, CancellationToken.None),
        callsignOptions.RefreshCron);

    // Repopulate from cached dumps on startup (offline-safe) so a DB wipe doesn't lose the table.
    if (callsignOptions.BuildOnStartup)
    {
        BackgroundJob.Enqueue<CallsignImportJob>(job => job.RunAsync(Guid.Empty, true, CancellationToken.None));
    }
}

await app.RunAsync();
