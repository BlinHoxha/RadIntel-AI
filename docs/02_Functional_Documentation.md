# Functional documentation

> Historical design baseline: this document records the pre-implementation planning snapshot. See [current implementation status](IMPLEMENTATION.md) for delivered behavior, prepared migrations/seeds, validation and remaining integrations.

Version 1.1 | 14 October 2026 | Aligned planning specification

## Product and status

RadIntel AI is a standalone radiology workflow and reporting application. AI assists documentation of physician-provided observations; MVP does not interpret images or independently diagnose. All requirements here are planned: the current solution has no projects or running product features.

Framework's generic services/controllers are optional foundations. RadIntel must enforce every role, tenant, patient, report and workflow restriction. AI-Framework's review label and evidence flags do not enforce clinician approval or prove clinical correctness.

## Actors

| Actor | Permitted responsibilities | Restrictions |
| --- | --- | --- |
| Clinic administrator | Clinic/facility configuration, staff access, services and template publication policy | Cannot silently alter signed clinical content; administration does not imply unrestricted clinical access |
| Reception / scheduler | Authorized patient registration and appointment management | No unrelated patient or facility access |
| Radiology technician | Acquisition status and permitted study metadata | Cannot sign reports |
| Radiologist | Authorized study review, dictation, report editing, signing and addenda | Only assigned or otherwise explicitly authorized resources |
| Referring clinician | Read explicitly shared finalized results | Cannot edit radiology reports |
| Compliance auditor | Read authorized access/audit evidence | Read-only and minimum necessary content |
| Platform support | Diagnose failures using redacted telemetry | No default patient-content access |

Roles are necessary but insufficient: tenant membership, facility grants, patient/study authorization and permitted action must also pass. Deny by default. The identity provider owns authentication/MFA; RadIntel owns membership and resource permissions.

## Release scope

| Release | Planned features | Gate |
| --- | --- | --- |
| R1 / manual workflow MVP | Organizations/facilities, identity integration, patients/orders/scheduling, assignments/worklist, manual templates and report revisions, signing/addenda, audit, authorized PACS study links, basic metrics | Complete and tested manual workflow; no dependence on an AI provider |
| R2 / assisted input and imaging | OHIF integration, Albanian/English UI/templates, dictation and transcript review, prior-report search, secure result delivery | Language and viewer acceptance, provider/privacy review and permission tests |
| R3 / optional knowledge assistance | Approved-document RAG, structured AI report proposals, cited textual comparisons, consultation, equipment availability and advanced analytics | Provenance/approval enforcement, per-language evaluations and reliable failure fallback |
| R4 / separate research | Potential image AI | Separate intended-use, evidence and regulatory workstream |

A synthetic RAG or draft demonstration may precede R3. It does not establish product readiness or move a clinical feature into R1. R2 may format a checked transcript using a selected template without retrieved knowledge; retrieval-enhanced drafting belongs to R3.

## Core objects

Organization, Facility, Membership/RoleGrant, Patient, PatientIdentifier, Referrer, ImagingOrder, Appointment, Modality, Equipment, ImagingStudy, SeriesReference, WorklistAssignment, Report, ReportVersion, ReportTemplateVersion, DictationTranscriptVersion, AIProposal, Signature, Addendum, DeliveryGrant, Notification, AuditEvent, OutboxMessage, DowntimeEvent, ConsultationGrant and ConsentOrLegalBasisRecord.

These are proposed product entities. They are not tables supplied by Framework or AI-Framework. Template publication and patient reports are distinct data lifecycles.

## Functional requirements and acceptance

| ID / release | Requirement | Acceptance criteria |
| --- | --- | --- |
| FR-001 / R1 | Organization onboarding with facilities, modalities, staff and locale | Tenant B cannot query, mutate, export or indirectly retrieve A's records; facility grants tested |
| FR-002 / R1 | Authentication with MFA and action/resource permissions | Expired/invalid identity denied; forbidden requests blocked and audited; forged tenant IDs do not grant access |
| FR-003 / R1 | Patient registration, scoped identifier validation and duplicate detection | Duplicate warning; verified privileged merge preserves historical links and audit; no name-only matching |
| FR-004 / R1 | Imaging orders with modality, body region and referral metadata | Required fields validated; every state change has actor/time/reason; patient belongs to authorized tenant |
| FR-005 / R1 | Scheduling, rescheduling and cancellations | Capacity enforced under concurrent booking; clinical priority entered only by authorized staff; changes audited |
| FR-006 / R1 | Role-constrained worklist and assignments | Acquired/review/draft/signature states visible only within grants; competing claims have one winner |
| FR-007 / R1 links, R2 viewer | PACS study linkage using archive and StudyInstanceUID | Verified patient/order mapping; viewer and image requests reauthorize; no unrestricted archive credentials |
| FR-008 / R1 | Template-based manual drafts and revision history | Drafts visibly unsigned; versions traceable; report cannot be distributed as final before signature |
| FR-009 / R2, enhanced R3 | Transcription and optional formatted proposal | Original transcript and edits visible; negation/units/laterality preserved; ambiguity requires correction; accepting AI never signs |
| FR-010 / R2-R3 | AI provenance and review history | Persist transcript/template/report version IDs, model/deployment identity, prompt/schema versions, retrieved source versions, generation time, validation and reviewer action |
| FR-011 / R1 | Sign a specific immutable report version | Qualified authorized signer and step-up policy; stale version rejected; signature binds exact content; post-signature mutation blocked |
| FR-012 / R2 | Prior approved report/study timeline | Patient matching verified; cross-patient/tenant tests pass; historic access reauthorized |
| FR-013 / R2 | Controlled distribution of signed results | Grant expires/revokes; access/download audited; draft and unrelated resources inaccessible; no identifiers in public URLs |
| FR-014 / R1 basic, R2 history | Authorized search/filter by patient, date, modality, facility and status | Bounded pagination and sorting; permissions apply before counts/results; agreed load target met |
| FR-015 / R1 basic, R3 advanced | Backlog, turnaround, no-show and utilization metrics | Defined event/time windows; totals reconcile with source events; scoped manager access |
| FR-016 / R1, extended later | Audit and retention | Protected append-only history for sensitive actions; tested retention/export procedure; restricted audit administration; no claim that a hash alone prevents tampering |
| FR-017 / R2 | Albanian and English UI/templates | Locale and terminology reviewed by pilot radiologists; speech and drafting evaluated separately by language |
| FR-018 / R2 DICOMweb, later FHIR | Versioned integration adapters | Contract/failure/retry tests; actual archive capabilities verified; FHIR version/profile agreed with receiving system |

## State transitions and consistency

The original linear journey is retained as a user-visible progression:

`Draft -> Requested -> Scheduled -> CheckedIn -> Acquired -> ReadyForReview -> InReview -> ReportDraft -> Signed -> Delivered`.

Implement it as coordinated aggregates: order/acquisition status, assignment status, report status and delivery status. A delivery retry must not reverse a signed report, and a report addendum must not imply reacquisition. Define permitted transitions and exceptional paths (`Cancelled`, rescheduling events, `AcquisitionFailed`, correction requests) in product services before coding.

Report progression: `Draft -> PendingReview -> Signed`. Rejection returns to a new editable draft revision. Signed versions remain immutable; correction creates a linked Addendum and its own review/signature. AIProposal progression: `Requested -> Generated -> ValidationFailed | PendingReview -> Accepted | Rejected`; failures/cancellation are explicit. A changed transcript/template/report base makes an existing proposal stale and ineligible for automatic acceptance.

Writes use optimistic concurrency and idempotency where repeated requests can create duplicate effects. Sign/version freeze/audit/outbox intent occur atomically in the product database. Notification delivery happens later and cannot change clinical content. Generic update/delete routes must never bypass these rules.

## AI behavior and review

The screenshot's flow is the target: dictation -> transcription/NLP -> approved template RAG/LLM -> .NET validation -> radiologist approval. Every stage must remain inspectable. Manual text entry and manual reporting remain available.

1. Preserve the original dictated/transcribed observations and checked transcript version.
2. Retrieve only published, permitted template/policy versions. Patient observations come from the current encounter, never a template or unrelated prior study.
3. Produce a schema-versioned proposal. Unknown details stay absent or explicitly incomplete; no automatic normal-findings boilerplate.
4. Validate shape, bounds, required fields, scope, version consistency and source IDs in .NET. Deterministic checks cannot prove every sentence is clinically supported; semantic review remains mandatory.
5. Show original input, proposal, relevant sources and a diff. Radiologist corrects or rejects; acceptance creates an ordinary draft version.
6. A separate explicit signature action finalizes that exact report version. Any provider failure or AI-off switch returns the user to manual reporting.

`Grounded=true` only reports that evidence reached generation. `RequiresHumanReview=true` is a profile label, not an authorization or approval mechanism. AI-Framework currently has no structured radiology output or speech-to-text implementation.

## Nonfunctional targets

The original pilot figures remain proposed targets, not measured service levels: 99.5% monthly availability excluding agreed maintenance, P95 ordinary metadata API below 500 ms under a defined load, RPO at most 24 hours and RTO at most 8 hours. Agree workloads and contracts before promising them. Image latency depends on PACS and network and must be measured separately.

Require encrypted transport/storage, least-privilege identity/secrets, scoped access, keyboard accessibility, bilingual error messages, tested restore and incident procedures, and redacted correlation/telemetry. The product must implement these controls; consuming shared libraries does not certify them. Raw audio, prompts and responses require an explicit access/retention policy; default application logs contain metadata only.

## Pilot acceptance

Demonstrate all R1 flows using synthetic records and studies, concurrent access and signing failures, tenant/patient isolation, immutable versions and addenda, downtime fallback and backup restoration. For later AI features, obtain clinician acceptance of each language/template and test unsupported additions, omissions, prompt injection, stale proposals and provider outages. Record clinical, privacy/security, operational and clinic approvals before real patient rollout.

Open choices and dependency evidence are in the [alignment checklist](05_Framework_Alignment.md); engineering design is in [technical documentation](04_Technical_Documentation.md).

