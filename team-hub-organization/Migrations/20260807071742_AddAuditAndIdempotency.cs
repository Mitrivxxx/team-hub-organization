using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace team_hub_organization.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_Email",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Slug",
                table: "organizations");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Email",
                table: "organizations",
                column: "Email",
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"Email\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Status_DeletedAt",
                table: "organizations",
                columns: new[] { "Status", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_CreatedAt",
                table: "idempotency_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_UserId_Key_RequestPath",
                table: "idempotency_records",
                columns: new[] { "UserId", "Key", "RequestPath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_audit_events_OrganizationId_Action_OccurredAt",
                table: "organization_audit_events",
                columns: new[] { "OrganizationId", "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_audit_events_OrganizationId_OccurredAt",
                table: "organization_audit_events",
                columns: new[] { "OrganizationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "organization_audit_events");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Email",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Slug",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Status_DeletedAt",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "organizations");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Email",
                table: "organizations",
                column: "Email",
                unique: true,
                filter: "\"Email\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true);
        }
    }
}
