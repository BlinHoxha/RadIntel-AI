# Business plan

> Historical design baseline: this document records the pre-implementation planning snapshot. See [current implementation status](IMPLEMENTATION.md) for delivered behavior, prepared migrations/seeds, validation and remaining integrations.

Version 1.1 | 14 October 2026 | Aligned planning specification

## Proposition and present stage

RadIntel AI is a radiology-specific B2B product concept for Kosovo diagnostic clinics and departments. The proposed value is faster physician-reviewed reporting, organized worklists, historical-study access and controlled delivery. AI is optional assistive documentation. It is not an autonomous diagnostic product.

Current implementation is an empty solution. Framework and AI-Framework reduce some foundation work, but do not supply a finished clinic system, PACS integration, speech recognition, report signatures, privacy governance or clinical validation. Do not describe planned features as deployed or validated in marketing, contracts or demonstrations.

The original PDF's national modernization context is retained as a discovery lead, not independently verified market evidence. Exact waiting times, clinic counts, competitor penetration, national integration requirements and willingness to pay remain research questions.

## Customers and differentiation hypotheses

| Segment | Buyer | Proposed need | Sales dependency |
| --- | --- | --- | --- |
| Small imaging clinic | Owner / lead radiologist | Worklist, templates, reporting and scheduling | Simple setup, training and predictable cost |
| Multi-site diagnostic center | Operations director | Standardized reporting and facility dashboards | Facility permissions and operational consistency |
| Hospital radiology department | IT / procurement | Security, RIS/PACS integration and audit | Longer procurement and environment-specific acceptance |
| Teleradiology group | Clinical director | Distributed assignments and secure delivery | Explicit cross-site sharing and governance |

Differentiation hypotheses from the original plan: Albanian-first/English-supported templates, integration with existing PACS, immutable reports/addenda and optional AI under physician control. Private-cloud, on-premises and hybrid are possible target topologies; none is packaged or operational today. Do not promise an offline LLM or speech recognizer on the basis of AI-Framework.Local: that adapter provides transient retrieval and deterministic extractive output for development.

Interview 10-15 clinics/radiologists, observe their workflows, and seek two design partners. Map existing RIS/PACS/EHR, dictation, outsourced reporting, scheduling, contracts and support burden. Use 5-10 vendor/customer demonstrations to compare language support, integration cost, training, security and switching barriers. Record evidence before making market-size or superiority claims.

## Packaging hypotheses retained from version 1.0

| Package | Indicative monthly price | Proposed inclusion / limits |
| --- | --- | --- |
| Starter | EUR 79 | One facility; worklist, templates, reporting; capped users |
| Clinic | EUR 249 | Scheduling, audit, basic PACS integration and more users |
| Professional | EUR 599 | Multi-facility dashboards, later dictation/AI allowance and priority support |
| Enterprise | Custom quote | Negotiated on-premises/integration/SLA/security work |

These are unvalidated planning assumptions, not offers, supplier quotes or recommended market prices. Security, tenant isolation and clinically necessary traceability apply to every tier; an audit add-on must never remove essential controls from Starter. Dictation/AI can only be sold once the corresponding R2/R3 feature is accepted. Basic PACS integration must specify supported archive/version and what implementation work is included.

Exclude archive-scale image storage and unbounded AI from base subscriptions unless measured economics support inclusion. Define users, studies, storage, retention, transcription minutes, tokens, integration fees and support limits explicitly. Vendor agreements and clinical data processing terms depend on actual provider selection.

## Cost implications of the current architecture

| Reuse | Potential saved work | Work/cost still owned by RadIntel |
| --- | --- | --- |
| Framework libraries | Base entities/DTOs, pagination, optional CRUD and EF primitives | Tenant/patient authorization, application schema/migrations, workflow rules, signing, audit and operations |
| AI-Framework libraries | Text ingestion, retrieval and selected model adapters | Speech service, structured reporting, approved corpus lifecycle, durable provenance/jobs, evaluations, quotas and clinician UI |
| Existing PACS | Avoids building a replacement archive | Adapter discovery, network access, identity matching, viewer validation and support |
| Existing infrastructure examples | Starting resource composition | Product images, identities, protected networking, migrations, backups, monitoring and acceptance |

No measured development savings or operating-cost reductions are claimed. Shared library licensing/distribution must be resolved before independent commercial distribution; AI-Framework's README currently states no scaffold license is assigned. Framework's documented dependency advisory and coordinated runtime upgrade also need engineering budget before release.

## Illustrative economics from the original plan

| Package | Monthly revenue assumption | Variable cost assumption | Contribution before fixed costs |
| --- | --- | --- | --- |
| Starter | EUR 79 | EUR 35 | EUR 44 |
| Clinic | EUR 249 | EUR 80 | EUR 169 |
| Professional | EUR 599 | EUR 180 | EUR 419 |

Costs exclude shared engineering, sales, clinical/privacy/security work and customer acquisition. Validate per-clinic speech usage, model tokens, retrieval service minimums, database/backup costs, viewer/PACS traffic and staff support time. A small clinic's recurring price may not cover dedicated cloud resources or on-prem support.

| Scenario | Customer mix | Monthly recurring revenue | Annualized run rate |
| --- | --- | --- | --- |
| Conservative | 5 Starter + 3 Clinic + 1 Professional | EUR 1,741 | EUR 20,892 |
| Base | 10 Starter + 8 Clinic + 3 Professional | EUR 4,579 | EUR 54,948 |
| Growth | 20 Starter + 15 Clinic + 6 Professional | EUR 8,909 | EUR 106,908 |

Arithmetic is retained and checked. Annualized run rate is monthly revenue multiplied by 12, not recognized revenue, profit or a forecast. Assumes list price, no churn and no discounts.

| Startup category | Original one-time EUR range | Original monthly EUR range |
| --- | --- | --- |
| Discovery and clinical advisor | 2,000-6,000 | 300-1,000 |
| MVP engineering | 8,000-35,000 | Variable |
| Security/legal/privacy preparation | 2,000-10,000 | 100-500 |
| Hosting, backup, monitoring and AI | 500-2,000 | 150-900 |
| Pilot onboarding/training | 1,000-4,000 | 100-400 |
| Sales/support operations | 500-2,000 | 300-1,500 |

These figures remain directional source assumptions, not validated estimates. Founder work has opportunity cost. On-premises maintenance, high-availability imaging or future regulated image AI require separate costing. Re-estimate after provider/PACS discovery and a measured synthetic prototype.

## Roadmap and decision gates

The original time windows are scenario estimates, not commitments. Sequence by completed gates:

| Original window | Aligned work | Exit evidence |
| --- | --- | --- |
| Weeks 1-4 | Discovery, scope, pilot workflow/PACS inventory, intended-use and data-processing review | Ten or more interviews, two design partners, recorded problems and integration assumptions |
| Weeks 5-8 | Synthetic reporting/worklist prototype and library integration proof | Radiologists accept workflow; demonstrable tenant boundaries and manual fallback |
| Months 3-5 | R1 manual reporting, state machines, identity, migrations, audit, signing/addenda, operational testing | Required R1 acceptance checks; backup/restore and access tests |
| Months 6-7 | One or two supervised R1 pilots after readiness approval | Measured benefit, recorded incidents and support/integration cost |
| Months 8-12 | Paid conversion and subsequent R2/R3 features as accepted | At least two paying customers, retention evidence and sustainable support |

Professional AI promises require a separate R2/R3 acceptance schedule. A synthetic screenshot/demo is not a clinical pilot. Gate A validates demand; Gate B validates usability/semantics; Gate C requires reviewed launch readiness and contracts; Gate D requires conversion and operating evidence.

## Risks and responses

| Risk | Response / decision |
| --- | --- |
| Existing clinic tools are adequate | Observe and test paid willingness before broad development |
| PACS/network variability delays onboarding | Integration inventory, supported-vendor matrix, paid discovery and explicit failure handling |
| Hallucinations or transcription errors | Draft-only flow, transcript/source visibility, clinician correction, separate signing and AI-off fallback |
| Tenant/patient data exposure | Product authorization, permission tests, protected telemetry/storage and response procedures |
| Intended-use or jurisdiction uncertainty | Qualified local assessment before clinical claims or patient rollout |
| Low margins or heavy support | Measured quotas, integration fees, pilot cost data and tier adjustments |
| National integration requirements change | Versioned application adapters; no assumed IHIS API |
| Foundation dependencies need upgrades | Coordinated remediation and runtime plan; no unreviewed framework changes |

Confirm applicable data-processing, hosting, professional liability, intended-use/classification, procurement, licensing and vendor obligations with responsible specialists. This revision does not determine legal applicability or certify readiness. Carry the original PDF's legal/market references forward as research leads; do not treat them as verified current conclusions.

See [functional scope](02_Functional_Documentation.md) and [implementation checklist](05_Framework_Alignment.md) for what must actually be built before each promise is deliverable.

