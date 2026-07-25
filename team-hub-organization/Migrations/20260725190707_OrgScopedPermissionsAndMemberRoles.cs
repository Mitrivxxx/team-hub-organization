using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace team_hub_organization.Migrations
{
    /// <inheritdoc />
    public partial class OrgScopedPermissionsAndMemberRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_roles_organization_name_scope";
                DROP INDEX IF EXISTS "IX_roles_system_name_scope";
                """);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "roles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE roles
                SET "IsSystem" = TRUE
                WHERE ("Scope" = 'ORG' AND "Name" IN ('Owner', 'Admin', 'Member'))
                   OR ("Scope" = 'TEAM' AND "Name" IN ('TeamLead', 'Member'));
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "roles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "organization_member_roles",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_member_roles", x => new { x.OrganizationId, x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_organization_member_roles_organization_members_Organization~",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "organization_members",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_member_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO organization_member_roles ("OrganizationId", "UserId", "RoleId", "AssignedAt")
                SELECT "OrganizationId", "UserId", "RoleId", "JoinedAt"
                FROM organization_members;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_organization_members_roles_RoleId",
                table: "organization_members");

            migrationBuilder.DropIndex(
                name: "IX_organization_members_RoleId",
                table: "organization_members");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "organization_members");

            migrationBuilder.CreateTable(
                name: "invitation_org_roles",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_org_roles", x => new { x.InvitationId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_invitation_org_roles_invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invitation_org_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO invitation_org_roles ("InvitationId", "RoleId")
                SELECT "Id", "OrgRoleId"
                FROM invitations;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_invitations_roles_OrgRoleId",
                table: "invitations");

            migrationBuilder.DropIndex(
                name: "IX_invitations_OrgRoleId",
                table: "invitations");

            migrationBuilder.DropColumn(
                name: "OrgRoleId",
                table: "invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "IX_permissions_Code",
                table: "permissions");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "permissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "permissions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "permissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE permissions
                SET "Name" = CASE "Code"
                    WHEN 'org.manage' THEN 'Manage organization'
                    WHEN 'org.members.manage' THEN 'Manage members'
                    WHEN 'org.teams.manage' THEN 'Manage teams'
                    WHEN 'org.roles.manage' THEN 'Manage roles'
                    WHEN 'org.delete' THEN 'Delete organization'
                    WHEN 'team.manage' THEN 'Manage team'
                    WHEN 'team.members.manage' THEN 'Manage team members'
                    ELSE "Code"
                END;

                CREATE TEMP TABLE permission_remap (
                    old_id uuid NOT NULL,
                    organization_id uuid NOT NULL,
                    new_id uuid NOT NULL
                );

                INSERT INTO permission_remap (old_id, organization_id, new_id)
                SELECT p."Id", o."Id", gen_random_uuid()
                FROM permissions p
                CROSS JOIN organizations o;

                INSERT INTO permissions ("Id", "OrganizationId", "Name", "Code", "Description", "IsSystem", "CreatedAt")
                SELECT r.new_id, r.organization_id, p."Name", p."Code", p."Description", TRUE, CURRENT_TIMESTAMP
                FROM permission_remap r
                JOIN permissions p ON p."Id" = r.old_id;

                UPDATE role_permissions rp
                SET "PermissionId" = r.new_id
                FROM roles role
                JOIN permission_remap r ON r.organization_id = role."OrganizationId" AND r.old_id = rp."PermissionId"
                WHERE rp."RoleId" = role."Id";

                DELETE FROM permissions
                WHERE "OrganizationId" IS NULL;

                DROP TABLE permission_remap;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "permissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_OrganizationId_Name_Scope",
                table: "roles",
                columns: new[] { "OrganizationId", "Name", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_OrganizationId_Code",
                table: "permissions",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitation_org_roles_RoleId",
                table: "invitation_org_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_member_roles_RoleId",
                table: "organization_member_roles",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_permissions_organizations_OrganizationId",
                table: "permissions",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions",
                column: "PermissionId",
                principalTable: "permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_permissions_organizations_OrganizationId",
                table: "permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions");

            migrationBuilder.DropTable(
                name: "invitation_org_roles");

            migrationBuilder.DropTable(
                name: "organization_member_roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_OrganizationId_Name_Scope",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_permissions_OrganizationId_Code",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "permissions");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "roles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "permissions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "organization_members",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrgRoleId",
                table: "invitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Code",
                table: "permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_RoleId",
                table: "organization_members",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_OrgRoleId",
                table: "invitations",
                column: "OrgRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_invitations_roles_OrgRoleId",
                table: "invitations",
                column: "OrgRoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_organization_members_roles_RoleId",
                table: "organization_members",
                column: "RoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_role_permissions_permissions_PermissionId",
                table: "role_permissions",
                column: "PermissionId",
                principalTable: "permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
