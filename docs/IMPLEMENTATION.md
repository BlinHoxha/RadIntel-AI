# Implementation record

14 October 2026 | First executable workflow foundation

## Scope and boundaries

Eight projects are present in `RadIntel-AI.sln`: Domain, Contracts, Application, Infrastructure, AI, Api, Integrations.Dicom and Tests. RadIntel directly consumes Framework.Domain, Framework.Contracts and Framework.Infrastructure plus AIFramework.Core, Application, Local and Azure. Neither shared reference host is used. No shared source, package policy or infrastructure template was edited.

The Domain owns clinical transitions; Application exposes identity/drafting/speech ports; Infrastructure supplies the product EF context and scoped workflow services; AI contains product profiles and draft adapters; Api composes authentication/providers and serves the initial browser workspace. The initial workspace uses native HTML/CSS/JavaScript so it needs no separate frontend toolchain. The planned React/TypeScript frontend remains a later client implementation over these API contracts.

## Working behavior

| Capability | Current implementation |
| --- | --- |
| Onboarding | Explicit authorized creation of organization, first facility and initial owner membership; no startup records |
| Identity | Configurable HTTPS JWT authority/audience; valid tenant_id/sub required; optional loopback-only Development transport with private configured token |
| Permissions | Authoritative active Membership lookup on each use case; role and facility checks; assigned-radiologist access for clinical report actions |
| Persistence | Product RadIntelDbContext, PostgreSQL provider, tenant query filters, same-tenant/facility foreign keys and optimistic concurrency |
| Patients | Identifier/issuer uniqueness within facility, registration, authorized paginated list; no automated merge |
| Orders/worklist | Staff-created modality/body-region/priority, check-in/acquisition/cancellation, atomic optimistic claim and paginated status filtering |
| Scheduling | Equipment registration, modality/availability checks, overlap rejection, resource concurrency and transactional rescheduling |
| Reports | Explicit creation after claim, append-only revisions, review submission, exact-version signature with SHA256 digest and recent MFA claim, immutable signed original, linked addenda |
| Idempotency | Signature request-key replay returns the existing signature; a reused key cannot sign another report; other create commands are not generally idempotent yet |
| Export | Authorized assigned-radiologist download of signed text only; read/export audit; PDF/portal distribution remains later work |
| Templates | Immutable version creation; publication/revocation requires both administrator and radiologist roles; unpublished definitions never supply retrieval evidence |
| Checked input | Original/checked transcript stored immutably after explicit radiologist confirmation; a new transcript invalidates prior proposals |
| Draft proposals | Disabled by default; optional Verbatim adapter copies checked findings exactly, records input/template/model/schema provenance, rejects altered findings and stale/revoked sources; accepting creates an unsigned draft |
| Institutional Q&A | Existing AI-Framework orchestration with explicit published document allowlists, scoped durable template retriever and Local deterministic or configured Azure chat |
| Audit/outbox | Protected append-only audit records and signed-report event intent stored atomically; no delivery dispatcher is implemented |
| Browser workspace | Connect/session/facility, patient/order/worklist, equipment scheduling, manual report editor, signature/addendum/export, templates, checked input and proposal review |
| Operations | Liveness, database/migration readiness probe, request limits, per-identity rate limiter, no-store clinical responses and redacted error logging |

Onboarding intentionally grants the first owner administrator/scheduler/technician/radiologist roles as an explicit administrative action; ordinary token/header role assertions do not grant these roles. Only a token carrying `radintel_provision=true` can onboard an organization whose ID matches its `tenant_id`. Configure issuance of that claim in the identity system; it is not public registration.

Signing requires `amr=mfa` and an `auth_time` within the previous ten minutes. The identity provider must issue those claims appropriately. Local MFA simulation is an explicit optional Development setting and is rejected outside Development. A stored digest and authenticated signer are not a legally certified digital-signature system.

## Database preparation only

The initial PostgreSQL migration, designer and model snapshot are under `src/RadIntel.Infrastructure/Migrations`. `deploy/sql/001_InitialRadiology.sql` is an idempotent schema script generated offline with EF tooling. It includes EF migration-history bookkeeping, schema/constraints/indexes, and no product seed inserts. It was not executed.

Runtime startup never calls `Migrate`, `EnsureCreated`, `EnsureDeleted`, seed routines or database creation. There is no automatic migration/seed deployment job. The schema must be applied later as a separately authorized administrative action. Runtime credentials should have application permissions and no schema-management privileges.

Migration/tooling generation can be repeated without applying schema:

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent --project src/RadIntel.Infrastructure --startup-project src/RadIntel.Infrastructure --configuration Release --output deploy/sql/001_InitialRadiology.sql
```

The design-time factory has a nonfunctional design-only connection string and does not open a connection for model/script generation. Real credentials belong in environment/secrets configuration, never migrations, seed JSON or committed settings.

Prepared seed JSON contains four English/Albanian reporting structures. No patient findings, accounts or clinic records are included. The optional importer previews by default and uses authorized API calls only with a future explicit `-Apply`; it never applies migrations or publishes templates. It has not been run.

## API contracts

Clinical routes are explicitly facility-scoped beneath `/api/v1/facilities/{facilityId}`. This makes the authorized context visible; the server still checks JWT tenant and database membership. Key suffixes are:

- `patients` — GET/POST
- `imaging-orders` — POST; `worklist` — GET
- `imaging-orders/{id}/transition`, `/claim`, `/schedule`, `/reports` — explicit workflow commands; reports also supports GET
- `reports/{id}` — GET; `/drafts`, `/submit`, `/sign`, `/addenda` — POST; `/export` — signed-only GET
- `templates` — GET/POST; `templates/{id}/publish` and `/revoke` — POST
- `reports/{id}/transcripts`, `/ai-draft`, `/proposals/{proposalId}/review` — POST
- `memberships` — PUT; `equipment` — GET/POST; `audit`, `metrics` — GET
- `knowledge/answers` — POST with question and explicit approved document IDs

`GET /api/v1/session` returns authorized facilities. `POST /api/v1/organizations/onboard` is the protected provisioning action. Development OpenAPI is at `/swagger`; clinical commands require authentication. Concurrency-protected commands carry `expectedToken`; clients reload after 409. Authorization failures return 403, unavailable scoped IDs return 404, invalid requests 400 and disabled providers 503, using redacted Problem Details.

## Provider setup

`AI:DraftProvider` accepts Disabled or Verbatim. Verbatim is a deterministic checked-text workflow, not a language model. There is no approved clinical LLM generator or speech adapter. Enter/check transcripts manually. The speech port throws an explicit unavailable-feature error until a real adapter is added.

`AI:KnowledgeProvider` accepts Local or Azure. Local uses the existing extractive chat model. Azure uses `AddAzureModels` and the five input pieces relevant to model configuration: OpenAI endpoint, chat/embedding deployment names, workload credential strategy, and deployment permissions. Configure `AI__Azure__OpenAiEndpoint`, `AI__Azure__ChatDeployment`, `AI__Azure__EmbeddingDeployment` and the environment's managed/workload identity. Azure Search is not selected by this first implementation: the product retriever reads published templates from PostgreSQL and ranks them lexically. No patient text is ingested into the knowledge corpus by these endpoints.

Cloud calls, cost, model quality, approved data use and actual identity/network configuration have not been verified. Provider configuration must not silently convert an extractive demo into a claimed clinical AI feature.

## Validation and remaining work

The solution builds and 47 automated tests pass, including PACS isolation, identity matching and transport-boundary tests described in [imaging documentation](IMAGING.md). Tests cover tenant/facility leakage, authoritative roles, relational identity constraints, scheduling/rescheduling, optimistic claims, version/audit immutability, explicit MFA/review signing, signature replay/outbox, addenda, assigned-radiologist access, checked-input proposals, revoked/stale evidence, fail-closed retrieval, HTTP authentication/model validation and PostgreSQL model/snapshot/script consistency.

Persistence tests use isolated in-memory SQLite schemas and test-only synthetic fixtures. They do not run migrations against any PostgreSQL/application database. PostgreSQL schema generation is validated offline; live PostgreSQL locking/constraint behavior still requires a disposable integration environment after schema application is separately authorized. Browser JavaScript passes syntax validation; a full browser usability/accessibility run is still required.

Remaining roadmap items: additional-facility administration, richer patient grants/verified merges, advanced scheduling/availability, OHIF/full viewer integration, FHIR, approved speech recognition, clinical structured LLM drafting, transcript diff editing UI, secure referring-clinician delivery/PDF generation, outbox dispatch/retry/dead-letter worker, cross-facility consultation, retention/export/restore operations, metrics beyond status counts, bilingual UI, React client, production networking/encryption/key management and clinical/security readiness. Initial list UIs have bounded subsets; use paginated APIs for larger datasets.

The current ORM protects history through application writes; database roles, tamper-evident audit storage and governance must still be designed for production. Encryption is a hosting/key-management requirement, not implemented field-level cryptography. Framework's existing AutoMapper advisory remains inherited, and the .NET runtime upgrade must be coordinated separately. The implementation is an executable foundation, not a clinically approved deployment.

PACS study linkage and bounded DICOMweb reads are now implemented. The PacsStudyLinks migration and SQL script are prepared only; no schema changes or imports were executed. See [imaging setup](IMAGING.md) for scope, configuration and limits.

