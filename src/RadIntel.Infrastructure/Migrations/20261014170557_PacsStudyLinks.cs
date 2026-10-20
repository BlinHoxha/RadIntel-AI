using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadIntel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PacsStudyLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImagingStudies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StudyInstanceUid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VerifiedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagingStudies", x => x.Id);
                    table.UniqueConstraint("AK_ImagingStudies_OrganizationId_FacilityId_Id", x => new { x.OrganizationId, x.FacilityId, x.Id });
                    table.ForeignKey(
                        name: "FK_ImagingStudies_Facilities_OrganizationId_FacilityId",
                        columns: x => new { x.OrganizationId, x.FacilityId },
                        principalTable: "Facilities",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImagingStudies_Orders_OrganizationId_FacilityId_OrderId",
                        columns: x => new { x.OrganizationId, x.FacilityId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "OrganizationId", "FacilityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImagingStudies_OrganizationId_ArchiveKey_StudyInstanceUid",
                table: "ImagingStudies",
                columns: new[] { "OrganizationId", "ArchiveKey", "StudyInstanceUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImagingStudies_OrganizationId_FacilityId_OrderId",
                table: "ImagingStudies",
                columns: new[] { "OrganizationId", "FacilityId", "OrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImagingStudies");
        }
    }
}
