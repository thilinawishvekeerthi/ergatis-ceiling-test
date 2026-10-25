using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Security.IntegrationTests.SecurityHostFixture;

namespace Talent.Security.IntegrationTests;

/// <summary>The BFF session: cookie only, no tokens in the browser, 401 not redirect, fake login only when switched on.</summary>
public sealed class SessionTests(SecurityHostFixture host) : IClassFixture<SecurityHostFixture>
{
    [Fact]
    public async Task User_WithoutSession_Returns401()
    {
        using var client = host.Anonymous();

        (await client.GetAsync("/bff/user", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task User_AfterFakeLogin_ReturnsRolesAndEmployeeButNoTokens()
    {
        using var client = await host.LoginAsync(Manager);

        var response = await client.GetAsync("/bff/user", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.GetProperty("userId").GetString().ShouldBe(Manager);
        json.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["MANAGER"]);
        json.GetProperty("hasEmployee").GetBoolean().ShouldBeTrue();
        body.ShouldNotContain("token", Case.Insensitive);
    }

    [Fact]
    public async Task FakeLogin_SetsHttpOnlySameSiteCookie()
    {
        using var client = host.Anonymous();

        var response = await client.PostAsJsonAsync("/bff/fake-login", new { subject = Employee }, Ct);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("Talent.Session", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=", Case.Insensitive);
        cookie.ShouldNotContain("samesite=none", Case.Insensitive);
    }

    [Fact]
    public async Task FakeLogin_ForUnknownSubject_StillSignsInButHasNoRoles()
    {
        using var client = await host.LoginAsync("someone.new");

        var json = await ReadAsync(await client.GetAsync("/bff/user", Ct));

        json.GetProperty("roles").GetArrayLength().ShouldBe(0);
        json.GetProperty("hasEmployee").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FakeLogin_WithEmptySubject_Returns400(string subject)
    {
        using var client = host.Anonymous();

        (await client.PostAsJsonAsync("/bff/fake-login", new { subject }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        using var client = await host.LoginAsync(Employee);

        (await client.PostAsync("/bff/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/bff/user", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryToken_Returns400()
    {
        using var client = await host.LoginAsync(Employee, withAntiforgery: false);

        (await client.PostAsync("/bff/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Antiforgery_WithoutSession_Returns401()
    {
        using var client = host.Anonymous();

        (await client.GetAsync("/bff/antiforgery", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Antiforgery_WithTokenFromAnotherSession_Returns400()
    {
        using var first = await host.LoginAsync(Admin);
        using var second = await host.LoginAsync(Admin, withAntiforgery: false);
        var token = await first.GetFromJsonAsync<JsonElement>("/bff/antiforgery", Ct);
        second.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token.GetProperty("requestToken").GetString());

        var response = await second.PostAsync($"/api/v1/config/Scale/{NewCode()}/versions", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_StaysOpenWithoutSession()
    {
        using var client = host.Anonymous();

        (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownApiRoute_WithoutSession_Returns404()
    {
        using var client = host.Anonymous();

        (await client.GetAsync("/api/v1/nope", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MalformedJson_Returns400WithoutStackTrace()
    {
        using var client = await host.LoginAsync(Admin);

        // A malformed JSON body is a client error and must not leak parser internals.
        var response = await client.PostAsync($"/api/v1/config/Scale/{NewCode()}/drafts",
            new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("   at ");
    }
}
