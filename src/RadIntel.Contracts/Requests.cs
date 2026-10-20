using System.ComponentModel.DataAnnotations;

namespace RadIntel.Contracts;

public sealed record OnboardRequest([Required, StringLength(200)] string OrganizationName,
    [Required, StringLength(200)] string FacilityName, [Required, StringLength(100)] string TimeZone);
public sealed record MembershipRequest([Required, StringLength(200)] string UserId, int Roles, bool Active);
public sealed record PatientRequest([Required, StringLength(100)] string Identifier,
    [Required, StringLength(100)] string IdentifierIssuer, [Required, StringLength(200)] string DisplayName, DateOnly? BirthDate);
public sealed record OrderRequest(Guid PatientId, [Required, StringLength(20)] string Modality,
    [Required, StringLength(100)] string BodyRegion, [StringLength(4000)] string Referral, [Required] string Priority = "Routine");
public sealed record TransitionRequest(Guid ExpectedToken, string Status, [Required, StringLength(500)] string Reason);
public sealed record VersionRequest(Guid ExpectedToken, [Required, StringLength(500)] string Examination,
    [Required, StringLength(30000)] string Findings, [Required, StringLength(10000)] string Impression, string Language);
public sealed record ConcurrencyRequest(Guid ExpectedToken);
public sealed record SignRequest(Guid ExpectedToken, [Required, StringLength(100)] string RequestKey);
public sealed record AddendumRequest(Guid ExpectedToken, [Required, StringLength(1000)] string Reason);
public sealed record EquipmentRequest([Required, StringLength(200)] string Name, [Required, StringLength(20)] string Modality);
public sealed record ScheduleRequest(Guid ExpectedToken, Guid EquipmentId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record TemplateRequest([Required, StringLength(200)] string Name, string Language,
    [Required, StringLength(20)] string Modality, [Required, StringLength(100)] string BodyRegion,
    [Required, StringLength(10000)] string Structure, int Version);
public sealed record TranscriptRequest([Required, StringLength(30000)] string OriginalText,
    [Required, StringLength(30000)] string CheckedText, string Language, bool ConfirmedByRadiologist);
public sealed record ProposalRequest(Guid ExpectedToken, Guid TranscriptId, Guid TemplateId);
public sealed record ReviewProposalRequest(Guid ExpectedToken, bool Accept, [Required, StringLength(500)] string Examination,
    [Required, StringLength(10000)] string Impression);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record LinkStudyRequest(Guid ExpectedToken, [Required, StringLength(100)] string ArchiveKey,
    [Required, StringLength(64)] string StudyInstanceUid);
