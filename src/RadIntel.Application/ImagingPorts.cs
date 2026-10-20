namespace RadIntel.Application;

public sealed record ArchiveDescriptor(string Key, string Name);
public sealed record ArchiveStudy(string StudyInstanceUid, string PatientIdentifier, string IdentifierIssuer);
public sealed record DicomPayload(byte[] Content, string ContentType);
public interface IImagingArchive
{
    IReadOnlyList<ArchiveDescriptor> Available(Guid organizationId, Guid facilityId);
    Task<ArchiveStudy> VerifyAsync(Guid organizationId, Guid facilityId, string archiveKey, string studyUid, CancellationToken ct);
    Task<DicomPayload> ReadAsync(Guid organizationId, Guid facilityId, string archiveKey, string studyUid, string resource, CancellationToken ct);
}
