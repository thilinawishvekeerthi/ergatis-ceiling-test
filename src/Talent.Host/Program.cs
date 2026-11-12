using Talent.Host;
using Talent.Security;
using Talent.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TalentSharedDbContext>("database", tags: ["ready"]);
builder.Services.AddTalentModules(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();
await app.InitializeAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseTalentSecurity();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapTalentEndpoints();
if (app.Environment.IsDevelopment())
{
    // The checked-in contract is docs/api/nine-box.openapi.yaml; this generated document lets developers compare.
    app.MapOpenApi().AllowAnonymous();
}

app.Run();

// Lets WebApplicationFactory<Program> reach the entry point.
public partial class Program;
