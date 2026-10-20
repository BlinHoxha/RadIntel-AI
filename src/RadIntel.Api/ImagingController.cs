using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;
using RadIntel.Infrastructure;

namespace RadIntel.Api;

[ApiController]
[Route("api/v1/facilities/{facilityId:guid}")]
[EnableRateLimiting("imaging")]
public sealed class ImagingController(ImagingService imaging) : ControllerBase
{
    [HttpGet("archives")]
    public Task<IReadOnlyList<ArchiveDescriptor>> Archives(Guid facilityId, CancellationToken ct) => imaging.Archives(facilityId, ct);
    [HttpPost("imaging-orders/{orderId:guid}/studies")]
    public async Task<IActionResult> Link(Guid facilityId, Guid orderId, LinkStudyRequest request, CancellationToken ct) =>
        StatusCode(201, await imaging.Link(facilityId, orderId, request, ct));
    [HttpGet("imaging-orders/{orderId:guid}/studies")]
    public Task<IReadOnlyList<ImagingStudy>> List(Guid facilityId, Guid orderId, CancellationToken ct) => imaging.List(facilityId, orderId, ct);
    [HttpGet("studies/{studyId:guid}/dicomweb/{**resource}")]
    public async Task<IActionResult> Read(Guid facilityId, Guid studyId, string resource, CancellationToken ct)
    {
        var response = await imaging.Read(facilityId, studyId, resource, ct);
        return File(response.Content, response.ContentType);
    }
}
