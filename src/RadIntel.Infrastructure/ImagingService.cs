using Microsoft.EntityFrameworkCore;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;

namespace RadIntel.Infrastructure;

public sealed class ImagingService(RadIntelDbContext db, WorkflowService workflow, ICurrentActor actor, IImagingArchive archive)
{
    private async Task<ImagingOrder> Access(Guid facility, Guid orderId, CancellationToken ct)
    {
        var membership = await workflow.Require(facility, ClinicRole.Technician | ClinicRole.Radiologist, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId && x.FacilityId == facility, ct) ?? throw new MissingResourceException();
        if ((membership.Roles & ClinicRole.Technician) == 0 && order.AssignedTo != actor.UserId) throw new AccessDeniedException();
        return order;
    }
    private void Audit(Guid facility, string action, Guid id) => db.AuditEvents.Add(new AuditEvent
    { OrganizationId = actor.OrganizationId, FacilityId = facility, ActorId = actor.UserId, Action = action, EntityId = id });
    private async Task CheckPatient(ImagingOrder order, ArchiveStudy returned, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().SingleAsync(x => x.Id == order.PatientId && x.FacilityId == order.FacilityId, ct);
        if (!string.Equals(returned.PatientIdentifier, patient.Identifier, StringComparison.Ordinal) ||
            !string.Equals(returned.IdentifierIssuer, patient.IdentifierIssuer, StringComparison.Ordinal))
            throw new RuleViolation("validation", "The PACS patient identifier and issuer do not match the registered patient.");
    }
    public async Task<IReadOnlyList<ArchiveDescriptor>> Archives(Guid facility, CancellationToken ct)
    {
        await workflow.Require(facility, ClinicRole.Technician | ClinicRole.Radiologist, ct);
        return archive.Available(actor.OrganizationId, facility);
    }
    public async Task<ImagingStudy> Link(Guid facility, Guid orderId, LinkStudyRequest request, CancellationToken ct)
    {
        var order = await Access(facility, orderId, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        if (order.Status is not (OrderStatus.Acquired or OrderStatus.InReview or OrderStatus.ReportDraft))
            throw new RuleViolation("conflict", "Studies may be linked after acquisition and before the original report is signed.");
        var uid = DicomIdentifiers.Uid(request.StudyInstanceUid);
        var key = Rules.Text(request.ArchiveKey, "Archive key", 100);
        var verified = await archive.VerifyAsync(actor.OrganizationId, facility, key, uid, ct);
        if (verified.StudyInstanceUid != uid) throw new RuleViolation("validation", "The archive study identity changed.");
        await CheckPatient(order, verified, ct);
        // Refresh tracked state and permissions after archive I/O before persisting.
        db.ChangeTracker.Clear(); order = await Access(facility, orderId, ct); Rules.Match(order.ConcurrencyToken, request.ExpectedToken);
        await CheckPatient(order, verified, ct);
        var study = new ImagingStudy { OrganizationId = actor.OrganizationId, FacilityId = facility, OrderId = order.Id,
            ArchiveKey = key, StudyInstanceUid = uid, VerifiedBy = actor.UserId };
        order.ConcurrencyToken = Guid.NewGuid(); // Races with acquisition, signing and another linkage must conflict.
        db.ImagingStudies.Add(study); Audit(facility, "ImagingStudy.Linked", study.Id); await db.SaveChangesAsync(ct);
        return study;
    }
    public async Task<IReadOnlyList<ImagingStudy>> List(Guid facility, Guid orderId, CancellationToken ct)
    {
        await Access(facility, orderId, ct);
        var studies = await db.ImagingStudies.AsNoTracking().Where(x => x.FacilityId == facility && x.OrderId == orderId).OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        Audit(facility, "ImagingStudy.ListViewed", orderId); await db.SaveChangesAsync(ct); return studies;
    }
    public async Task<DicomPayload> Read(Guid facility, Guid studyId, string resource, CancellationToken ct)
    {
        // Resolve the persisted study before contacting an archive; callers never select an upstream UID or URL.
        await workflow.Require(facility, ClinicRole.Technician | ClinicRole.Radiologist, ct);
        var study = await db.ImagingStudies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == studyId && x.FacilityId == facility, ct) ?? throw new MissingResourceException();
        var order = await Access(facility, study.OrderId, ct);
        var verified = await archive.VerifyAsync(actor.OrganizationId, facility, study.ArchiveKey, study.StudyInstanceUid, ct);
        if (verified.StudyInstanceUid != study.StudyInstanceUid) throw new RuleViolation("validation", "The archive study identity changed.");
        await CheckPatient(order, verified, ct);
        var payload = await archive.ReadAsync(actor.OrganizationId, facility, study.ArchiveKey, study.StudyInstanceUid, resource, ct);
        db.ChangeTracker.Clear(); order = await Access(facility, study.OrderId, ct);
        await CheckPatient(order, verified, ct);
        Audit(facility, "ImagingStudy.ResourceRead", studyId); await db.SaveChangesAsync(ct);
        return payload;
    }
}
