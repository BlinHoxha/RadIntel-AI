using AIFramework.Core.Abstractions;
using AIFramework.Core.Models;
using Microsoft.EntityFrameworkCore;
using RadIntel.Application;

namespace RadIntel.Infrastructure;

// Application-owned durable retrieval over approved template versions. This initial
// adapter performs bounded lexical ranking, not embeddings/vector search.
public sealed class PublishedTemplateRetriever(RadIntelDbContext db, ICurrentActor actor) : IKnowledgeRetriever
{
    public async Task<IReadOnlyCollection<KnowledgeChunk>> SearchAsync(KnowledgeSearchQuery query, CancellationToken ct)
    {
        if (query.TenantId != actor.OrganizationId || query.TenantId == Guid.Empty || query.ScopeId is null ||
            query.Profile != "radiology-template" || query.DocumentIds is not { Count: > 0 } || query.DocumentIds.Count > 100)
            return [];
        if (!await db.Memberships.AnyAsync(x => x.FacilityId == query.ScopeId && x.UserId == actor.UserId && x.Active &&
            (x.Roles & (Domain.ClinicRole.Radiologist | Domain.ClinicRole.Administrator)) != 0, ct)) return [];
        var allowed = query.DocumentIds.ToArray();
        var documents = await db.Templates.AsNoTracking().Where(x => x.Published && x.FacilityId == query.ScopeId && allowed.Contains(x.Id))
            .Take(100).ToListAsync(ct);
        var words = query.Question.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return documents.Select(x => new KnowledgeChunk($"{x.Id}:v{x.Version}", x.Id, x.Version, x.Name, x.Structure,
            query.Profile, x.OrganizationId, x.FacilityId, x.Language, null,
            words.Count(w => x.Structure.Contains(w, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(x => x.Score).ThenBy(x => x.DocumentId).Take(Math.Clamp(query.MaxResults, 1, 20)).ToArray();
    }
}
