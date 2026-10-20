using AIFramework.Core.Abstractions;
using AIFramework.Core.Models;
using RadIntel.Application;
using RadIntel.Domain;

namespace RadIntel.AI;

public sealed class RadiologyProfileCatalog : IDocumentProfileCatalog
{
    private static readonly DocumentProfile Template = new("radiology-template",
        "Published institutional reporting structure; never a source of patient findings.",
        ["ingest", "question-answering"], ["sq", "en"], true);
    public IReadOnlyCollection<DocumentProfile> GetAll() => [Template];
    public DocumentProfile GetRequired(string name) => name == Template.Name ? Template : throw new ArgumentException("Unknown radiology profile.");
}

public sealed class DisabledDraftGenerator : IReportDraftGenerator
{
    public Task<DraftGeneration> GenerateAsync(DictationTranscript transcript, ReportTemplate template,
        IReadOnlyCollection<KnowledgeChunk> evidence, CancellationToken cancellationToken) =>
        throw new FeatureUnavailableException("Draft assistance is disabled. Manual reporting remains available.");
}

// The first report adapter deliberately preserves the entire checked input verbatim.
// It is a functional, deterministic proposal workflow, not an LLM or diagnostic model.
public sealed class VerbatimDraftGenerator : IReportDraftGenerator
{
    public Task<DraftGeneration> GenerateAsync(DictationTranscript transcript, ReportTemplate template,
        IReadOnlyCollection<KnowledgeChunk> evidence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DraftGeneration(transcript.CheckedText, "verbatim-assistant", "1.0",
            evidence.Select(x => x.Id).ToArray()));
    }
}

public sealed class UnconfiguredSpeechTranscriber : ISpeechTranscriber
{
    public Task<string> TranscribeAsync(Stream audio, string contentType, string language, CancellationToken cancellationToken) =>
        throw new FeatureUnavailableException("No approved speech provider is configured. Enter and check the transcript manually.");
}
