using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using RadIntel.Contracts;
using RadIntel.Infrastructure;

namespace RadIntel.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public ApiFactory() { connection.Open(); }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "Development", ["Authentication:DevelopmentToken"] = new string('x', 40),
            ["AI:DraftProvider"] = "Verbatim"
        }));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Mode"] = "Development", ["Authentication:DevelopmentToken"] = new string('x', 40),
            ["AI:DraftProvider"] = "Verbatim"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RadIntelDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RadIntelDbContext>>();
            services.AddDbContext<RadIntelDbContext>(o => o.UseSqlite(connection));
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        });
    }
    public async Task Initialize()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RadIntelDbContext>().Database.EnsureCreatedAsync();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}
public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Test-Tenant", out var tenant)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new("tenant_id", tenant.ToString()), new("sub", Request.Headers["Test-User"].ToString()) };
        if (Request.Headers["Test-Provision"] == "true") claims.Add(new("radintel_provision", "true"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
public sealed class ApiTests
{
    [Fact]
    public async Task Clinical_routes_require_authentication_and_ignore_forged_role_headers()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-RadIntel-Role", "Administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var shell = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, shell.StatusCode);
        Assert.Contains("RadIntel", await shell.Content.ReadAsStringAsync());
        Assert.True(shell.Headers.Contains("Content-Security-Policy"));
    }
    [Fact]
    public async Task Api_onboarding_creates_no_seed_patients_and_enforces_resource_scope()
    {
        using var factory = new ApiFactory(); await factory.Initialize(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Tenant", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("Test-User", "owner"); client.DefaultRequestHeaders.Add("Test-Provision", "true");
        var response = await client.PostAsJsonAsync("/api/v1/organizations/onboard", new OnboardRequest("Organization", "Facility", "UTC"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OnboardResponse>();
        var patients = await client.GetFromJsonAsync<PageResult<RadIntel.Domain.Patient>>($"/api/v1/facilities/{body!.FacilityId}/patients");
        Assert.Empty(patients!.Items);
        var denied = await client.GetAsync($"/api/v1/facilities/{Guid.NewGuid()}/patients");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var problem = await denied.Content.ReadAsStringAsync();
        Assert.Contains("traceId", problem); Assert.DoesNotContain("StackTrace", problem);
        Assert.True(denied.Headers.CacheControl?.NoStore);
    }
    [Fact]
    public async Task Invalid_model_is_rejected_before_any_write()
    {
        using var factory = new ApiFactory(); await factory.Initialize(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Tenant", Guid.NewGuid().ToString()); client.DefaultRequestHeaders.Add("Test-User", "owner");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/facilities/{Guid.NewGuid()}/patients", new { identifier = "" })).StatusCode);
    }
    private sealed record OnboardResponse(Guid OrganizationId, Guid FacilityId);
}
