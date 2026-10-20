using AIFramework.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RadIntel.AI;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;
using RadIntel.Infrastructure;

namespace RadIntel.Tests;

public sealed class TestActor : ICurrentActor
{
    public Guid OrganizationId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "test-radiologist";
    public bool CanProvision { get; set; } = true;
    public bool HasRecentMfa { get; set; } = true;
}

public sealed class TestClinic : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TestActor Actor { get; } = new();
    public RadIntelDbContext Db { get; private set; } = null!;
    public WorkflowService Service { get; private set; } = null!;
    public Guid FacilityId { get; private set; }
    public DbContextOptions<RadIntelDbContext> Options { get; private set; } = null!;

    public static async Task<TestClinic> Create()
    {
        var fixture = new TestClinic(); await fixture.connection.OpenAsync();
        fixture.Options = new DbContextOptionsBuilder<RadIntelDbContext>().UseSqlite(fixture.connection).Options;
        fixture.Db = new(fixture.Options, fixture.Actor);
        // Ephemeral test schema only: no migration runner or application database.
        await fixture.Db.Database.EnsureCreatedAsync();
        fixture.Service = new(fixture.Db, fixture.Actor, new VerbatimDraftGenerator(), new PublishedTemplateRetriever(fixture.Db, fixture.Actor));
        await fixture.Service.Onboard(new("Test organization", "Test facility", "UTC"), default);
        fixture.FacilityId = await fixture.Db.Facilities.Select(x => x.Id).SingleAsync();
        return fixture;
    }
    public async Task<ImagingOrder> Order()
    {
        var patient = await Service.CreatePatient(FacilityId, new(Guid.NewGuid().ToString(), "test-issuer", "Synthetic test person", null), default);
        return await Service.CreateOrder(FacilityId, new(patient.Id, "DX", "Chest", "Test referral"), default);
    }
    public async Task<Report> Report()
    {
        var order = await Order();
        order = await Service.Transition(FacilityId, order.Id, new(order.ConcurrencyToken, "CheckedIn", "Test check-in"), default);
        order = await Service.Transition(FacilityId, order.Id, new(order.ConcurrencyToken, "Acquired", "Test acquisition"), default);
        order = await Service.Claim(FacilityId, order.Id, new(order.ConcurrencyToken), default);
        return await Service.CreateReport(FacilityId, order.Id, new(order.ConcurrencyToken), default);
    }
    public async Task<Report> Draft()
    {
        var report = await Report();
        return await Service.SaveDraft(FacilityId, report.Id, new(report.ConcurrencyToken, "Chest X-ray", "Physician-supplied test observations.", "Clinician-authored test impression.", "en"), default);
    }
    public async Task<ReportTemplate> Template()
    {
        var template = await Service.CreateTemplate(FacilityId, new("Test template", "en", "DX", "Chest", "Examination, Findings, Impression. Do not invent findings.", 1), default);
        return await Service.PublishTemplate(FacilityId, template.Id, new(template.ConcurrencyToken), true, default);
    }
    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
}

public sealed class WorkflowTests
{
    [Fact]
    public async Task Onboarding_is_explicit_and_does_not_duplicate_organization()
    {
        await using var clinic = await TestClinic.Create();
        Assert.Equal(0, await clinic.Db.Patients.CountAsync());
        Assert.Equal(0, await clinic.Db.Templates.CountAsync());
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.Onboard(new("Duplicate", "Duplicate", "UTC"), default));
    }
    [Fact]
    public async Task Tenant_filter_and_facility_authorization_prevent_cross_scope_access()
    {
        await using var clinic = await TestClinic.Create(); await clinic.Order();
        clinic.Actor.OrganizationId = Guid.NewGuid();
        Assert.Empty(await clinic.Db.Patients.AsNoTracking().ToListAsync());
        await Assert.ThrowsAsync<AccessDeniedException>(() => clinic.Service.Worklist(clinic.FacilityId, null, 1, 30, default));
    }
    [Fact]
    public async Task Token_roles_do_not_replace_database_membership()
    {
        await using var clinic = await TestClinic.Create();
        clinic.Actor.UserId = "unregistered-user";
        await Assert.ThrowsAsync<AccessDeniedException>(() => clinic.Service.CreatePatient(clinic.FacilityId, new("id", "issuer", "Name", null), default));
    }
    [Fact]
    public async Task Read_only_membership_cannot_create_patient()
    {
        await using var clinic = await TestClinic.Create();
        await clinic.Service.SetMembership(clinic.FacilityId, new("auditor", (int)ClinicRole.Auditor, true), default);
        clinic.Actor.UserId = "auditor";
        await Assert.ThrowsAsync<AccessDeniedException>(() => clinic.Service.CreatePatient(clinic.FacilityId, new("id", "issuer", "Name", null), default));
    }
    [Fact]
    public async Task Duplicate_patient_identifier_is_rejected_by_relational_constraint()
    {
        await using var clinic = await TestClinic.Create();
        var request = new PatientRequest("same", "issuer", "Name", null);
        await clinic.Service.CreatePatient(clinic.FacilityId, request, default);
        await Assert.ThrowsAsync<DbUpdateException>(() => clinic.Service.CreatePatient(clinic.FacilityId, request, default));
    }
    [Fact]
    public async Task Cross_facility_patient_reference_is_rejected()
    {
        await using var clinic = await TestClinic.Create();
        var patient = await clinic.Service.CreatePatient(clinic.FacilityId, new("id", "issuer", "Name", null), default);
        var other = new Facility { Id = Guid.NewGuid(), OrganizationId = clinic.Actor.OrganizationId, Name = "Other" };
        clinic.Db.Facilities.Add(other); await clinic.Db.SaveChangesAsync();
        clinic.Db.Orders.Add(new ImagingOrder { OrganizationId = clinic.Actor.OrganizationId, FacilityId = other.Id, PatientId = patient.Id, Modality = "DX", BodyRegion = "Chest" });
        await Assert.ThrowsAsync<DbUpdateException>(() => clinic.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Claim_requires_acquisition_and_rejects_stale_token()
    {
        await using var clinic = await TestClinic.Create(); var order = await clinic.Order();
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.Claim(clinic.FacilityId, order.Id, new(order.ConcurrencyToken), default));
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.Transition(clinic.FacilityId, order.Id, new(Guid.NewGuid(), "CheckedIn", "reason"), default));
    }
    [Fact]
    public async Task Competing_claims_use_database_optimistic_concurrency()
    {
        await using var clinic = await TestClinic.Create(); var order = await clinic.Order();
        await clinic.Service.Transition(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "CheckedIn", "reason"), default);
        await clinic.Service.Transition(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "Acquired", "reason"), default);
        await using var second = new RadIntelDbContext(clinic.Options, clinic.Actor);
        var competing = await second.Orders.SingleAsync(x => x.Id == order.Id);
        await clinic.Service.Claim(clinic.FacilityId, order.Id, new(order.ConcurrencyToken), default);
        competing.Claim("another-radiologist");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
    [Fact]
    public async Task Scheduling_blocks_overlap_and_allows_adjacent_appointments()
    {
        await using var clinic = await TestClinic.Create(); var a = await clinic.Order(); var b = await clinic.Order();
        var equipment = await clinic.Service.CreateEquipment(clinic.FacilityId, new("Scanner", "DX"), default);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await clinic.Service.Schedule(clinic.FacilityId, a.Id, new(a.ConcurrencyToken, equipment.Id, start, start.AddMinutes(30)), default);
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.Schedule(clinic.FacilityId, b.Id, new(b.ConcurrencyToken, equipment.Id, start.AddMinutes(10), start.AddMinutes(40)), default));
        await clinic.Service.Schedule(clinic.FacilityId, b.Id, new(b.ConcurrencyToken, equipment.Id, start.AddMinutes(30), start.AddMinutes(60)), default);
        Assert.Equal(2, await clinic.Db.Appointments.CountAsync(x => !x.Cancelled));
    }
    [Fact]
    public async Task Rescheduling_replaces_one_active_appointment_atomically()
    {
        await using var clinic = await TestClinic.Create(); var order = await clinic.Order();
        var equipment = await clinic.Service.CreateEquipment(clinic.FacilityId, new("Scanner", "DX"), default);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        await clinic.Service.Schedule(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, equipment.Id, start, start.AddMinutes(30)), default);
        await clinic.Service.Schedule(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, equipment.Id, start.AddHours(1), start.AddHours(2)), default);
        Assert.Equal(1, await clinic.Db.Appointments.CountAsync(x => !x.Cancelled));
        Assert.Equal(1, await clinic.Db.Appointments.CountAsync(x => x.Cancelled));
    }
    [Fact]
    public async Task Signature_requires_review_and_recent_mfa()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Draft();
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.Sign(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, "key"), default));
        await clinic.Service.Submit(clinic.FacilityId, report.Id, new(report.ConcurrencyToken), default);
        clinic.Actor.HasRecentMfa = false;
        await Assert.ThrowsAsync<AccessDeniedException>(() => clinic.Service.Sign(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, "key"), default));
    }
    [Fact]
    public async Task Signing_is_idempotent_and_freezes_exact_content_with_audit_and_outbox()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Draft();
        await clinic.Service.Submit(clinic.FacilityId, report.Id, new(report.ConcurrencyToken), default);
        var request = new SignRequest(report.ConcurrencyToken, "once");
        var signature = await clinic.Service.Sign(clinic.FacilityId, report.Id, request, default);
        var retry = await clinic.Service.Sign(clinic.FacilityId, report.Id, request, default);
        Assert.Equal(signature.Id, retry.Id);
        Assert.Equal(ReportStatus.Signed, report.Status);
        Assert.Equal((await clinic.Db.ReportVersions.SingleAsync()).Content().Digest(), signature.ContentHash);
        Assert.Single(await clinic.Db.Outbox.ToListAsync());
        Assert.Single(await clinic.Db.AuditEvents.Where(x => x.Action == "Report.Signed").ToListAsync());
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.SaveDraft(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, "Changed", "Changed", "Changed", "en"), default));
    }
    [Fact]
    public async Task Historical_versions_and_audit_events_cannot_be_mutated_through_context()
    {
        await using var clinic = await TestClinic.Create(); await clinic.Draft();
        var version = await clinic.Db.ReportVersions.SingleAsync(); version.Findings = "Tampered";
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Db.SaveChangesAsync());
        clinic.Db.ChangeTracker.Clear();
        var audit = await clinic.Db.AuditEvents.FirstAsync(); clinic.Db.AuditEvents.Remove(audit);
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Addendum_does_not_change_signed_original()
    {
        await using var clinic = await TestClinic.Create(); var original = await clinic.Draft();
        await clinic.Service.Submit(clinic.FacilityId, original.Id, new(original.ConcurrencyToken), default);
        await clinic.Service.Sign(clinic.FacilityId, original.Id, new(original.ConcurrencyToken, "original"), default);
        var token = original.ConcurrencyToken;
        var addendum = await clinic.Service.Addendum(clinic.FacilityId, original.Id, new(token, "Clinician correction"), default);
        Assert.Equal(original.Id, addendum.ParentReportId);
        Assert.Equal(token, original.ConcurrencyToken);
        Assert.Equal(ReportStatus.Signed, original.Status);
    }
    [Fact]
    public async Task Another_radiologist_cannot_access_an_assigned_report()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Draft();
        await clinic.Service.SetMembership(clinic.FacilityId, new("other-rad", (int)ClinicRole.Radiologist, true), default);
        clinic.Actor.UserId = "other-rad";
        await Assert.ThrowsAsync<AccessDeniedException>(() => clinic.Service.GetReport(clinic.FacilityId, report.Id, default));
    }
    [Fact]
    public async Task Proposal_acceptance_creates_only_a_draft_and_preserves_checked_findings()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Report(); var template = await clinic.Template();
        var transcript = await clinic.Service.Transcript(clinic.FacilityId, report.Id, new("No test finding.", "No test finding.", "en", true), default);
        var proposal = await clinic.Service.Generate(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, transcript.Id, template.Id), default);
        await clinic.Service.ReviewProposal(clinic.FacilityId, report.Id, proposal.Id, new(report.ConcurrencyToken, true, "Test exam", "Clinician impression"), default);
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(transcript.CheckedText, (await clinic.Db.ReportVersions.SingleAsync()).Findings);
        Assert.Empty(await clinic.Db.Signatures.ToListAsync());
        Assert.Empty(await clinic.Db.Outbox.ToListAsync());
    }
    [Fact]
    public async Task Revoked_template_prevents_proposal_acceptance()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Report(); var template = await clinic.Template();
        var transcript = await clinic.Service.Transcript(clinic.FacilityId, report.Id, new("Test input", "Test input", "en", true), default);
        var proposal = await clinic.Service.Generate(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, transcript.Id, template.Id), default);
        template = await clinic.Db.Templates.SingleAsync();
        await clinic.Service.PublishTemplate(clinic.FacilityId, template.Id, new(template.ConcurrencyToken), false, default);
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.ReviewProposal(clinic.FacilityId, report.Id, proposal.Id, new(report.ConcurrencyToken, true, "Exam", "Impression"), default));
    }
    [Fact]
    public async Task Changed_transcript_or_report_invalidates_old_proposal()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Report(); var template = await clinic.Template();
        var transcript = await clinic.Service.Transcript(clinic.FacilityId, report.Id, new("First", "First", "en", true), default);
        var proposal = await clinic.Service.Generate(clinic.FacilityId, report.Id, new(report.ConcurrencyToken, transcript.Id, template.Id), default);
        report = await clinic.Db.Reports.SingleAsync();
        await clinic.Service.Transcript(clinic.FacilityId, report.Id, new("Second", "Second", "en", true), default);
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Service.ReviewProposal(clinic.FacilityId, report.Id, proposal.Id, new(report.ConcurrencyToken, true, "Exam", "Impression"), default));
    }
    [Fact]
    public async Task Retriever_fails_closed_without_explicit_document_ids_or_scope()
    {
        await using var clinic = await TestClinic.Create(); var template = await clinic.Template();
        var retriever = new PublishedTemplateRetriever(clinic.Db, clinic.Actor);
        Assert.Empty(await retriever.SearchAsync(new("test", "radiology-template", clinic.Actor.OrganizationId, clinic.FacilityId, null, 5), default));
        Assert.Empty(await retriever.SearchAsync(new("test", "radiology-template", clinic.Actor.OrganizationId, null, [template.Id], 5), default));
        Assert.Empty(await retriever.SearchAsync(new("test", "radiology-template", Guid.NewGuid(), clinic.FacilityId, [template.Id], 5), default));
        Assert.Single(await retriever.SearchAsync(new("test", "radiology-template", clinic.Actor.OrganizationId, clinic.FacilityId, [template.Id], 5), default));
    }
    [Fact]
    public async Task Ai_disabled_does_not_break_manual_reporting()
    {
        await using var clinic = await TestClinic.Create(); var report = await clinic.Draft();
        await Assert.ThrowsAsync<FeatureUnavailableException>(() => new DisabledDraftGenerator().GenerateAsync(new(), new(), [], default));
        Assert.Equal(ReportStatus.Draft, report.Status);
    }
}
