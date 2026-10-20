# PACS study linkage and controlled DICOMweb reads

14 October 2026 | Integration increment

`RadIntel.Integrations.Dicom` implements bounded QIDO-RS verification and selected DICOMweb reads through product-owned ports. Query syntax follows the [DICOM Search Transaction](https://dicom.nema.org/medical/dicom/current/output/chtml/part18/sect_10.6.html). [OHIF configuration](https://docs.ohif.org/configuration/datasources/dicom-web/) is the reference for a future viewer adapter; this proxy is narrower than a complete viewer data source. PACS/OHIF are not installed or clinically validated by this increment.

## Behavior

A technician or assigned radiologist links a configured archive's StudyInstanceUID to an acquired order. Exactly one returned study must match the registered patient identifier and issuer. Names are never used. Missing issuers and ambiguous/mismatched results fail closed. Membership, identity and concurrency are rechecked after archive I/O. References are immutable and unique per tenant/archive/UID. Linkage is permitted before the original report is signed.

Each read resolves the stored study, checks permissions and archive patient identity, and reauthorizes after receiving the response. Clients cannot choose upstream URLs or different study UIDs. Successful linkage/list/read actions are audited. Credentials, upstream error bodies, cookies and redirect locations are not returned. Incorrect linkage needs a future reviewed correction workflow; generic update/delete is not exposed.

## Configuration

No archives are configured by default. Use trusted environment/secret configuration:

| Key | Purpose |
| --- | --- |
| `Pacs__Archives__0__Key` | Stable alias, at most 100 characters |
| `Pacs__Archives__0__Name` | Display name |
| `Pacs__Archives__0__OrganizationId` | Existing organization UUID |
| `Pacs__Archives__0__FacilityId` | Existing facility UUID |
| `Pacs__Archives__0__Endpoint` | HTTPS DICOMweb root, no user info/query/fragment |
| `Pacs__Archives__0__BearerToken` | Server-only archive credential; never the browser token |
| `Pacs__Archives__0__AllowAnonymousDevelopment` | Optional loopback archive only, rejected outside Development |

HTTP is accepted only for loopback in Development. TLS validation stays enabled. Redirects/cookies are disabled; duplicate facility aliases are rejected. Archive credentials must themselves be restricted to the clinic's data. Application matching does not replace archive-side isolation. Token rotation/workload identity and vendor-specific authentication remain further adapter work.

## API and limits

All routes require authenticated tenant membership and facility/resource permissions:

- `GET /api/v1/facilities/{facilityId}/archives` returns aliases/names only.
- `POST /api/v1/facilities/{facilityId}/imaging-orders/{orderId}/studies` accepts expectedToken, archiveKey and studyInstanceUid.
- `GET /api/v1/facilities/{facilityId}/imaging-orders/{orderId}/studies` lists at most 100 references.
- `GET /api/v1/facilities/{facilityId}/studies/{studyId}/dicomweb/{resource}` reads an allowed resource of the stored study.

Resources: metadata, series, instances, series/{seriesUid}/metadata, series/{seriesUid}/instances, series/{seriesUid}/instances/{instanceUid}/metadata, and series/{seriesUid}/instances/{instanceUid}/frames/{frameNumbers}. UIDs are validated; lists permit at most sixteen positive frame numbers. Arbitrary paths/URLs/queries, general study search, STOW uploads and bulk downloads are not forwarded.

Reads are buffered with a 16 MiB limit and 60-second total timeout, including the body. Four concurrent imaging actions per authenticated identity are permitted. Metadata must be DICOM JSON; frames must be multipart/related. Unexpected study IDs in metadata are rejected. BulkDataURI and RetrieveURL fields are removed recursively. These limits and URL removal require explicit viewer adaptation and vendor contract tests before OHIF integration.

The browser worklist offers Link PACS study and Linked studies actions. Authorized metadata can be downloaded. Diagnostic image viewing is not presented as available.

## Prepared schema and validation

PacsStudyLinks and deploy/sql/002_PacsStudyLinks.sql are review artifacts only; neither is applied. Existing seed status is unchanged. Tests simulate archive responses and use isolated in-memory SQLite schemas. They cover identity matching, changed archive identity, revoked/cross-tenant/assigned-user access, immutable/duplicate linkage, server credentials, traversal paths, response bounds, redirects, metadata URL removal and insecure configuration. No live PACS or patient study was contacted. Real archive conformance, networking, full viewer support and diagnostic acceptance remain environment-specific work.
