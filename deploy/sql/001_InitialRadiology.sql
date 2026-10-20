CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Organizations" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        CONSTRAINT "PK_Organizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Facilities" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "TimeZone" character varying(100) NOT NULL,
        CONSTRAINT "PK_Facilities" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Facilities_OrganizationId_Id" UNIQUE ("OrganizationId", "Id"),
        CONSTRAINT "FK_Facilities_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "AuditEvents" (
        "Id" uuid NOT NULL,
        "ActorId" text NOT NULL,
        "Action" text NOT NULL,
        "EntityId" uuid NOT NULL,
        "Reason" text,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_AuditEvents" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_AuditEvents_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_AuditEvents_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Equipment" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Modality" text NOT NULL,
        "Available" boolean NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Equipment" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Equipment_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Equipment_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Memberships" (
        "Id" uuid NOT NULL,
        "UserId" character varying(200) NOT NULL,
        "Roles" integer NOT NULL,
        "Active" boolean NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Memberships" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Memberships_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Memberships_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Patients" (
        "Id" uuid NOT NULL,
        "Identifier" character varying(100) NOT NULL,
        "IdentifierIssuer" character varying(100) NOT NULL,
        "DisplayName" character varying(200) NOT NULL,
        "BirthDate" date,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Patients" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Patients_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Patients_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Templates" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Language" text NOT NULL,
        "Modality" text NOT NULL,
        "BodyRegion" text NOT NULL,
        "Structure" text NOT NULL,
        "Version" integer NOT NULL,
        "Published" boolean NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Templates" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Templates_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Templates_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Orders" (
        "Id" uuid NOT NULL,
        "PatientId" uuid NOT NULL,
        "Modality" character varying(20) NOT NULL,
        "BodyRegion" character varying(100) NOT NULL,
        "Referral" character varying(4000) NOT NULL,
        "Priority" character varying(20) NOT NULL,
        "Status" integer NOT NULL,
        "AssignedTo" character varying(200),
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Orders" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Orders_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Orders_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Orders_Patients_OrganizationId_FacilityId_PatientId" FOREIGN KEY ("OrganizationId", "FacilityId", "PatientId") REFERENCES "Patients" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Appointments" (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "EquipmentId" uuid NOT NULL,
        "StartsAt" bigint NOT NULL,
        "EndsAt" bigint NOT NULL,
        "Cancelled" boolean NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Appointments_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Appointments_Equipment_OrganizationId_FacilityId_EquipmentId" FOREIGN KEY ("OrganizationId", "FacilityId", "EquipmentId") REFERENCES "Equipment" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Appointments_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Appointments_Orders_OrganizationId_FacilityId_OrderId" FOREIGN KEY ("OrganizationId", "FacilityId", "OrderId") REFERENCES "Orders" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Reports" (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "CheckedTranscriptId" uuid,
        "ParentReportId" uuid,
        "AmendmentReason" text,
        "Kind" integer NOT NULL,
        "Status" integer NOT NULL,
        "CurrentVersion" integer NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Reports" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Reports_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Reports_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Reports_Orders_OrganizationId_FacilityId_OrderId" FOREIGN KEY ("OrganizationId", "FacilityId", "OrderId") REFERENCES "Orders" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Reports_Reports_OrganizationId_FacilityId_ParentReportId" FOREIGN KEY ("OrganizationId", "FacilityId", "ParentReportId") REFERENCES "Reports" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "ReportVersions" (
        "Id" uuid NOT NULL,
        "ReportId" uuid NOT NULL,
        "Number" integer NOT NULL,
        "Examination" text NOT NULL,
        "Findings" text NOT NULL,
        "Impression" text NOT NULL,
        "Language" text NOT NULL,
        "AuthorId" text NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_ReportVersions" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_ReportVersions_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_ReportVersions_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ReportVersions_Reports_OrganizationId_FacilityId_ReportId" FOREIGN KEY ("OrganizationId", "FacilityId", "ReportId") REFERENCES "Reports" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Transcripts" (
        "Id" uuid NOT NULL,
        "ReportId" uuid NOT NULL,
        "OriginalText" text NOT NULL,
        "CheckedText" text NOT NULL,
        "Language" text NOT NULL,
        "CheckedBy" text NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Transcripts" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Transcripts_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Transcripts_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transcripts_Reports_OrganizationId_FacilityId_ReportId" FOREIGN KEY ("OrganizationId", "FacilityId", "ReportId") REFERENCES "Reports" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Outbox" (
        "Id" uuid NOT NULL,
        "ReportVersionId" uuid NOT NULL,
        "EventType" text NOT NULL,
        "Payload" text NOT NULL,
        "DispatchedAt" bigint,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Outbox" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Outbox_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Outbox_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Outbox_ReportVersions_OrganizationId_FacilityId_ReportVersi~" FOREIGN KEY ("OrganizationId", "FacilityId", "ReportVersionId") REFERENCES "ReportVersions" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Signatures" (
        "Id" uuid NOT NULL,
        "ReportVersionId" uuid NOT NULL,
        "SignedBy" character varying(200) NOT NULL,
        "ContentHash" character varying(64) NOT NULL,
        "RequestKey" character varying(100) NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Signatures" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Signatures_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Signatures_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Signatures_ReportVersions_OrganizationId_FacilityId_ReportV~" FOREIGN KEY ("OrganizationId", "FacilityId", "ReportVersionId") REFERENCES "ReportVersions" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE TABLE "Proposals" (
        "Id" uuid NOT NULL,
        "ReportId" uuid NOT NULL,
        "TranscriptId" uuid NOT NULL,
        "TemplateId" uuid NOT NULL,
        "BaseVersion" integer NOT NULL,
        "BaseConcurrencyToken" uuid NOT NULL,
        "Findings" text NOT NULL,
        "Provider" text NOT NULL,
        "ModelVersion" text NOT NULL,
        "PromptVersion" text NOT NULL,
        "SchemaVersion" text NOT NULL,
        "SourcesJson" text NOT NULL,
        "Status" integer NOT NULL,
        "ReviewedBy" text,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_Proposals" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Proposals_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_Proposals_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Proposals_Reports_OrganizationId_FacilityId_ReportId" FOREIGN KEY ("OrganizationId", "FacilityId", "ReportId") REFERENCES "Reports" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Proposals_Templates_OrganizationId_FacilityId_TemplateId" FOREIGN KEY ("OrganizationId", "FacilityId", "TemplateId") REFERENCES "Templates" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Proposals_Transcripts_OrganizationId_FacilityId_TranscriptId" FOREIGN KEY ("OrganizationId", "FacilityId", "TranscriptId") REFERENCES "Transcripts" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Appointments_OrganizationId_FacilityId_EquipmentId_StartsAt" ON "Appointments" ("OrganizationId", "FacilityId", "EquipmentId", "StartsAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Appointments_OrganizationId_FacilityId_OrderId" ON "Appointments" ("OrganizationId", "FacilityId", "OrderId") WHERE "Cancelled" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Memberships_OrganizationId_FacilityId_UserId" ON "Memberships" ("OrganizationId", "FacilityId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Orders_OrganizationId_FacilityId_PatientId" ON "Orders" ("OrganizationId", "FacilityId", "PatientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Orders_OrganizationId_FacilityId_Status_CreatedAt" ON "Orders" ("OrganizationId", "FacilityId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Outbox_OrganizationId_FacilityId_ReportVersionId" ON "Outbox" ("OrganizationId", "FacilityId", "ReportVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Patients_OrganizationId_FacilityId_IdentifierIssuer_Identif~" ON "Patients" ("OrganizationId", "FacilityId", "IdentifierIssuer", "Identifier");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Proposals_OrganizationId_FacilityId_ReportId" ON "Proposals" ("OrganizationId", "FacilityId", "ReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Proposals_OrganizationId_FacilityId_TemplateId" ON "Proposals" ("OrganizationId", "FacilityId", "TemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Proposals_OrganizationId_FacilityId_TranscriptId" ON "Proposals" ("OrganizationId", "FacilityId", "TranscriptId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Reports_OrganizationId_FacilityId_OrderId" ON "Reports" ("OrganizationId", "FacilityId", "OrderId") WHERE "Kind" = 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Reports_OrganizationId_FacilityId_ParentReportId" ON "Reports" ("OrganizationId", "FacilityId", "ParentReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_ReportVersions_OrganizationId_FacilityId_ReportId_Number" ON "ReportVersions" ("OrganizationId", "FacilityId", "ReportId", "Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Signatures_OrganizationId_FacilityId_ReportVersionId" ON "Signatures" ("OrganizationId", "FacilityId", "ReportVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Signatures_OrganizationId_FacilityId_SignedBy_RequestKey" ON "Signatures" ("OrganizationId", "FacilityId", "SignedBy", "RequestKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE UNIQUE INDEX "IX_Templates_OrganizationId_FacilityId_Name_Version" ON "Templates" ("OrganizationId", "FacilityId", "Name", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    CREATE INDEX "IX_Transcripts_OrganizationId_FacilityId_ReportId" ON "Transcripts" ("OrganizationId", "FacilityId", "ReportId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014130734_InitialRadiology') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014130734_InitialRadiology', '9.0.8');
    END IF;
END $EF$;
COMMIT;

