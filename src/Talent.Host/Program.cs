using Talent.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddTalentModules(builder.Configuration);

var app = builder.Build();

app.UseStatusCodePages();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapTalentEndpoints();

app.Run();

// Lets WebApplicationFactory<Program> reach the entry point.
public partial class Program;
