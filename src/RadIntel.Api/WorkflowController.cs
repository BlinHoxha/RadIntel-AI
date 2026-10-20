using AIFramework.Application.Services;
using AIFramework.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;
using RadIntel.Infrastructure;

namespace RadIntel.Api;

[ApiController]
[Route("api/v1")]
public sealed class WorkflowController(WorkflowService workflow, RadIntelDbContext db,
    ICurrentActor actor, KnowledgeAnswerService knowledge) : ControllerBase
{
    [HttpGet("session")]
    public Task<object> Session(CancellationToken ct) => workflow.Session(ct);
    [HttpPost("organizations/onboard")]
    public async Task<IActionResult> Onboard(OnboardRequest request, CancellationToken ct) =>
        StatusCode(201, await workflow.Onboard(request, ct));
    [HttpPut("facilities/{facilityId:guid}/memberships")]
    public Task<Membership> Membership(Guid facilityId, MembershipRequest request, CancellationToken ct) => workflow.SetMembership(facilityId, request, ct);
    [HttpGet("facilities/{facilityId:guid}/patients")]
    public Task<PageResult<Patient>> Patients(Guid facilityId, CancellationToken ct, int page = 1, int pageSize = 30) => workflow.Patients(facilityId, page, pageSize, ct);
    [HttpPost("facilities/{facilityId:guid}/patients")]
    public async Task<IActionResult> Patient(Guid facilityId, PatientRequest request, CancellationToken ct) => StatusCode(201, await workflow.CreatePatient(facilityId, request, ct));
    [HttpPost("facilities/{facilityId:guid}/imaging-orders")]
    public async Task<IActionResult> Order(Guid facilityId, OrderRequest request, CancellationToken ct) => StatusCode(201, await workflow.CreateOrder(facilityId, request, ct));
    [HttpGet("facilities/{facilityId:guid}/worklist")]
    public Task<PageResult<ImagingOrder>> Worklist(Guid facilityId, CancellationToken ct, string? status = null, int page = 1, int pageSize = 30) => workflow.Worklist(facilityId, status, page, pageSize, ct);
    [HttpPost("facilities/{facilityId:guid}/imaging-orders/{id:guid}/transition")]
    public Task<ImagingOrder> Transition(Guid facilityId, Guid id, TransitionRequest request, CancellationToken ct) => workflow.Transition(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/imaging-orders/{id:guid}/claim")]
    public Task<ImagingOrder> Claim(Guid facilityId, Guid id, ConcurrencyRequest request, CancellationToken ct) => workflow.Claim(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/imaging-orders/{id:guid}/schedule")]
    public Task<Appointment> Schedule(Guid facilityId, Guid id, ScheduleRequest request, CancellationToken ct) => workflow.Schedule(facilityId, id, request, ct);
    [HttpGet("facilities/{facilityId:guid}/equipment")]
    public Task<IReadOnlyList<Equipment>> Equipment(Guid facilityId, CancellationToken ct) => workflow.GetEquipment(facilityId, ct);
    [HttpPost("facilities/{facilityId:guid}/equipment")]
    public async Task<IActionResult> Equipment(Guid facilityId, EquipmentRequest request, CancellationToken ct) => StatusCode(201, await workflow.CreateEquipment(facilityId, request, ct));
    [HttpPost("facilities/{facilityId:guid}/imaging-orders/{id:guid}/reports")]
    public async Task<IActionResult> CreateReport(Guid facilityId, Guid id, ConcurrencyRequest request, CancellationToken ct) => StatusCode(201, await workflow.CreateReport(facilityId, id, request, ct));
    [HttpGet("facilities/{facilityId:guid}/imaging-orders/{id:guid}/reports")]
    public async Task<IReadOnlyList<Report>> Reports(Guid facilityId, Guid id, CancellationToken ct)
    {
        await workflow.Require(facilityId, ClinicRole.Radiologist, ct);
        if (!await db.Orders.AnyAsync(x => x.Id == id && x.FacilityId == facilityId && x.AssignedTo == actor.UserId, ct)) throw new MissingResourceException();
        return await db.Reports.AsNoTracking().Where(x => x.FacilityId == facilityId && x.OrderId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    }
    [HttpGet("facilities/{facilityId:guid}/reports/{id:guid}")]
    public Task<object> Report(Guid facilityId, Guid id, CancellationToken ct) => workflow.GetReport(facilityId, id, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/drafts")]
    public Task<Report> Draft(Guid facilityId, Guid id, VersionRequest request, CancellationToken ct) => workflow.SaveDraft(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/submit")]
    public Task<Report> Submit(Guid facilityId, Guid id, ConcurrencyRequest request, CancellationToken ct) => workflow.Submit(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/sign")]
    public Task<Signature> Sign(Guid facilityId, Guid id, SignRequest request, CancellationToken ct) => workflow.Sign(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/addenda")]
    public async Task<IActionResult> Addendum(Guid facilityId, Guid id, AddendumRequest request, CancellationToken ct) => StatusCode(201, await workflow.Addendum(facilityId, id, request, ct));
    [HttpGet("facilities/{facilityId:guid}/templates")]
    public Task<IReadOnlyList<ReportTemplate>> Templates(Guid facilityId, CancellationToken ct) => workflow.Templates(facilityId, ct);
    [HttpPost("facilities/{facilityId:guid}/templates")]
    public async Task<IActionResult> Template(Guid facilityId, TemplateRequest request, CancellationToken ct) => StatusCode(201, await workflow.CreateTemplate(facilityId, request, ct));
    [HttpPost("facilities/{facilityId:guid}/templates/{id:guid}/publish")]
    public Task<ReportTemplate> Publish(Guid facilityId, Guid id, ConcurrencyRequest request, CancellationToken ct) => workflow.PublishTemplate(facilityId, id, request, true, ct);
    [HttpPost("facilities/{facilityId:guid}/templates/{id:guid}/revoke")]
    public Task<ReportTemplate> Revoke(Guid facilityId, Guid id, ConcurrencyRequest request, CancellationToken ct) => workflow.PublishTemplate(facilityId, id, request, false, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/transcripts")]
    public async Task<IActionResult> Transcript(Guid facilityId, Guid id, TranscriptRequest request, CancellationToken ct) => StatusCode(201, await workflow.Transcript(facilityId, id, request, ct));
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/ai-draft")]
    public Task<AIProposal> Generate(Guid facilityId, Guid id, ProposalRequest request, CancellationToken ct) => workflow.Generate(facilityId, id, request, ct);
    [HttpPost("facilities/{facilityId:guid}/reports/{id:guid}/proposals/{proposalId:guid}/review")]
    public Task<object> Review(Guid facilityId, Guid id, Guid proposalId, ReviewProposalRequest request, CancellationToken ct) => workflow.ReviewProposal(facilityId, id, proposalId, request, ct);
    [HttpGet("facilities/{facilityId:guid}/audit")]
    public Task<PageResult<AuditEvent>> Audit(Guid facilityId, CancellationToken ct, int page = 1, int pageSize = 30) => workflow.AuditTrail(facilityId, page, pageSize, ct);
    [HttpGet("facilities/{facilityId:guid}/metrics")]
    public Task<object> Metrics(Guid facilityId, CancellationToken ct) => workflow.Metrics(facilityId, ct);
    [HttpPost("facilities/{facilityId:guid}/knowledge/answers")]
    public async Task<GroundedAnswerResponse> Answer(Guid facilityId, KnowledgeRequest request, CancellationToken ct)
    {
        await workflow.Require(facilityId, ClinicRole.Administrator | ClinicRole.Radiologist, ct);
        if (request.DocumentIds is not { Length: > 0 and <= 20 } || string.IsNullOrWhiteSpace(request.Question) || request.Question.Trim().Length is < 2 or > 2000)
            throw new RuleViolation("validation", "A question up to 2000 characters and 1-20 approved document IDs are required.");
        var ids = request.DocumentIds.Distinct().ToArray();
        var allowed = await db.Templates.Where(x => x.FacilityId == facilityId && x.Published && ids.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        if (allowed.Length != ids.Length) throw new MissingResourceException();
        var response = await knowledge.AnswerAsync(new AskKnowledgeRequest { Question = request.Question,
            Profile = "radiology-template", TenantId = actor.OrganizationId, ScopeId = facilityId, DocumentIds = allowed, MaxResults = 5 }, ct);
        db.AuditEvents.Add(new AuditEvent { OrganizationId = actor.OrganizationId, FacilityId = facilityId,
            ActorId = actor.UserId, Action = "Knowledge.AnswerRequested", EntityId = facilityId });
        await db.SaveChangesAsync(ct); return response;
    }
    [HttpGet("facilities/{facilityId:guid}/reports/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid facilityId, Guid id, CancellationToken ct)
    {
        // Reuse assigned-radiologist access and read auditing; drafts are never exported as final.
        await workflow.GetReport(facilityId, id, ct);
        var report = await db.Reports.SingleAsync(x => x.FacilityId == facilityId && x.Id == id, ct);
        if (report.Status != ReportStatus.Signed) throw new RuleViolation("conflict", "Only signed reports may be exported as final.");
        var version = await db.ReportVersions.SingleAsync(x => x.FacilityId == facilityId && x.ReportId == id && x.Number == report.CurrentVersion, ct);
        var signature = await db.Signatures.SingleAsync(x => x.FacilityId == facilityId && x.ReportVersionId == version.Id, ct);
        db.AuditEvents.Add(new AuditEvent { OrganizationId = actor.OrganizationId, FacilityId = facilityId,
            ActorId = actor.UserId, Action = "Report.Exported", EntityId = id });
        await db.SaveChangesAsync(ct);
        var text = $"SIGNED REPORT\nReport: {id}\nVersion: {version.Number}\nLanguage: {version.Language}\n\nExamination\n{version.Examination}\n\nFindings\n{version.Findings}\n\nImpression\n{version.Impression}\n\nSigner: {signature.SignedBy}\nSigned UTC: {DateTimeOffset.FromUnixTimeMilliseconds(signature.CreatedAt):O}\nSHA256: {signature.ContentHash}\n";
        return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", $"report-{id}.txt");
    }
}
public sealed record KnowledgeRequest(string Question, Guid[] DocumentIds);
