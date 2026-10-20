using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadIntel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRadiology : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.Id);
                    table.UniqueConstraint("AK_Facilities_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Facilities_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.UniqueConstraint("AK_AuditEvents_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_AuditEvents_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Modality = table.Column<string>(type: "text", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.UniqueConstraint("AK_Equipment_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Equipment_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Roles = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.UniqueConstraint("AK_Memberships_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Memberships_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdentifierIssuer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.UniqueConstraint("AK_Patients_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Patients_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Modality = table.Column<string>(type: "text", nullable: false),
                    BodyRegion = table.Column<string>(type: "text", nullable: false),
                    Structure = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                    table.UniqueConstraint("AK_Templates_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Templates_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyRegion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Referral = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.UniqueConstraint("AK_Orders_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Orders_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Patients_OrganizationId_FacilityId_PatientId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.PatientId },
                        principalTable: "Patients",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<long>(type: "bigint", nullable: false),
                    EndsAt = table.Column<long>(type: "bigint", nullable: false),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.UniqueConstraint("AK_Appointments_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Appointments_Equipment_OrganizationId_FacilityId_EquipmentId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.EquipmentId },
                        principalTable: "Equipment",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Orders_OrganizationId_FacilityId_OrderId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedTranscriptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmendmentReason = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.UniqueConstraint("AK_Reports_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Reports_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Orders_OrganizationId_FacilityId_OrderId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Reports_OrganizationId_FacilityId_ParentReportId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ParentReportId },
                        principalTable: "Reports",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Examination = table.Column<string>(type: "text", nullable: false),
                    Findings = table.Column<string>(type: "text", nullable: false),
                    Impression = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportVersions", x => x.Id);
                    table.UniqueConstraint("AK_ReportVersions_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_ReportVersions_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportVersions_Reports_OrganizationId_FacilityId_ReportId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ReportId },
                        principalTable: "Reports",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transcripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    CheckedText = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    CheckedBy = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transcripts", x => x.Id);
                    table.UniqueConstraint("AK_Transcripts_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Transcripts_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transcripts_Reports_OrganizationId_FacilityId_ReportId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ReportId },
                        principalTable: "Reports",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    DispatchedAt = table.Column<long>(type: "bigint", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                    table.UniqueConstraint("AK_Outbox_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Outbox_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Outbox_ReportVersions_OrganizationId_FacilityId_ReportVersi~",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ReportVersionId },
                        principalTable: "ReportVersions",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signatures", x => x.Id);
                    table.UniqueConstraint("AK_Signatures_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Signatures_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Signatures_ReportVersions_OrganizationId_FacilityId_ReportV~",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ReportVersionId },
                        principalTable: "ReportVersions",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    TranscriptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseVersion = table.Column<int>(type: "integer", nullable: false),
                    BaseConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    Findings = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    SourcesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.UniqueConstraint("AK_Proposals_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_Proposals_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proposals_Reports_OrganizationId_FacilityId_ReportId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.ReportId },
                        principalTable: "Reports",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proposals_Templates_OrganizationId_FacilityId_TemplateId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.TemplateId },
                        principalTable: "Templates",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proposals_Transcripts_OrganizationId_FacilityId_TranscriptId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.TranscriptId },
                        principalTable: "Transcripts",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrganizationId_FacilityId_EquipmentId_StartsAt",
                table: "Appointments",
                columns: new[] { "OrganizationId", "FacilityId", "EquipmentId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrganizationId_FacilityId_OrderId",
                table: "Appointments",
                columns: new[] { "OrganizationId", "FacilityId", "OrderId" },
                unique: true,
                filter: "\"Cancelled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_OrganizationId_FacilityId_UserId",
                table: "Memberships",
                columns: new[] { "OrganizationId", "FacilityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_FacilityId_PatientId",
                table: "Orders",
                columns: new[] { "OrganizationId", "FacilityId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_FacilityId_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "OrganizationId", "FacilityId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_OrganizationId_FacilityId_ReportVersionId",
                table: "Outbox",
                columns: new[] { "OrganizationId", "FacilityId", "ReportVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_OrganizationId_FacilityId_IdentifierIssuer_Identif~",
                table: "Patients",
                columns: new[] { "OrganizationId", "FacilityId", "IdentifierIssuer", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_OrganizationId_FacilityId_ReportId",
                table: "Proposals",
                columns: new[] { "OrganizationId", "FacilityId", "ReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_OrganizationId_FacilityId_TemplateId",
                table: "Proposals",
                columns: new[] { "OrganizationId", "FacilityId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_OrganizationId_FacilityId_TranscriptId",
                table: "Proposals",
                columns: new[] { "OrganizationId", "FacilityId", "TranscriptId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrganizationId_FacilityId_OrderId",
                table: "Reports",
                columns: new[] { "OrganizationId", "FacilityId", "OrderId" },
                unique: true,
                filter: "\"Kind\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrganizationId_FacilityId_ParentReportId",
                table: "Reports",
                columns: new[] { "OrganizationId", "FacilityId", "ParentReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportVersions_OrganizationId_FacilityId_ReportId_Number",
                table: "ReportVersions",
                columns: new[] { "OrganizationId", "FacilityId", "ReportId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_OrganizationId_FacilityId_ReportVersionId",
                table: "Signatures",
                columns: new[] { "OrganizationId", "FacilityId", "ReportVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_OrganizationId_FacilityId_SignedBy_RequestKey",
                table: "Signatures",
                columns: new[] { "OrganizationId", "FacilityId", "SignedBy", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Templates_OrganizationId_FacilityId_Name_Version",
                table: "Templates",
                columns: new[] { "OrganizationId", "FacilityId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transcripts_OrganizationId_FacilityId_ReportId",
                table: "Transcripts",
                columns: new[] { "OrganizationId", "FacilityId", "ReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "Signatures");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "Transcripts");

            migrationBuilder.DropTable(
                name: "ReportVersions");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropTable(
                name: "Facilities");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
