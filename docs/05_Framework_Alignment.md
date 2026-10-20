# Framework alignment and implementation checklist

> Historical design baseline: this document records the pre-implementation planning snapshot. See [current implementation status](IMPLEMENTATION.md) for delivered behavior, prepared migrations/seeds, validation and remaining integrations.

Version 1.1 | 14 October 2026 | Repository inspection snapshot

## What changed in this documentation revision

All four original planning documents have revised Markdown counterparts. Original PDFs are preserved byte-for-byte under `docs/originals`. This revision does not add projects, alter the solution, change shared libraries/packages, provision resources or modify existing infrastructure state. Source documents are requirements/reference material; their internal checklists are not authorization to deploy or change frameworks.

| Input | Retained | Aligned or clarified |
| --- | --- | --- |
| 01 Problem-solving use cases | UC-01 through UC-12, Kosovo radiology focus, physician control, pilot hypotheses | Per-release ownership; existing library versus missing product behavior; no patient records in general RAG index |
| 02 Functional documentation | FR-001 through FR-018, actors, signing/addenda, R1/R2/R3/R4 progression | Explicit acceptance criteria for scope/concurrency/stale proposals; separate AI acceptance and signature; release map for equipment and advanced analytics |
| 03 Business plan | Customer segments, discovery plan, original prices/costs/scenarios and gates | Empty-solution status, reuse limits, provider/integration costs, hypothetical pricing and delivery dates, essential security across tiers |
| 04 Technical documentation | Modular monolith, React/.NET, product modules, PACS/OHIF, provider choice, proposed API routes | Actual `RadIntel-AI.sln` name; direct library composition; product context/migrations; independent framework ownership; no ready-made speech or structured report service |
| Screenshot from chat | Dictation -> speech-to-text/NLP -> RAG/LLM -> .NET validation -> radiologist approval | Checked transcript, approved evidence versions, proposal diff and separate final signature; all stages explicitly planned |

The original technical sprint outline placed AI in a later phase while functional releases put dictation in R2 and RAG in R3. The revised documents consistently use R1 manual workflow, R2 dictation/viewer, R3 retrieval-enhanced drafting; an early synthetic demo does not change release acceptance.

## Evidence reviewed

Links below resolve against sibling repositories in the current workspace. These are inspected source files, not API promises for future versions.

| Evidence | Observation that affects RadIntel |
| --- | --- |
| [RadIntel solution](../RadIntel-AI.sln) | No Project entries; no product implementation exists |
| [Framework overview](../../Framework/README.md) | General .NET backend foundations, optional CRUD, no AI dependency; documents an existing dependency advisory and runtime migration need |
| [Framework context](../../Framework/src/Framework.Infrastructure/Persistence/FrameworkDbContext.cs) | Extensible context; base assembly model configuration only |
| [Framework DI](../../Framework/src/Framework.Infrastructure/DependencyInjection.cs) | Registers base context/repository and Framework migrations assembly; cannot register a product model unchanged |
| [Framework repository](../../Framework/src/Framework.Infrastructure/Persistence/Repositories/BaseRepository.cs) | Supports TContext; default query has no tenant/resource restriction |
| [Framework controller base](../../Framework/src/Framework.Web/Controllers/BaseControllerCRUD.cs) | Generic add/update/delete extension point; no report-signing invariants |
| [Framework host](../../Framework/src/Framework.Api/Program.cs) | Reference authentication/health example; product must own its host policies |
| [AI-Framework overview](../../AI-Framework/README.md) | Independent libraries, net9.0, Local/Azure/AWS capability boundaries |
| [AI contracts](../../AI-Framework/src/AIFramework.Core/Abstractions/IAiProviders.cs) | Retriever/model/catalog interfaces; no speech-to-text or structured clinical generator |
| [AI models](../../AI-Framework/src/AIFramework.Core/Models/AiModels.cs) | Knowledge scope/profiles and answer/provider generation shape; no report schema/model provenance record |
| [AI ingestion](../../AI-Framework/src/AIFramework.Application/Services/DocumentIngestionService.cs) | Synchronous text, new document ID each ingestion, one-million-character content limit |
| [AI answer orchestration](../../AI-Framework/src/AIFramework.Application/Services/KnowledgeAnswerService.cs) | 2,000-character question limit; scope/document defense in depth; null-scope tenant evidence; Grounded does not verify every assertion |
| [Default profile catalog](../../AI-Framework/src/AIFramework.Application/DocumentProfiles/DefaultDocumentProfileCatalog.cs) | Only general-document exists; radiology profiles must be supplied by the product |
| [AI DI](../../AI-Framework/src/AIFramework.Application/DependencyInjection.cs) | AddAiApplication uses TryAdd for default catalog and services |
| [Local DI](../../AI-Framework/src/AIFramework.Local/DependencyInjection.cs) | Process-local store and deterministic model for development |
| [Azure DI](../../AI-Framework/src/AIFramework.Azure/DependencyInjection.cs) | Combined or separate Azure model/search registration; credential/transport composition |
| [Bedrock adapter](../../AI-Framework/src/AIFramework.Aws/BedrockChatModel.cs) | AWS chat and AddBedrockChat; no AWS retrieval/embedding implementation |
| [Document extraction port](../../AI-Framework/src/AIFramework.Core/Abstractions/IDocumentProcessing.cs) | Extension contract for file extraction, no implemented OCR or speech service |
| [Framework infrastructure](../../Framework/infrastructure/README.md) | General Azure examples; existing AI resource ownership must be migrated before template applies |
| [AI infrastructure](../../AI-Framework/infrastructure/README.md) | AI-specific resources, identity-prefix assumption, embedding dimension and AWS IAM limits |
| [AI evaluation guidance](../../AI-Framework/evaluations/README.md) | Deterministic library tests are separate from domain/provider quality evaluations |

The integration descriptions also use [official OHIF DICOMweb documentation](https://docs.ohif.org/configuration/datasources/dicom-web/) and [HL7 DiagnosticReport](https://hl7.org/fhir/diagnosticreport.html). No new national/legal/medical/market conclusions are asserted. Original external references are retained in the archived PDFs as research leads.

## Gap and ownership matrix

| Capability | Existing reusable support | Planned RadIntel responsibility |
| --- | --- | --- |
| Authentication / MFA / permissions | Reference host JWT conventions | Identity integration, membership, facility/patient scope, step-up signing |
| Patients / orders / scheduling / worklist | Generic entity/DTO/repository/pagination | All entities, constraints, transitions and concurrent writes |
| Database / seed data | PostgreSQL/SQL Server base EF helpers | Select provider; product context/configuration/migrations and synthetic fixtures |
| Report lifecycle | No clinical implementation | Approved templates, draft versions, immutable signature, addenda and controlled export |
| Speech / NLP | No speech implementation | Approved adapter, transcript versions, reviewed normalization, bilingual evaluation |
| Template / policy retrieval | Text chunking, ingestion and scope-aware retrieval | Approval/version lifecycle, allowlists, facility ACLs, revocation, deletion/reindex jobs |
| Structured report generation | Chat model abstraction | Proposal schema, orchestration, parsing/validation and source provenance |
| Human approval | Profile label only | Mandatory review UI, accepted-draft command and separate sign command |
| PACS / viewer / FHIR | No product adapters | Archive capability matrix, authorized data path, identity mapping and contract tests |
| Audit / outbox / durable jobs | No complete shared implementation | Transactional intent, tamper protection, retries, idempotency, retention and recovery |
| Deployment / on-prem / hybrid | Separate foundation/AI examples | Product composition, identities, networking, backups, monitoring and support |
| Clinical readiness | No certification from libraries | Clinician/language acceptance, intended-use review, agreed real-data pilot readiness |

## Implementation order

- [ ] Confirm clinical owner, pilot workflow, data handling and allowed intended use.
- [ ] Choose database, identity provider, PACS archive/version and target hosting/data location.
- [ ] Create minimal Domain, Contracts, Application, Infrastructure and Api projects with explicit shared-library references; maintain separate package/build ownership.
- [ ] Implement RadIntel context/migrations, scoped memberships/queries and synthetic fixtures.
- [ ] Build and test patient/order/scheduling/worklist transitions, concurrency and protected audit.
- [ ] Build manual report templates/versions, signing/addenda, transactional outbox and R1 acceptance.
- [ ] Add authorized viewer integration, prior-report search, bilingual UI/templates, checked dictation and secure delivery for R2.
- [ ] Select approved speech/AI providers and measure language quality/cost before making commercial promises.
- [ ] Add published template allowlists, product profiles, durable corpus lifecycle and R3 retrieval.
- [ ] Add schema-versioned proposals, parsing/validation, full provenance, review/diff UI, quotas and manual fallback.
- [ ] Complete clinician/security/operational evaluations, restore drills and applicable pilot readiness approvals.
- [ ] Coordinate shared dependency/runtime remediation separately before production release.

No checked implementation item is implied by this documentation change. Detailed FR acceptance remains in [functional documentation](02_Functional_Documentation.md).

## Decisions still open

| Decision | Resolve before | Safe documentation assumption |
| --- | --- | --- |
| PostgreSQL versus SQL Server | First product migration/deployment | Both base adapters exist; one product migration strategy will be selected |
| Identity provider and clinic roles | Product authentication implementation | OIDC integration plus product resource permissions |
| Pilot PACS and viewer requirements | DICOM integration work | Existing archive remains image owner; no universal compatibility claim |
| Hosting, residency and support model | Provider selection and real-data pilot | No cloud/on-prem environment is assumed ready |
| Speech and generation provider/model | Dictation/AI integration | Local adapter is synthetic development support only |
| Signature and audit guarantees | Clinical signing implementation | Immutable product versions are required; legal/cryptographic properties need explicit design |
| Metrics/quality acceptance thresholds | Pilot and per-language evaluations | Original numerical figures are proposals, not achieved measurements |

## Verification record

This is a documentation-only revision. Verify all relative links using their destination paths, FR-001 through FR-018 and UC-01 through UC-12 coverage, balanced Markdown code fences, original PDF hashes, scenario arithmetic and the unchanged solution hash. No build/test result or live provider/deployment success is claimed for the unimplemented product.

