using AIFramework.Core.Models;
using RadIntel.Domain;

namespace RadIntel.Application;

public interface ICurrentActor
{
    Guid OrganizationId { get; }
    string UserId { get; }
    bool CanProvision { get; }
    bool HasRecentMfa { get; }
}
public sealed record DraftGeneration(string Findings, string Provider, string ModelVersion,
    IReadOnlyList<string> SourceIds);
public interface IReportDraftGenerator
{
    Task<DraftGeneration> GenerateAsync(DictationTranscript transcript, ReportTemplate template,
        IReadOnlyCollection<KnowledgeChunk> evidence, CancellationToken cancellationToken);
}
public interface ISpeechTranscriber
{
    Task<string> TranscribeAsync(Stream audio, string contentType, string language, CancellationToken cancellationToken);
}
public sealed class AccessDeniedException : Exception;
public sealed class MissingResourceException : Exception;
public sealed class FeatureUnavailableException(string message) : Exception(message);
