using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using RadIntel.Application;
using RadIntel.Domain;

namespace RadIntel.Integrations.Dicom;

public sealed record ArchiveConfiguration(string Key, string Name, Guid OrganizationId, Guid FacilityId,
    Uri Endpoint, string? BearerToken, bool AllowAnonymousDevelopment = false);

public sealed class DicomWebArchive(HttpClient client, IReadOnlyList<ArchiveConfiguration> archives) : IImagingArchive
{
    public const int MaximumResponseBytes = 16 * 1024 * 1024;
    public IReadOnlyList<ArchiveDescriptor> Available(Guid organizationId, Guid facilityId) => archives
        .Where(x => x.OrganizationId == organizationId && x.FacilityId == facilityId)
        .Select(x => new ArchiveDescriptor(x.Key, x.Name)).ToArray();

    public static void ValidateConfiguration(ArchiveConfiguration archive, bool development)
    {
        if (string.IsNullOrWhiteSpace(archive.Key) || archive.Key.Length > 100 || archive.OrganizationId == Guid.Empty || archive.FacilityId == Guid.Empty ||
            !archive.Endpoint.IsAbsoluteUri || !string.IsNullOrEmpty(archive.Endpoint.UserInfo) || !string.IsNullOrEmpty(archive.Endpoint.Query) || !string.IsNullOrEmpty(archive.Endpoint.Fragment) ||
            (archive.Endpoint.Scheme != "https" && !(development && archive.Endpoint.Scheme == "http" && archive.Endpoint.IsLoopback)) ||
            (archive.AllowAnonymousDevelopment && (!development || !archive.Endpoint.IsLoopback)))
            throw new InvalidOperationException("PACS archive configuration is invalid or insecure.");
        if (string.IsNullOrWhiteSpace(archive.BearerToken) && !archive.AllowAnonymousDevelopment)
            throw new InvalidOperationException("PACS requires a server-side bearer credential or explicitly anonymous local development.");
    }

    private ArchiveConfiguration Configuration(Guid organization, Guid facility, string key) => archives
        .SingleOrDefault(x => x.Key == key && x.OrganizationId == organization && x.FacilityId == facility)
        ?? throw new FeatureUnavailableException("No archive is configured for this clinic and facility.");

    public async Task<ArchiveStudy> VerifyAsync(Guid organization, Guid facility, string key, string studyUid, CancellationToken ct)
    {
        DicomIdentifiers.Uid(studyUid);
        var payload = await Send(Configuration(organization, facility, key),
            $"studies?StudyInstanceUID={Uri.EscapeDataString(studyUid)}&includefield=00100020&includefield=00100021&limit=2",
            "application/dicom+json", ct);
        try
        {
            using var document = JsonDocument.Parse(payload.Content, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 1)
                throw new RuleViolation("validation", "The archive must return exactly one matching study.");
            var data = document.RootElement[0];
            if (data.ValueKind != JsonValueKind.Object) throw new FeatureUnavailableException("The archive returned invalid study metadata.");
            string Required(string tag)
            {
                if (!data.TryGetProperty(tag, out var attribute) || attribute.ValueKind != JsonValueKind.Object || !attribute.TryGetProperty("Value", out var values) ||
                    values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != 1 || values[0].ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(values[0].GetString()))
                    throw new RuleViolation("validation", "The archive did not supply a verified study UID, patient identifier and issuer.");
                return values[0].GetString()!.TrimEnd(' ', '\0');
            }
            var result = new ArchiveStudy(Required("0020000D"), Required("00100020"), Required("00100021"));
            if (result.StudyInstanceUid != studyUid) throw new RuleViolation("validation", "Archive study identity does not match the request.");
            return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { throw new FeatureUnavailableException("The archive returned invalid DICOM JSON."); }
    }

    // Deliberately narrow read surface; never follow BulkDataURI or caller-supplied URLs.
    public static string Resource(string resource)
    {
        if (resource is "metadata" or "series" or "instances") return resource;
        var parts = resource.Split('/');
        if (parts.Length is 3 or 5 or 6 && parts[0] == "series")
        {
            DicomIdentifiers.Uid(parts[1]);
            if (parts.Length == 3 && parts[2] is "metadata" or "instances") return resource;
            if (parts.Length >= 5 && parts[2] == "instances")
            {
                DicomIdentifiers.Uid(parts[3]);
                if (parts.Length == 5 && parts[4] == "metadata") return resource;
                if (parts.Length == 6 && parts[4] == "frames")
                {
                    var frames = parts[5].Split(',');
                    if (frames.Length is > 0 and <= 16 && frames.All(x => int.TryParse(x, out var n) && n is > 0 and <= 100000 && x == n.ToString())) return resource;
                }
            }
        }
        throw new RuleViolation("validation", "Unsupported DICOMweb resource. Only scoped metadata, series, instances and bounded frames are available.");
    }

    public async Task<DicomPayload> ReadAsync(Guid organization, Guid facility, string key, string studyUid, string resource, CancellationToken ct)
    {
        DicomIdentifiers.Uid(studyUid); resource = Resource(resource);
        var payload = await Send(Configuration(organization, facility, key), $"studies/{studyUid}/{resource}",
            resource.Contains("/frames/", StringComparison.Ordinal) ? "multipart/related; type=\"application/octet-stream\"" : "application/dicom+json", ct);
        if (resource.Contains("/frames/", StringComparison.Ordinal)) return payload;
        try
        {
            var node = JsonNode.Parse(payload.Content, documentOptions: new JsonDocumentOptions { MaxDepth = 32 });
            if (node is not JsonArray array) throw new FeatureUnavailableException("The archive returned invalid DICOM JSON.");
            foreach (var item in array)
            {
                if (item is not JsonObject obj) throw new FeatureUnavailableException("The archive returned invalid DICOM metadata.");
                if (obj.TryGetPropertyValue("0020000D", out var studyAttribute) &&
                    (studyAttribute is not JsonObject uidTag || uidTag["Value"] is not JsonArray values ||
                     values.Count != 1 || values[0]?.GetValue<string>() != studyUid))
                    throw new FeatureUnavailableException("The archive returned metadata for an unexpected study.");
            }
            void Strip(JsonNode? current)
            {
                if (current is JsonObject obj)
                {
                    obj.Remove("BulkDataURI"); obj.Remove("00081190"); // Never expose archive retrieval URLs to the client.
                    foreach (var pair in obj.ToArray()) Strip(pair.Value);
                }
                else if (current is JsonArray list) foreach (var item in list) Strip(item);
            }
            Strip(node);
            return new(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()), payload.ContentType);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { throw new FeatureUnavailableException("The archive returned invalid DICOM JSON."); }
    }

    private async Task<DicomPayload> Send(ArchiveConfiguration archive, string resource, string accept, CancellationToken ct)
    {
        var root = new Uri(archive.Endpoint.AbsoluteUri.TrimEnd('/') + "/");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, resource));
        request.Headers.Accept.ParseAdd(accept);
        if (!string.IsNullOrWhiteSpace(archive.BearerToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", archive.BearerToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new FeatureUnavailableException("The archive request failed. No upstream response body is exposed.");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) throw new FeatureUnavailableException("The archive response exceeds the configured read limit.");
            var media = response.Content.Headers.ContentType;
            if (media is null || (accept.StartsWith("application/dicom+json", StringComparison.Ordinal)
                ? media.MediaType != "application/dicom+json" : media.MediaType != "multipart/related"))
                throw new FeatureUnavailableException("The archive response content type is unsupported.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream(); var bytes = new byte[65536];
            int read;
            while ((read = await stream.ReadAsync(bytes, timeout.Token)) > 0)
            {
                if (buffer.Length + read > MaximumResponseBytes) throw new FeatureUnavailableException("The archive response exceeds the configured read limit.");
                await buffer.WriteAsync(bytes.AsMemory(0, read), timeout.Token);
            }
            return new(buffer.ToArray(), media.ToString());
        }
        catch (HttpRequestException) { throw new FeatureUnavailableException("The archive could not be reached."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new FeatureUnavailableException("The archive read timed out."); }
    }
}
