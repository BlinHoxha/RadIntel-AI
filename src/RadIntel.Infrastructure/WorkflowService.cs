using System.Text.Json;
using AIFramework.Core.Abstractions;
using AIFramework.Core.Models;
using Microsoft.EntityFrameworkCore;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;

namespace RadIntel.Infrastructure;

public sealed class WorkflowService(RadIntelDbContext db, ICurrentActor actor,
    IReportDraftGenerator generator, IKnowledgeRetriever retriever)
{
    private const ClinicRole Staff = ClinicRole.Administrator | ClinicRole.Scheduler | ClinicRole.Technician | ClinicRole.Radiologist;

    public async Task<Membership> Require(Guid facilityId, ClinicRole roles, CancellationToken ct)
    {
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.FacilityId == facilityId && x.UserId == actor.UserId && x.Active, ct);
        if (member is null || (member.Roles & roles) == 0) throw new AccessDeniedException();
        return member;
    }
    private T Own<T>(T entity, Guid facility) where T : TenantEntity
    {
        entity.OrganizationId = actor.OrganizationId; entity.FacilityId = facility;
        return entity;
    }
    private void Audit(Guid facility, string action, Guid id, string? reason = null) => db.AuditEvents.Add(Own(new AuditEvent
        { ActorId = actor.UserId, Action = action, EntityId = id, Reason = reason }, facility));
    private static int Size(int size) => Math.Clamp(size, 1, 100);
    private static int Page(int page) => Math.Clamp(page, 1, 10000);
    private async Task<PageResult<T>> Paginate<T>(IQueryable<T> query, int page, int size, CancellationToken ct)
    {
        page = Page(page); size = Size(size);
        return new(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), page, size, await query.CountAsync(ct));
    }

    public async Task<object> Onboard(OnboardRequest request, CancellationToken ct)
    {
        if (!actor.CanProvision || actor.OrganizationId == Guid.Empty) throw new AccessDeniedException();
        if (await db.Organizations.AnyAsync(ct)) throw new RuleViolation("conflict", "Organization is already provisioned.");
        try { TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone); }
        catch (TimeZoneNotFoundException) { throw new RuleViolation("validation", "Unknown time zone."); }
        var org = new Organization { Id = actor.OrganizationId, Name = Rules.Text(request.OrganizationName, "Organization name", 200) };
        var facility = new Facility { Id = Guid.NewGuid(), OrganizationId = org.Id,
            Name = Rules.Text(request.FacilityName, "Facility name", 200), TimeZone = request.TimeZone };
        db.Organizations.Add(org); db.Facilities.Add(facility);
        db.Memberships.Add(Own(new Membership { UserId = actor.UserId, Roles = Staff }, facility.Id));
        Audit(facility.Id, "Organization.Onboarded", org.Id);
        await db.SaveChangesAsync(ct);
        return new { OrganizationId = org.Id, FacilityId = facility.Id };
    }
    public async Task<object> Session(CancellationToken ct) => new
    {
        actor.OrganizationId, actor.UserId, actor.CanProvision, actor.HasRecentMfa,
        Facilities = await (from m in db.Memberships.AsNoTracking()
                            join f in db.Facilities.AsNoTracking() on m.FacilityId equals f.Id
                            where m.UserId == actor.UserId && m.Active
                            select new { f.Id, f.Name, Roles = (int)m.Roles }).ToListAsync(ct)
    };
    public async Task<Membership> SetMembership(Guid facility, MembershipRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator, ct);
        var roles = (ClinicRole)request.Roles;
        if ((roles & ~(Staff | ClinicRole.Auditor)) != 0 || roles == ClinicRole.None)
            throw new RuleViolation("validation", "At least one valid clinic role is required.");
        if (request.UserId == actor.UserId) throw new RuleViolation("validation", "Another administrator must change your own membership.");
        var user = Rules.Text(request.UserId, "User ID", 200);
        var membership = await db.Memberships.SingleOrDefaultAsync(x => x.FacilityId == facility && x.UserId == user, ct);
        if (membership is null) { membership = Own(new Membership { UserId = user }, facility); db.Memberships.Add(membership); }
        membership.Roles = roles; membership.Active = request.Active;
        Audit(facility, "Membership.Changed", membership.Id);
        await db.SaveChangesAsync(ct);
        return membership;
    }
    public async Task<Patient> CreatePatient(Guid facility, PatientRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator | ClinicRole.Scheduler, ct);
        if (request.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new RuleViolation("validation", "Birth date cannot be in the future.");
        var patient = Own(new Patient { Identifier = Rules.Text(request.Identifier, "Identifier", 100),
            IdentifierIssuer = Rules.Text(request.IdentifierIssuer, "Identifier issuer", 100),
            DisplayName = Rules.Text(request.DisplayName, "Display name", 200), BirthDate = request.BirthDate }, facility);
        db.Patients.Add(patient); Audit(facility, "Patient.Created", patient.Id); await db.SaveChangesAsync(ct);
        return patient;
    }
    public async Task<PageResult<Patient>> Patients(Guid facility, int page, int size, CancellationToken ct)
    {
        var member = await Require(facility, Staff, ct);
        var query = db.Patients.AsNoTracking().Where(x => x.FacilityId == facility);
        if ((member.Roles & (ClinicRole.Administrator | ClinicRole.Scheduler | ClinicRole.Technician)) == 0)
            query = query.Where(p => db.Orders.Any(o => o.FacilityId == facility && o.PatientId == p.Id && o.AssignedTo == actor.UserId));
        var result = await Paginate(query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id), page, size, ct);
        Audit(facility, "Patient.ListViewed", facility); await db.SaveChangesAsync(ct);
        return result;
    }
    public async Task<ImagingOrder> CreateOrder(Guid facility, OrderRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Scheduler | ClinicRole.Administrator, ct);
        if (!await db.Patients.AnyAsync(x => x.FacilityId == facility && x.Id == request.PatientId, ct)) throw new MissingResourceException();
        if (request.Priority is not ("Routine" or "Urgent")) throw new RuleViolation("validation", "Priority must be Routine or Urgent and entered by authorized staff.");
        var order = Own(new ImagingOrder { PatientId = request.PatientId, Modality = Rules.Text(request.Modality, "Modality", 20).ToUpperInvariant(),
            BodyRegion = Rules.Text(request.BodyRegion, "Body region", 100), Referral = request.Referral?.Trim() ?? "", Priority = request.Priority }, facility);
        db.Orders.Add(order); Audit(facility, "Order.Created", order.Id); await db.SaveChangesAsync(ct);
        return order;
    }
    public async Task<PageResult<ImagingOrder>> Worklist(Guid facility, string? status, int page, int size, CancellationToken ct)
    {
        var member = await Require(facility, Staff, ct);
        var query = db.Orders.AsNoTracking().Where(x => x.FacilityId == facility);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, false, out var parsed) || !Enum.IsDefined(parsed)) throw new RuleViolation("validation", "Invalid order status.");
            query = query.Where(x => x.Status == parsed);
        }
        if ((member.Roles & (ClinicRole.Administrator | ClinicRole.Scheduler | ClinicRole.Technician)) == 0)
            query = query.Where(x => x.AssignedTo == actor.UserId || (x.Status == OrderStatus.Acquired && x.AssignedTo == null));
        var result = await Paginate(query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id), page, size, ct);
        Audit(facility, "Worklist.Viewed", facility); await db.SaveChangesAsync(ct);
        return result;
    }
    private async Task<ImagingOrder> FindOrder(Guid facility, Guid id, CancellationToken ct) =>
        await db.Orders.SingleOrDefaultAsync(x => x.FacilityId == facility && x.Id == id, ct) ?? throw new MissingResourceException();
    public async Task<ImagingOrder> Transition(Guid facility, Guid id, TransitionRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, false, out var next) || !Enum.IsDefined(next) ||
            next is not (OrderStatus.CheckedIn or OrderStatus.Acquired or OrderStatus.Cancelled))
            throw new RuleViolation("validation", "Use scheduling, claim and reporting commands for other transitions.");
        await Require(facility, next == OrderStatus.Acquired ? ClinicRole.Technician : ClinicRole.Scheduler | ClinicRole.Administrator, ct);
        Rules.Text(request.Reason, "Reason", 500);
        var order = await FindOrder(facility, id, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        order.Transition(next);
        if (next == OrderStatus.Cancelled)
            foreach (var appointment in await db.Appointments.Where(x => x.FacilityId == facility && x.OrderId == id && !x.Cancelled).ToListAsync(ct)) appointment.Cancelled = true;
        Audit(facility, "Order." + next, id, request.Reason.Trim()); await db.SaveChangesAsync(ct);
        return order;
    }
    public async Task<ImagingOrder> Claim(Guid facility, Guid id, ConcurrencyRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Radiologist, ct);
        var order = await FindOrder(facility, id, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        order.Claim(actor.UserId); Audit(facility, "Order.Claimed", id); await db.SaveChangesAsync(ct);
        return order;
    }
    public async Task<Equipment> CreateEquipment(Guid facility, EquipmentRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator, ct);
        var equipment = Own(new Equipment { Name = Rules.Text(request.Name, "Equipment name", 200), Modality = Rules.Text(request.Modality, "Modality", 20).ToUpperInvariant() }, facility);
        db.Equipment.Add(equipment); Audit(facility, "Equipment.Created", equipment.Id); await db.SaveChangesAsync(ct); return equipment;
    }
    public async Task<IReadOnlyList<Equipment>> GetEquipment(Guid facility, CancellationToken ct)
    {
        await Require(facility, Staff, ct);
        return await db.Equipment.AsNoTracking().Where(x => x.FacilityId == facility).OrderBy(x => x.Name).Take(500).ToListAsync(ct);
    }
    public async Task<Appointment> Schedule(Guid facility, Guid id, ScheduleRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Scheduler | ClinicRole.Administrator, ct);
        var order = await FindOrder(facility, id, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        if (order.Status is not (OrderStatus.Requested or OrderStatus.Scheduled)) throw new RuleViolation("conflict", "Only requested or scheduled orders can be scheduled.");
        var equipment = await db.Equipment.SingleOrDefaultAsync(x => x.FacilityId == facility && x.Id == request.EquipmentId, ct) ?? throw new MissingResourceException();
        if (!equipment.Available || equipment.Modality != order.Modality) throw new RuleViolation("validation", "Equipment must be available and match the modality.");
        var starts = request.StartsAt.ToUnixTimeMilliseconds(); var ends = request.EndsAt.ToUnixTimeMilliseconds();
        if (ends <= starts || ends - starts > 24 * 60 * 60 * 1000) throw new RuleViolation("validation", "A positive appointment duration of at most 24 hours is required.");
        if (await db.Appointments.AnyAsync(x => x.FacilityId == facility && x.EquipmentId == equipment.Id && !x.Cancelled && x.OrderId != id && x.StartsAt < ends && x.EndsAt > starts, ct))
            throw new RuleViolation("conflict", "The equipment is already booked for this time.");
        // Optimistic resource write serializes competing bookings even when appointment reads race.
        equipment.ConcurrencyToken = Guid.NewGuid();
        foreach (var old in await db.Appointments.Where(x => x.FacilityId == facility && x.OrderId == id && !x.Cancelled).ToListAsync(ct)) old.Cancelled = true;
        // Persist cancellation in the same explicit transaction before inserting its unique replacement.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (order.Status == OrderStatus.Requested) order.Transition(OrderStatus.Scheduled);
        else order.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        var appointment = Own(new Appointment { OrderId = id, EquipmentId = equipment.Id, StartsAt = starts, EndsAt = ends }, facility);
        db.Appointments.Add(appointment); Audit(facility, "Appointment.Scheduled", appointment.Id);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return appointment;
    }
    private async Task<(Report Report, ImagingOrder Order)> AccessReport(Guid facility, Guid id, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Radiologist, ct);
        var report = await db.Reports.SingleOrDefaultAsync(x => x.FacilityId == facility && x.Id == id, ct) ?? throw new MissingResourceException();
        var order = await FindOrder(facility, report.OrderId, ct);
        if (order.AssignedTo != actor.UserId) throw new AccessDeniedException();
        return (report, order);
    }
    public async Task<Report> CreateReport(Guid facility, Guid orderId, ConcurrencyRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Radiologist, ct);
        var order = await FindOrder(facility, orderId, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        if (order.AssignedTo != actor.UserId) throw new AccessDeniedException();
        order.Transition(OrderStatus.ReportDraft);
        var report = Own(new Report { OrderId = orderId }, facility);
        db.Reports.Add(report); Audit(facility, "Report.Created", report.Id); await db.SaveChangesAsync(ct); return report;
    }
    public async Task<object> GetReport(Guid facility, Guid id, CancellationToken ct)
    {
        var (report, _) = await AccessReport(facility, id, ct);
        var versions = await db.ReportVersions.AsNoTracking().Where(x => x.FacilityId == facility && x.ReportId == id).OrderBy(x => x.Number).ToListAsync(ct);
        var versionIds = versions.Select(x => x.Id).ToArray();
        var signatures = await db.Signatures.AsNoTracking().Where(x => x.FacilityId == facility && versionIds.Contains(x.ReportVersionId)).ToListAsync(ct);
        Audit(facility, "Report.Viewed", id); await db.SaveChangesAsync(ct);
        return new { Report = report, Versions = versions, Signatures = signatures,
            Addenda = await db.Reports.AsNoTracking().Where(x => x.FacilityId == facility && x.ParentReportId == id).ToListAsync(ct) };
    }
    private ReportVersion NewVersion(Report report, ReportContent input)
    {
        var content = input.Validate(); report.AddVersion();
        var version = Own(new ReportVersion { ReportId = report.Id, Number = report.CurrentVersion,
            Examination = content.Examination, Findings = content.Findings, Impression = content.Impression,
            Language = content.Language, AuthorId = actor.UserId }, report.FacilityId);
        db.ReportVersions.Add(version); return version;
    }
    public async Task<Report> SaveDraft(Guid facility, Guid id, VersionRequest request, CancellationToken ct)
    {
        var (report, _) = await AccessReport(facility, id, ct); Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        NewVersion(report, new(request.Examination, request.Findings, request.Impression, request.Language));
        Audit(facility, "Report.DraftSaved", id); await db.SaveChangesAsync(ct); return report;
    }
    public async Task<Report> Submit(Guid facility, Guid id, ConcurrencyRequest request, CancellationToken ct)
    {
        var (report, _) = await AccessReport(facility, id, ct); Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        report.Submit(); Audit(facility, "Report.SubmittedForReview", id); await db.SaveChangesAsync(ct); return report;
    }
    public async Task<Signature> Sign(Guid facility, Guid id, SignRequest request, CancellationToken ct)
    {
        var (report, order) = await AccessReport(facility, id, ct);
        if (!actor.HasRecentMfa) throw new AccessDeniedException();
        var key = Rules.Text(request.RequestKey, "Request key", 100);
        var prior = await db.Signatures.SingleOrDefaultAsync(x => x.FacilityId == facility && x.RequestKey == key && x.SignedBy == actor.UserId, ct);
        if (prior is not null)
        {
            if (!await db.ReportVersions.AnyAsync(x => x.Id == prior.ReportVersionId && x.ReportId == id && x.FacilityId == facility, ct))
                throw new RuleViolation("conflict", "Request key was already used for another report.");
            return prior;
        }
        Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        if (report.Status != ReportStatus.PendingReview) throw new RuleViolation("conflict", "Submit the current draft for review before signing.");
        var version = await db.ReportVersions.SingleAsync(x => x.FacilityId == facility && x.ReportId == id && x.Number == report.CurrentVersion, ct);
        version.Content().Validate(); report.Sign();
        if (report.Kind == 0) order.Transition(OrderStatus.Signed);
        var signature = Own(new Signature { ReportVersionId = version.Id, SignedBy = actor.UserId,
            ContentHash = version.Content().Digest(), RequestKey = key }, facility);
        db.Signatures.Add(signature);
        db.Outbox.Add(Own(new OutboxMessage { ReportVersionId = version.Id,
            Payload = JsonSerializer.Serialize(new { ReportId = report.Id, VersionId = version.Id, SignatureId = signature.Id }) }, facility));
        Audit(facility, "Report.Signed", id); await db.SaveChangesAsync(ct); return signature;
    }
    public async Task<Report> Addendum(Guid facility, Guid id, AddendumRequest request, CancellationToken ct)
    {
        var (original, order) = await AccessReport(facility, id, ct); Rules.Match(original.ConcurrencyToken, request.ExpectedToken);
        if (original.Status != ReportStatus.Signed || original.Kind != 0) throw new RuleViolation("conflict", "Addenda must reference a signed original report.");
        var addendum = Own(new Report { OrderId = order.Id, ParentReportId = id, Kind = 1,
            AmendmentReason = Rules.Text(request.Reason, "Amendment reason", 1000) }, facility);
        db.Reports.Add(addendum); Audit(facility, "Report.AddendumCreated", addendum.Id); await db.SaveChangesAsync(ct); return addendum;
    }
    public async Task<ReportTemplate> CreateTemplate(Guid facility, TemplateRequest request, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator, ct);
        if (request.Version < 1) throw new RuleViolation("validation", "Template version must be positive.");
        var template = Own(new ReportTemplate { Name = Rules.Text(request.Name, "Name", 200), Language = Rules.Language(request.Language),
            Modality = Rules.Text(request.Modality, "Modality", 20).ToUpperInvariant(), BodyRegion = Rules.Text(request.BodyRegion, "Body region", 100),
            Structure = Rules.Text(request.Structure, "Structure", 10000), Version = request.Version }, facility);
        db.Templates.Add(template); Audit(facility, "Template.Created", template.Id); await db.SaveChangesAsync(ct); return template;
    }
    public async Task<ReportTemplate> PublishTemplate(Guid facility, Guid id, ConcurrencyRequest request, bool publish, CancellationToken ct)
    {
        // Clinical publication requires both administration and radiology privileges.
        await Require(facility, ClinicRole.Administrator, ct); await Require(facility, ClinicRole.Radiologist, ct);
        var template = await db.Templates.SingleOrDefaultAsync(x => x.FacilityId == facility && x.Id == id, ct) ?? throw new MissingResourceException();
        Rules.Match(template.ConcurrencyToken, request.ExpectedToken); template.Published = publish;
        Audit(facility, publish ? "Template.Published" : "Template.Revoked", id); await db.SaveChangesAsync(ct); return template;
    }
    public async Task<IReadOnlyList<ReportTemplate>> Templates(Guid facility, CancellationToken ct)
    {
        var membership = await Require(facility, ClinicRole.Administrator | ClinicRole.Radiologist, ct);
        return await db.Templates.AsNoTracking().Where(x => x.FacilityId == facility &&
            (x.Published || (membership.Roles & ClinicRole.Administrator) != 0)).OrderBy(x => x.Name).ThenByDescending(x => x.Version).Take(500).ToListAsync(ct);
    }
    public async Task<DictationTranscript> Transcript(Guid facility, Guid id, TranscriptRequest request, CancellationToken ct)
    {
        var (report, _) = await AccessReport(facility, id, ct);
        if (report.Status == ReportStatus.Signed) throw new RuleViolation("conflict", "Cannot add input to a signed report.");
        if (!request.ConfirmedByRadiologist) throw new RuleViolation("validation", "The radiologist must check and confirm the transcript.");
        var transcript = Own(new DictationTranscript { ReportId = id, OriginalText = Rules.Text(request.OriginalText, "Original transcript", 30000),
            CheckedText = Rules.Text(request.CheckedText, "Checked transcript", 30000), Language = Rules.Language(request.Language), CheckedBy = actor.UserId }, facility);
        db.Transcripts.Add(transcript); report.CheckedTranscriptId = transcript.Id;
        Audit(facility, "Transcript.Checked", transcript.Id); await db.SaveChangesAsync(ct); return transcript;
    }
    public async Task<AIProposal> Generate(Guid facility, Guid id, ProposalRequest request, CancellationToken ct)
    {
        var (report, order) = await AccessReport(facility, id, ct); Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        if (report.Status == ReportStatus.Signed) throw new RuleViolation("conflict", "Cannot generate for a signed report.");
        if (report.CheckedTranscriptId != request.TranscriptId) throw new RuleViolation("conflict", "Use the latest checked transcript.");
        var transcript = await db.Transcripts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TranscriptId && x.ReportId == id && x.FacilityId == facility, ct) ?? throw new MissingResourceException();
        var template = await db.Templates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TemplateId && x.FacilityId == facility && x.Published, ct) ?? throw new MissingResourceException();
        if (template.Language != transcript.Language || template.Modality != order.Modality || template.BodyRegion != order.BodyRegion)
            throw new RuleViolation("validation", "Template language, modality and body region must match the encounter.");
        var evidence = await retriever.SearchAsync(new KnowledgeSearchQuery(order.BodyRegion, "radiology-template",
            actor.OrganizationId, facility, [template.Id], 1), ct);
        if (evidence.Count != 1 || evidence.Any(x => x.DocumentId != template.Id || x.TenantId != actor.OrganizationId || x.ScopeId != facility))
            throw new RuleViolation("conflict", "Approved template evidence is unavailable.");
        var generated = await generator.GenerateAsync(transcript, template, evidence, ct);
        // Every generated clinical span must be verbatim input. Impression remains clinician-authored.
        Rules.Text(generated.Findings, "Generated findings", 30000);
        if (!string.Equals(generated.Findings, transcript.CheckedText, StringComparison.Ordinal))
            throw new RuleViolation("validation", "The draft generator changed physician observations.");
        // Recheck authorization, publication and base version after the external call.
        db.ChangeTracker.Clear();
        (report, _) = await AccessReport(facility, id, ct); Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        if (!await db.Templates.AnyAsync(x => x.Id == template.Id && x.FacilityId == facility && x.Published && x.ConcurrencyToken == template.ConcurrencyToken, ct))
            throw new RuleViolation("conflict", "The template changed during generation.");
        var proposal = Own(new AIProposal { ReportId = id, TranscriptId = transcript.Id, TemplateId = template.Id,
            BaseVersion = report.CurrentVersion, BaseConcurrencyToken = report.ConcurrencyToken, Findings = generated.Findings,
            Provider = generated.Provider, ModelVersion = generated.ModelVersion,
            SourcesJson = JsonSerializer.Serialize(evidence.Select(x => new { x.DocumentId, x.Id, TemplateVersion = template.Version, TemplateToken = template.ConcurrencyToken })) }, facility);
        db.Proposals.Add(proposal); Audit(facility, "AIProposal.Generated", proposal.Id); await db.SaveChangesAsync(ct); return proposal;
    }
    public async Task<object> ReviewProposal(Guid facility, Guid id, Guid proposalId, ReviewProposalRequest request, CancellationToken ct)
    {
        var (report, _) = await AccessReport(facility, id, ct); Rules.Match(report.ConcurrencyToken, request.ExpectedToken);
        var proposal = await db.Proposals.SingleOrDefaultAsync(x => x.Id == proposalId && x.ReportId == id && x.FacilityId == facility, ct) ?? throw new MissingResourceException();
        if (proposal.Status != ProposalStatus.PendingReview) throw new RuleViolation("conflict", "Proposal was already reviewed.");
        if (request.Accept)
        {
            if (proposal.BaseVersion != report.CurrentVersion || proposal.BaseConcurrencyToken != report.ConcurrencyToken)
                throw new RuleViolation("conflict", "Proposal is stale; generate again for the current draft.");
            var template = await db.Templates.SingleOrDefaultAsync(x => x.Id == proposal.TemplateId && x.FacilityId == facility && x.Published, ct) ?? throw new RuleViolation("conflict", "The template was revoked.");
            using var sources = JsonDocument.Parse(proposal.SourcesJson);
            if (sources.RootElement[0].GetProperty("TemplateToken").GetGuid() != template.ConcurrencyToken)
                throw new RuleViolation("conflict", "The approved template changed.");
            var transcript = await db.Transcripts.SingleAsync(x => x.Id == proposal.TranscriptId && x.FacilityId == facility, ct);
            NewVersion(report, new(request.Examination, proposal.Findings, request.Impression, transcript.Language));
        }
        proposal.Status = request.Accept ? ProposalStatus.Accepted : ProposalStatus.Rejected; proposal.ReviewedBy = actor.UserId;
        Audit(facility, request.Accept ? "AIProposal.Accepted" : "AIProposal.Rejected", proposalId); await db.SaveChangesAsync(ct);
        return new { Report = report, Proposal = proposal };
    }
    public async Task<PageResult<AuditEvent>> AuditTrail(Guid facility, int page, int size, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator | ClinicRole.Auditor, ct);
        var result = await Paginate(db.AuditEvents.AsNoTracking().Where(x => x.FacilityId == facility).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id), page, size, ct);
        Audit(facility, "Audit.Viewed", facility); await db.SaveChangesAsync(ct); return result;
    }
    public async Task<object> Metrics(Guid facility, CancellationToken ct)
    {
        await Require(facility, ClinicRole.Administrator, ct);
        var counts = await db.Orders.Where(x => x.FacilityId == facility).GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        return new { OrdersByStatus = counts, PendingOutbox = await db.Outbox.CountAsync(x => x.FacilityId == facility && x.DispatchedAt == null, ct) };
    }
}
