
using System.Net.Mime;
using HamStats.Data;
using HamStats.Website.HostedServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var configuration = builder.Configuration;

builder.Services
    .AddDbContext<HamStatsDb>(options => options
        .UseNpgsql(configuration.GetConnectionString("Default"),
            o => o
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                .EnableRetryOnFailure(5))
        .EnableSensitiveDataLogging())
    .AddHostedService<N1MMWatcher>()
    .AddMvc()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var result = new BadRequestObjectResult(context.ModelState);
            result.ContentTypes.Add(MediaTypeNames.Application.Json);
            return result;
        };
    });

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

app
    .UseDeveloperExceptionPage()
    .UseForwardedHeaders()
    .UseDefaultFiles()
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
    });

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<HamStatsDb>();
    context.Database.SetCommandTimeout((int)TimeSpan.FromMinutes(120).TotalSeconds);
    await context.Database.EnsureCreatedAsync();
}

await app.RunAsync();
