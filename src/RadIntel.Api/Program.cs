using System.Text.Json.Serialization;
using AIFramework.Application;
using AIFramework.Core.Abstractions;
using AIFramework.Local;
using AIFramework.Azure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RadIntel.AI;
using RadIntel.Api;
using RadIntel.Application;
using RadIntel.Domain;
using RadIntel.Infrastructure;
using RadIntel.Integrations.Dicom;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics", LogLevel.None);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<ImagingService>();
var archives = builder.Configuration.GetSection("Pacs:Archives").GetChildren().Select(section => new ArchiveConfiguration(
    section["Key"] ?? section.Key, section["Name"] ?? section.Key,
    Guid.Parse(section["OrganizationId"] ?? throw new InvalidOperationException("PACS organization scope required.")),
    Guid.Parse(section["FacilityId"] ?? throw new InvalidOperationException("PACS facility scope required.")),
    new Uri(section["Endpoint"] ?? throw new InvalidOperationException("PACS endpoint required.")),
    section["BearerToken"], section.GetValue<bool>("AllowAnonymousDevelopment"))).ToArray();
foreach (var archive in archives) DicomWebArchive.ValidateConfiguration(archive, builder.Environment.IsDevelopment());
if (archives.GroupBy(x => new { x.OrganizationId, x.FacilityId, x.Key }).Any(x => x.Count() > 1))
    throw new InvalidOperationException("PACS archive keys must be unique within their organization and facility.");
builder.Services.AddSingleton<IReadOnlyList<ArchiveConfiguration>>(archives);
builder.Services.AddHttpClient<IImagingArchive, DicomWebArchive>(client => client.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None); // Upstream URLs may contain DICOM identifiers.
builder.Services.AddDbContext<RadIntelDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("A PostgreSQL connection string is required."),
    pg => pg.MigrationsAssembly(typeof(RadIntelDbContext).Assembly.FullName)));
builder.Services.AddScoped<IDocumentProfileCatalog, RadiologyProfileCatalog>();
builder.Services.AddAiApplication();
var knowledgeProvider = builder.Configuration["AI:KnowledgeProvider"] ?? "Local";
if (knowledgeProvider == "Azure")
{
    builder.Services.AddAzureModels(new AzureModelOptions(
        new Uri(builder.Configuration["AI:Azure:OpenAiEndpoint"] ?? throw new InvalidOperationException("Azure endpoint required.")),
        builder.Configuration["AI:Azure:ChatDeployment"] ?? throw new InvalidOperationException("Chat deployment required."),
        builder.Configuration["AI:Azure:EmbeddingDeployment"] ?? throw new InvalidOperationException("Embedding deployment required.")));
}
else if (knowledgeProvider == "Local") builder.Services.AddLocalAi();
else throw new InvalidOperationException("AI:KnowledgeProvider must be Local or Azure.");
// Replace Local's ephemeral retriever with published product data and explicit ACLs.
builder.Services.AddScoped<IKnowledgeRetriever, PublishedTemplateRetriever>();
switch (builder.Configuration["AI:DraftProvider"] ?? "Disabled")
{
    case "Disabled": builder.Services.AddScoped<IReportDraftGenerator, DisabledDraftGenerator>(); break;
    case "Verbatim": builder.Services.AddScoped<IReportDraftGenerator, VerbatimDraftGenerator>(); break;
    default: throw new InvalidOperationException("AI:DraftProvider must be Disabled or Verbatim. No clinical LLM drafting adapter is approved.");
}
builder.Services.AddScoped<ISpeechTranscriber, UnconfiguredSpeechTranscriber>();

var authMode = builder.Configuration["Authentication:Mode"] ?? "Jwt";
if (authMode == "Development")
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Development authentication is permitted only in Development.");
    if ((builder.Configuration["Authentication:DevelopmentToken"]?.Length ?? 0) < 32)
        throw new InvalidOperationException("Set a development token of at least 32 characters through user secrets or the environment.");
    builder.Services.AddAuthentication("Development").AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Development", _ => { });
}
else if (authMode == "Jwt")
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];
    if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("JWT authentication requires an HTTPS authority and audience.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    {
        o.Authority = authority; o.Audience = audience; o.MapInboundClaims = false; o.RequireHttpsMetadata = true;
    });
}
else throw new InvalidOperationException("Unknown authentication mode.");
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
    .RequireAssertion(c => Guid.TryParse(c.User.FindFirst("tenant_id")?.Value, out var id) && id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(c.User.FindFirst("sub")?.Value) && c.User.FindFirst("sub")!.Value.Length <= 200).Build());
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("imaging", context => RateLimitPartition.GetConcurrencyLimiter(
        (context.User.FindFirst("tenant_id")?.Value ?? "anonymous") + ":" + (context.User.FindFirst("sub")?.Value ?? "anonymous"),
        _ => new ConcurrencyLimiterOptions { PermitLimit = 4, QueueLimit = 0 }));
    o.AddPolicy("clinical", context => RateLimitPartition.GetFixedWindowLimiter(
        (context.User.FindFirst("tenant_id")?.Value ?? "anonymous") + ":" + (context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString()),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title, detail) = error switch
    {
        AccessDeniedException => (403, "Access denied", "The requested action is not permitted."),
        MissingResourceException => (404, "Resource not found", "The resource is unavailable in the authorized scope."),
        RuleViolation r => (r.Code == "conflict" ? 409 : 400, "Request rejected", r.Message),
        DbUpdateConcurrencyException => (409, "Record changed", "Reload before retrying."),
        DbUpdateException => (409, "Write rejected", "A uniqueness or relationship constraint prevented the write."),
        FeatureUnavailableException e => (503, "Feature unavailable", e.Message),
        OperationCanceledException => (503, "Operation cancelled", "The operation did not complete."),
        _ => (500, "Request failed", "An unexpected failure occurred. Use the trace ID when contacting support.")
    };
    // No exception text, path, patient identifiers, SQL or clinical payload in routine logs.
    app.Logger.LogWarning("Request rejected with status {Status}; trace {TraceId}; type {ErrorType}", status, context.TraceIdentifier, error?.GetType().Name);
    await Results.Problem(statusCode: status, title: title, detail: detail, extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger"))
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'";
    await next();
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapGet("/health/live", () => Results.Ok(new { Status = "live" })).AllowAnonymous();
app.MapGet("/client-config", () => Results.Ok(new { DevelopmentAuthentication = app.Environment.IsDevelopment() && authMode == "Development" })).AllowAnonymous();
app.MapGet("/health/ready", async (RadIntelDbContext db, CancellationToken ct) =>
{
    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync(ct);
        return !pending.Any() && await db.Database.CanConnectAsync(ct) ? Results.Ok(new { Status = "ready" }) : Results.StatusCode(503);
    }
    catch { return Results.StatusCode(503); }
}).AllowAnonymous();
app.MapControllers().RequireRateLimiting("clinical");
// Deliberately no Migrate, EnsureCreated, HasData or startup seed execution.
app.Run();
public partial class Program;
