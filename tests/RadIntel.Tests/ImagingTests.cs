using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RadIntel.Application;
using RadIntel.Contracts;
using RadIntel.Domain;
using RadIntel.Infrastructure;
using RadIntel.Integrations.Dicom;

namespace RadIntel.Tests;

public sealed class ImagingTests
{
    private sealed class FakeArchive : IImagingArchive
    {
        public ArchiveStudy Study { get; set; } = new("1.2.3", "", "");
        public int VerifyCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public IReadOnlyList<ArchiveDescriptor> Available(Guid organizationId, Guid facilityId) => [new("test", "Test archive")];
        public Task<ArchiveStudy> VerifyAsync(Guid organizationId, Guid facilityId, string key, string uid, CancellationToken ct)
        { VerifyCalls++; return Task.FromResult(Study); }
        public Task<DicomPayload> ReadAsync(Guid organizationId, Guid facilityId, string key, string uid, string resource, CancellationToken ct)
        { ReadCalls++; return Task.FromResult(new DicomPayload(Encoding.UTF8.GetBytes("[]"), "application/dicom+json")); }
    }
    private static async Task<(ImagingOrder Order, FakeArchive Archive, ImagingService Service)> Setup(TestClinic clinic)
    {
        var order = await clinic.Order();
        await clinic.Service.Transition(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "CheckedIn", "Test"), default);
        await clinic.Service.Transition(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "Acquired", "Test"), default);
        var patient = await clinic.Db.Patients.SingleAsync(x => x.Id == order.PatientId);
        var archive = new FakeArchive { Study = new("1.2.3", patient.Identifier, patient.IdentifierIssuer) };
        return (order, archive, new ImagingService(clinic.Db, clinic.Service, clinic.Actor, archive));
    }
    [Fact]
    public async Task Linking_requires_matching_patient_identifier_and_issuer()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        archive.Study = archive.Study with { IdentifierIssuer = "wrong-issuer" };
        await Assert.ThrowsAsync<RuleViolation>(() => service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default));
        Assert.Empty(await clinic.Db.ImagingStudies.ToListAsync());
    }
    [Fact]
    public async Task Link_and_read_reverify_identity_and_audit_access()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        await service.Read(clinic.FacilityId, study.Id, "metadata", default);
        Assert.Equal(2, archive.VerifyCalls); Assert.Equal(1, archive.ReadCalls);
        Assert.Single(await clinic.Db.AuditEvents.Where(x => x.Action == "ImagingStudy.ResourceRead").ToListAsync());
    }
    [Fact]
    public async Task Changed_archive_patient_blocks_read_before_pixels_leave_archive()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        archive.Study = archive.Study with { PatientIdentifier = "different-patient" };
        await Assert.ThrowsAsync<RuleViolation>(() => service.Read(clinic.FacilityId, study.Id, "metadata", default));
        Assert.Equal(0, archive.ReadCalls);
    }
    [Fact]
    public async Task Revoked_membership_blocks_read_without_archive_call()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        var member = await clinic.Db.Memberships.SingleAsync(); member.Active = false; await clinic.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.Read(clinic.FacilityId, study.Id, "metadata", default));
        Assert.Equal(1, archive.VerifyCalls); Assert.Equal(0, archive.ReadCalls);
    }
    [Fact]
    public async Task Cross_tenant_read_is_denied_before_archive_io()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        clinic.Actor.OrganizationId = Guid.NewGuid();
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.Read(clinic.FacilityId, study.Id, "metadata", default));
        Assert.Equal(1, archive.VerifyCalls);
    }
    [Fact]
    public async Task Unassigned_radiologist_cannot_read_another_study()
    {
        await using var clinic = await TestClinic.Create(); var (order, archive, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        await clinic.Service.SetMembership(clinic.FacilityId, new("another-rad", (int)ClinicRole.Radiologist, true), default);
        clinic.Actor.UserId = "another-rad";
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.Read(clinic.FacilityId, study.Id, "metadata", default));
        Assert.Equal(1, archive.VerifyCalls);
    }
    [Fact]
    public async Task Study_reference_is_immutable_and_duplicate_archive_uid_is_rejected()
    {
        await using var clinic = await TestClinic.Create(); var (order, _, service) = await Setup(clinic);
        var study = await service.Link(clinic.FacilityId, order.Id, new(order.ConcurrencyToken, "test", "1.2.3"), default);
        study.StudyInstanceUid = "1.2.4";
        await Assert.ThrowsAsync<RuleViolation>(() => clinic.Db.SaveChangesAsync());
        clinic.Db.ChangeTracker.Clear();
        var current = await clinic.Db.Orders.SingleAsync(x => x.Id == order.Id);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.Link(clinic.FacilityId, current.Id, new(current.ConcurrencyToken, "test", "1.2.3"), default));
    }
}

public sealed class DicomWebArchiveTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response(request));
    }
    private static readonly Guid Tenant = Guid.NewGuid(), Facility = Guid.NewGuid();
    private static readonly ArchiveConfiguration Configuration = new("test", "Archive", Tenant, Facility, new Uri("https://pacs.test/dicom-web"), "test-server-secret");
    private static DicomWebArchive Adapter(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new HttpClient(new Handler(response)), [Configuration]);
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/dicom+json") };
    [Theory]
    [InlineData("../metadata")]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    [InlineData("series/1.2.3/instances/1.2.4/frames/0")]
    [InlineData("series/1.2.3/instances/1.2.4/frames/1?url=evil")]
    [InlineData("series/1.2.03/metadata")]
    public void Unsupported_resource_paths_fail_closed(string path) => Assert.Throws<RuleViolation>(() => DicomWebArchive.Resource(path));
    [Theory]
    [InlineData("metadata")]
    [InlineData("series")]
    [InlineData("series/1.2.3/instances/1.2.4/frames/1,2")]
    public void Supported_resources_remain_relative_and_bounded(string path) => Assert.Equal(path, DicomWebArchive.Resource(path));
    [Fact]
    public async Task Qido_verifies_unique_study_and_uses_only_server_credentials()
    {
        var adapter = Adapter(request =>
        {
            Assert.Equal("pacs.test", request.RequestUri!.Host);
            Assert.Equal("test-server-secret", request.Headers.Authorization!.Parameter);
            Assert.Contains("StudyInstanceUID=1.2.3", request.RequestUri.Query);
            return Json("[{\"0020000D\":{\"Value\":[\"1.2.3\"]},\"00100020\":{\"Value\":[\"patient\"]},\"00100021\":{\"Value\":[\"issuer\"]}}]");
        });
        var result = await adapter.VerifyAsync(Tenant, Facility, "test", "1.2.3", default);
        Assert.Equal("patient", result.PatientIdentifier); Assert.Equal("issuer", result.IdentifierIssuer);
        Assert.DoesNotContain("secret", result.ToString());
    }
    [Fact]
    public async Task Missing_issuer_and_ambiguous_studies_are_rejected()
    {
        var missing = Adapter(_ => Json("[{\"0020000D\":{\"Value\":[\"1.2.3\"]},\"00100020\":{\"Value\":[\"patient\"]}}]"));
        await Assert.ThrowsAsync<RuleViolation>(() => missing.VerifyAsync(Tenant, Facility, "test", "1.2.3", default));
        await Assert.ThrowsAsync<RuleViolation>(() => Adapter(_ => Json("[{},{}]")).VerifyAsync(Tenant, Facility, "test", "1.2.3", default));
    }
    [Fact]
    public async Task Metadata_strips_bulk_data_and_archive_retrieval_urls()
    {
        var adapter = Adapter(_ => Json("[{\"0020000D\":{\"Value\":[\"1.2.3\"]},\"00081190\":{\"Value\":[\"https://private.test\"]},\"7FE00010\":{\"BulkDataURI\":\"https://private.test/pixels\"}}]"));
        var result = await adapter.ReadAsync(Tenant, Facility, "test", "1.2.3", "metadata", default);
        var json = Encoding.UTF8.GetString(result.Content);
        Assert.DoesNotContain("private.test", json); Assert.DoesNotContain("BulkDataURI", json);
    }
    [Fact]
    public async Task Wrong_study_metadata_and_oversized_responses_are_rejected()
    {
        await Assert.ThrowsAsync<FeatureUnavailableException>(() => Adapter(_ => Json("[{\"0020000D\":{\"Value\":[\"9.9.9\"]}}]")).ReadAsync(Tenant, Facility, "test", "1.2.3", "metadata", default));
        await Assert.ThrowsAsync<FeatureUnavailableException>(() => Adapter(_ => Json("[{\"0020000D\":{\"Value\":\"invalid\"}}]")).ReadAsync(Tenant, Facility, "test", "1.2.3", "metadata", default));
        var tooLarge = Adapter(_ => { var response = Json("[]"); response.Content.Headers.ContentLength = DicomWebArchive.MaximumResponseBytes + 1; return response; });
        await Assert.ThrowsAsync<FeatureUnavailableException>(() => tooLarge.ReadAsync(Tenant, Facility, "test", "1.2.3", "metadata", default));
    }
    [Fact]
    public async Task Redirect_and_unconfigured_scope_never_expose_upstream_error_bodies()
    {
        var adapter = Adapter(_ => new(HttpStatusCode.Redirect) { Content = new StringContent("secret patient data") });
        var failure = await Assert.ThrowsAsync<FeatureUnavailableException>(() => adapter.VerifyAsync(Tenant, Facility, "test", "1.2.3", default));
        Assert.DoesNotContain("secret patient", failure.Message);
        await Assert.ThrowsAsync<FeatureUnavailableException>(() => adapter.VerifyAsync(Guid.NewGuid(), Facility, "test", "1.2.3", default));
    }
    [Fact]
    public void Insecure_or_cross_origin_anonymous_configuration_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => DicomWebArchive.ValidateConfiguration(Configuration with { Endpoint = new Uri("http://pacs.test/") }, false));
        Assert.Throws<InvalidOperationException>(() => DicomWebArchive.ValidateConfiguration(Configuration with { BearerToken = null, AllowAnonymousDevelopment = true }, false));
        Assert.Throws<InvalidOperationException>(() => DicomWebArchive.ValidateConfiguration(Configuration with { BearerToken = null, AllowAnonymousDevelopment = true }, true));
        DicomWebArchive.ValidateConfiguration(Configuration with { Endpoint = new Uri("http://localhost:8042/dicom-web/"), BearerToken = null, AllowAnonymousDevelopment = true }, true);
    }
}
