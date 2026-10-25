using Talent.Host;
using Talent.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TalentSharedDbContext>("database", tags: ["ready"]);
builder.Services.AddTalentModules(builder.Configuration);

var app = builder.Build();
await app.InitializeAsync();

app.UseStatusCodePages();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });
app.MapTalentEndpoints();

app.Run();

// Lets WebApplicationFactory<Program> reach the entry point.
public partial class Program;
