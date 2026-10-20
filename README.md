# RadIntel AI

.NET 9 radiology workflow and physician-controlled reporting, consuming the independent sibling [Framework](../Framework/README.md) and [AI-Framework](../AI-Framework/README.md) libraries.

The implementation now includes clinic onboarding, authoritative facility memberships, patients/orders, scheduling/worklist, versioned manual reports, explicit review/signing, addenda, audit/outbox persistence, approved templates, checked transcripts and a guarded draft-proposal workflow. A browser workspace is served by the API. See [implementation status and setup](docs/IMPLEMENTATION.md) for exact capabilities and remaining integrations.

**Database changes have not been executed.** PostgreSQL migration files, a reviewable SQL script and optional seed definitions are prepared only. Application startup does not initialize, migrate or seed a database. No application database or seed import was run during implementation.

## Build and verify

From this directory, with the sibling Framework and AI-Framework repositories present:

```powershell
dotnet restore RadIntel-AI.sln
dotnet build RadIntel-AI.sln --no-restore -c Release
dotnet test RadIntel-AI.sln --no-build --no-restore -c Release
```

`FrameworkRoot` and `AIFrameworkRoot` are overridable MSBuild properties for other checkout layouts. Shared source and package versions remain owned by their respective repositories. Persistence now selects Framework.Persistence.PostgreSql, excluding the legacy AutoMapper/SQL Server dependencies from RadIntel's runtime. The optional legacy mapping adapter's advisory remains unresolved for applications still using it.

## Start for local inspection

Configure authentication first. Production uses an HTTPS OIDC/JWT authority and audience; `sub` identifies the user, `tenant_id` the organization. Database Membership records determine facility permissions. The first owner must explicitly onboard using a token carrying `radintel_provision=true`.

For an isolated loopback-only development session, set these process environment variables before starting:

```powershell
$env:Authentication__Mode = 'Development'
$env:Authentication__DevelopmentToken = '<your-private-random-token-at-least-32-characters>'
$env:Authentication__AllowDevelopmentProvisioning = 'true'
# Optional only for testing review/signing locally:
$env:Authentication__AllowSimulatedMfa = 'true'
# Optional deterministic draft assistance; no LLM and no image interpretation:
$env:AI__DraftProvider = 'Verbatim'
dotnet run --project src/RadIntel.Api --launch-profile http
```

Open `http://localhost:5300/`. Enter the private development token, your chosen user ID and a nonempty organization UUID. No identity/clinic data is automatically created. Database-backed actions remain unavailable until an administrator separately reviews and applies the prepared schema and configures runtime database credentials. The live-health endpoint and static workspace can be inspected beforehand. Never use development authentication for a clinical or public deployment.

## Database and seed review

- [Initial migration and model snapshot](src/RadIntel.Infrastructure/Migrations/)
- [Idempotent PostgreSQL schema script](deploy/sql/001_InitialRadiology.sql)
- [Prepared PACS study-link increment](deploy/sql/002_PacsStudyLinks.sql)
- [Prepared reference template definitions](data/seeds/README.md)
- [Optional template importer](scripts/Import-ReferenceTemplates.ps1) — preview by default; no automatic publication

No patient, organization, account or credential seed records are provided. Reference templates are unpublished when imported and need clinical review before publication.

## Documentation

The original [use cases](docs/01_Problem_Solving_Use_Cases.md), [functional specification](docs/02_Functional_Documentation.md), [business plan](docs/03_Business_Plan.md), [technical design](docs/04_Technical_Documentation.md) and [alignment snapshot](docs/05_Framework_Alignment.md) describe the broader roadmap. The [implementation record](docs/IMPLEMENTATION.md) is authoritative for what works now. [Original PDFs](docs/originals/README.md) remain unchanged.

PACS study linkage and controlled DICOMweb reads are implemented; see [imaging setup and limits](docs/IMAGING.md). No live archive has been tested. Speech recognition, OHIF/full viewer integration, FHIR, result delivery and clinical LLM report generation remain future integrations. Azure-backed institutional template Q&A is configurable; the report-proposal adapter currently preserves checked findings verbatim and requires a clinician-authored impression. Manual reporting does not depend on an AI provider.

