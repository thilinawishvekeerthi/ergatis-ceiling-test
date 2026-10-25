using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Talent.Config.Contracts;

namespace Talent.Host.Tests;

public sealed class HealthEndpointTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_WhenCalled_ReturnsOk(string path)
    {
        using var client = host.Factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownRoute_WhenCalled_ReturnsProblemDetailsBody()
    {
        using var client = host.Factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/nope", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Startup_WhenMigrateOnStartup_SeedsDefaultNineBox()
    {
        _ = host.Factory.Server;
        using var scope = host.Factory.Services.CreateScope();

        var nineBox = await scope.ServiceProvider.GetRequiredService<IConfigVersionReader>()
            .GetLatestPublishedAsync(ConfigType.Grid, "NINE_BOX", Ct);

        nineBox.ShouldNotBeNull();
    }
}
