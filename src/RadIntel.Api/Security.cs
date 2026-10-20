using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RadIntel.Application;

namespace RadIntel.Api;

public sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public Guid OrganizationId => Guid.TryParse(User.FindFirstValue("tenant_id"), out var id) ? id : Guid.Empty;
    public string UserId => User.FindFirstValue("sub") ?? "";
    public bool CanProvision => User.HasClaim("radintel_provision", "true");
    public bool HasRecentMfa
    {
        get
        {
            if (!User.HasClaim("amr", "mfa") || !long.TryParse(User.FindFirstValue("auth_time"), out var timestamp)) return false;
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp;
            return age is >= 0 and <= 600;
        }
    }
}

// Explicit local-only development transport. No role or clinical permission is
// accepted from headers: authoritative roles always come from Membership rows.
public sealed class DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ip = Context.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip)) return Task.FromResult(AuthenticateResult.Fail("Local development authentication requires loopback."));
        var secret = config["Authentication:DevelopmentToken"] ?? "";
        var provided = Request.Headers.Authorization.ToString();
        if (secret.Length < 32 || !provided.StartsWith("Bearer ", StringComparison.Ordinal) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)),
                SHA256.HashData(Encoding.UTF8.GetBytes(provided[7..]))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid development credentials."));
        var userId = Request.Headers["X-RadIntel-User"].ToString();
        if (!Guid.TryParse(Request.Headers["X-RadIntel-Tenant"], out var tenant) || tenant == Guid.Empty || string.IsNullOrWhiteSpace(userId) || userId.Length > 200)
            return Task.FromResult(AuthenticateResult.Fail("Development identity is incomplete."));
        var claims = new List<Claim> { new("sub", userId), new("tenant_id", tenant.ToString()) };
        if (config.GetValue<bool>("Authentication:AllowDevelopmentProvisioning")) claims.Add(new("radintel_provision", "true"));
        if (config.GetValue<bool>("Authentication:AllowSimulatedMfa") && Request.Headers["X-RadIntel-Mfa"] == "true")
        {
            claims.Add(new("amr", "mfa")); claims.Add(new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        }
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
