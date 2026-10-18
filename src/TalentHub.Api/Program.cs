using Microsoft.EntityFrameworkCore;
using TalentHub.Api.Auth;
using TalentHub.Api.Data;
using TalentHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TalentHubDbContext>(o =>
{
    var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
    if (provider == "SqlServer") o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
    else o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
});

builder.Services.AddAuth(builder.Configuration);
builder.Services.AddScoped<ILmsClient, MockLmsClient>();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins("http://localhost:4200", "http://localhost:4173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TalentHubDbContext>();
    Db.Migrate(db);
    await AuthSetup.SeedRolesAsync(scope.ServiceProvider);
    await Db.SeedAsync(db, app.Configuration);
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapAuthEndpoints();

app.Run();

public partial class Program;
