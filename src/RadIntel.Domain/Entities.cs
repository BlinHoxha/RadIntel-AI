using Framework.Domain.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace RadIntel.Domain;

[Flags]
public enum ClinicRole { None = 0, Administrator = 1, Scheduler = 2, Technician = 4, Radiologist = 8, Auditor = 16 }
public enum OrderStatus { Requested, Scheduled, CheckedIn, Acquired, InReview, ReportDraft, Signed, Cancelled }
public enum ReportStatus { Draft, PendingReview, Signed }
public enum ProposalStatus { PendingReview, Accepted, Rejected }

public sealed class RuleViolation(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class Rules
{
    public static string Text(string? value, string name, int max = 2000)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new RuleViolation("validation", $"{name} is required and must be at most {max} characters.");
        return value.Trim();
    }
    public static string Language(string language) => language is "sq" or "en" ? language
        : throw new RuleViolation("validation", "Language must be sq or en.");
    public static void Match(Guid actual, Guid expected)
    {
        if (actual != expected) throw new RuleViolation("conflict", "The record changed. Reload before retrying.");
    }
}

public abstract class TenantEntity : BaseEntity<Guid>
{
    public Guid OrganizationId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    protected TenantEntity() { Id = Guid.NewGuid(); }
}

public sealed class Organization : BaseEntity<Guid>
{
    public string Name { get; set; } = "";
}
public sealed class Facility : BaseEntity<Guid>
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string TimeZone { get; set; } = "Europe/Belgrade";
}
public sealed class Membership : TenantEntity
{
    public string UserId { get; set; } = "";
    public ClinicRole Roles { get; set; }
    public bool Active { get; set; } = true;
}
public sealed class Patient : TenantEntity
{
    public string Identifier { get; set; } = "";
    public string IdentifierIssuer { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
}
public sealed class ImagingOrder : TenantEntity
{
    public Guid PatientId { get; set; }
    public string Modality { get; set; } = "";
    public string BodyRegion { get; set; } = "";
    public string Referral { get; set; } = "";
    public string Priority { get; set; } = "Routine";
    public OrderStatus Status { get; private set; } = OrderStatus.Requested;
    public string? AssignedTo { get; private set; }
    public void Transition(OrderStatus next)
    {
        bool allowed = (Status, next) switch
        {
            (OrderStatus.Requested, OrderStatus.Scheduled or OrderStatus.CheckedIn or OrderStatus.Cancelled) => true,
            (OrderStatus.Scheduled, OrderStatus.CheckedIn or OrderStatus.Cancelled) => true,
            (OrderStatus.CheckedIn, OrderStatus.Acquired or OrderStatus.Cancelled) => true,
            (OrderStatus.Acquired, OrderStatus.InReview) => true,
            (OrderStatus.InReview, OrderStatus.ReportDraft) => true,
            (OrderStatus.ReportDraft, OrderStatus.Signed) => true,
            _ => false
        };
        if (!allowed) throw new RuleViolation("conflict", "This order transition is not permitted.");
        Status = next;
    }
    public void Claim(string userId)
    {
        if (AssignedTo is not null) throw new RuleViolation("conflict", "This study is already assigned.");
        Transition(OrderStatus.InReview);
        AssignedTo = userId;
    }
}
public sealed class Equipment : TenantEntity
{
    public string Name { get; set; } = "";
    public string Modality { get; set; } = "";
    public bool Available { get; set; } = true;
}
public sealed class Appointment : TenantEntity
{
    public Guid OrderId { get; set; }
    public Guid EquipmentId { get; set; }
    public long StartsAt { get; set; }
    public long EndsAt { get; set; }
    public bool Cancelled { get; set; }
}
public sealed class ReportTemplate : TenantEntity
{
    public string Name { get; set; } = "";
    public string Language { get; set; } = "en";
    public string Modality { get; set; } = "";
    public string BodyRegion { get; set; } = "";
    public string Structure { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool Published { get; set; }
}

public sealed record ReportContent(string Examination, string Findings, string Impression, string Language)
{
    public ReportContent Validate() => new(Rules.Text(Examination, "Examination", 500),
        Rules.Text(Findings, "Findings", 30000), Rules.Text(Impression, "Impression", 10000), Rules.Language(Language));
    // Length-prefixing avoids ambiguous concatenation when signing exact content.
    public string Digest() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Concat(new[] { Examination, Findings, Impression, Language }.Select(v => $"{v.Length}:{v}")))));
}
public sealed class Report : TenantEntity
{
    public Guid OrderId { get; set; }
    public Guid? CheckedTranscriptId { get; set; }
    public Guid? ParentReportId { get; set; }
    public string? AmendmentReason { get; set; }
    public int Kind { get; set; } // 0 original, 1 addendum
    public ReportStatus Status { get; private set; }
    public int CurrentVersion { get; private set; }
    public void AddVersion()
    {
        if (Status == ReportStatus.Signed) throw new RuleViolation("conflict", "Signed reports are immutable. Create an addendum.");
        CurrentVersion++;
        Status = ReportStatus.Draft;
    }
    public void Submit()
    {
        if (Status != ReportStatus.Draft || CurrentVersion == 0)
            throw new RuleViolation("conflict", "A completed draft is required before review.");
        Status = ReportStatus.PendingReview;
    }
    public void Sign()
    {
        if (Status != ReportStatus.PendingReview) throw new RuleViolation("conflict", "Submit the current version for review before signing.");
        Status = ReportStatus.Signed;
    }
}
public sealed class ReportVersion : TenantEntity
{
    public Guid ReportId { get; set; }
    public int Number { get; set; }
    public string Examination { get; set; } = "";
    public string Findings { get; set; } = "";
    public string Impression { get; set; } = "";
    public string Language { get; set; } = "en";
    public string AuthorId { get; set; } = "";
    public ReportContent Content() => new(Examination, Findings, Impression, Language);
}
public sealed class Signature : TenantEntity
{
    public Guid ReportVersionId { get; set; }
    public string SignedBy { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string RequestKey { get; set; } = "";
}
public sealed class DictationTranscript : TenantEntity
{
    public Guid ReportId { get; set; }
    public string OriginalText { get; set; } = "";
    public string CheckedText { get; set; } = "";
    public string Language { get; set; } = "en";
    public string CheckedBy { get; set; } = "";
}
public sealed class AIProposal : TenantEntity
{
    public Guid ReportId { get; set; }
    public Guid TranscriptId { get; set; }
    public Guid TemplateId { get; set; }
    public int BaseVersion { get; set; }
    public Guid BaseConcurrencyToken { get; set; }
    public string Findings { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public string PromptVersion { get; set; } = "report-v1";
    public string SchemaVersion { get; set; } = "1";
    public string SourcesJson { get; set; } = "[]";
    public ProposalStatus Status { get; set; }
    public string? ReviewedBy { get; set; }
}
public sealed class AuditEvent : TenantEntity
{
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid EntityId { get; set; }
    public string? Reason { get; set; }
}
public sealed class OutboxMessage : TenantEntity
{
    public Guid ReportVersionId { get; set; }
    public string EventType { get; set; } = "ReportSigned";
    public string Payload { get; set; } = "";
    public long? DispatchedAt { get; set; }
}
