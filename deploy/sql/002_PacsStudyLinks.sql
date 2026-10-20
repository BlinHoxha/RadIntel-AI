START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014170557_PacsStudyLinks') THEN
    CREATE TABLE "ImagingStudies" (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "ArchiveKey" character varying(100) NOT NULL,
        "StudyInstanceUid" character varying(64) NOT NULL,
        "VerifiedBy" character varying(200) NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FacilityId" uuid NOT NULL,
        "ConcurrencyToken" uuid NOT NULL,
        "CreatedAt" bigint NOT NULL,
        CONSTRAINT "PK_ImagingStudies" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_ImagingStudies_OrganizationId_FacilityId_Id" UNIQUE ("OrganizationId", "FacilityId", "Id"),
        CONSTRAINT "FK_ImagingStudies_Facilities_OrganizationId_FacilityId" FOREIGN KEY ("OrganizationId", "FacilityId") REFERENCES "Facilities" ("OrganizationId", "Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ImagingStudies_Orders_OrganizationId_FacilityId_OrderId" FOREIGN KEY ("OrganizationId", "FacilityId", "OrderId") REFERENCES "Orders" ("OrganizationId", "FacilityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014170557_PacsStudyLinks') THEN
    CREATE UNIQUE INDEX "IX_ImagingStudies_OrganizationId_ArchiveKey_StudyInstanceUid" ON "ImagingStudies" ("OrganizationId", "ArchiveKey", "StudyInstanceUid");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014170557_PacsStudyLinks') THEN
    CREATE INDEX "IX_ImagingStudies_OrganizationId_FacilityId_OrderId" ON "ImagingStudies" ("OrganizationId", "FacilityId", "OrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014170557_PacsStudyLinks') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014170557_PacsStudyLinks', '9.0.8');
    END IF;
END $EF$;
COMMIT;

