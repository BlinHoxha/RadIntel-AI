using System.Text.RegularExpressions;

namespace RadIntel.Domain;

public static partial class DicomIdentifiers
{
    [GeneratedRegex(@"^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*))+$", RegexOptions.CultureInvariant, 100)]
    private static partial Regex UidPattern();
    public static string Uid(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64 || !UidPattern().IsMatch(value))
            throw new RuleViolation("validation", "A valid DICOM UID of at most 64 characters is required.");
        return value;
    }
}
public sealed class ImagingStudy : TenantEntity
{
    public Guid OrderId { get; set; }
    public string ArchiveKey { get; set; } = "";
    public string StudyInstanceUid { get; set; } = "";
    public string VerifiedBy { get; set; } = "";
}
