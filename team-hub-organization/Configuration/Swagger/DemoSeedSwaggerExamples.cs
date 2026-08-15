using System.Text.Json.Nodes;
using team_hub_organization.Seeding.Internal;

namespace team_hub_organization.Configuration.Swagger;

/// <summary>
/// Copy-paste request examples aligned with demo seed data.
/// Guid fields are placeholders — replace from GET after login as JanWilk123.
/// </summary>
public static class DemoSeedSwaggerExamples
{
    /// <summary>Replace with ORG Member role id from GET /{orgId}/roles.</summary>
    public static readonly Guid PlaceholderOrgMemberRoleId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");

    /// <summary>Replace with ORG Admin role id from GET /{orgId}/roles.</summary>
    public static readonly Guid PlaceholderOrgAdminRoleId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2");

    /// <summary>Replace with TEAM Member role id from GET /{orgId}/roles?scope=TEAM (or list roles).</summary>
    public static readonly Guid PlaceholderTeamMemberRoleId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");

    /// <summary>Replace with TEAM TeamLead role id from GET /{orgId}/roles.</summary>
    public static readonly Guid PlaceholderTeamLeadRoleId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");

    /// <summary>Replace with a seeded member userId from GET /{orgId}/members — or addMemberUserId from demo/context for AddMember.</summary>
    public static readonly Guid PlaceholderDemoUserId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");

    /// <summary>Replace with Engineering team id from GET /{orgId}/teams (seeded name Engineering).</summary>
    public static readonly Guid PlaceholderTeamId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd1");

    /// <summary>Replace with a permission id from GET /{orgId}/permissions (e.g. org.teams.manage).</summary>
    public static readonly Guid PlaceholderPermissionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1");

    public const string DevWorkflowMarkdown =
        """
        ## Demo seed — quick try-out

        1. Seed auth (`--seed`), start auth, then seed organization (`--seed`).
        2. Login **JanWilk123** / **janwilk123**, authorize Swagger with the JWT Bearer token.
        3. **`GET /api/organizations/v1/demo/context`** (optional `?index=1`) → copy real Guids from the response:
           - `organizationId` → path `orgId`
           - `roles.orgMember` / `roles.orgAdmin` / `roles.teamMember` / `roles.teamLead` → request role ids
           - `teams[0].id` (Engineering) → `teamId`
           - `sampleMemberUserIds[0]` → member/team-member `userId`
           - `addMemberUserId` → `POST /members` body (not already in org)
           - `permissions[].id` (e.g. `org.teams.manage`) → assign-permission examples
           - `pendingInvitations[].id` → invitation path ids
        4. Replace Guid placeholders in request examples (`aaaaaaaa-…`, …) with values from step 3, then Execute.

        Notes:
        - Create-invitation example uses `magdalena.wisniewska@example.com` (seed already has anna/piotr pending).
        - Transfer ownership has no auto-filled example (destructive).
        - `demo/context` is Development/Staging only.

        Seeded Dev defaults: 1 org (`demo-org-1`), 15 members, 2 Admins, teams Engineering + Product, 2 pending invitations.
        """;

    public static JsonNode CreateOrganization()
    {
        // Company index 2 — not the default seeded org (index 1), safe to create while demo-org-1 exists.
        var company = DemoOrganizationCatalog.GetCompany(2);
        return JsonNode.Parse(
            $$"""
            {
              "name": "{{Escape(company.Name)}}",
              "slug": "baltic-cloud-demo",
              "description": "{{Escape(company.Description)}}",
              "nip": "5000000099",
              "address": {
                "country": "{{Escape(company.Address.Country)}}",
                "city": "{{Escape(company.Address.City)}}",
                "postalCode": "{{Escape(company.Address.PostalCode)}}"
              }
            }
            """)!;
    }

    public static JsonNode UpdateOrganization()
    {
        var company = DemoOrganizationCatalog.GetCompany(1);
        return JsonNode.Parse(
            $$"""
            {
              "description": "{{Escape(company.Description)}} Updated via Swagger demo.",
              "address": {
                "country": "Poland",
                "city": "Warsaw",
                "postalCode": "00-851"
              }
            }
            """)!;
    }

    public static JsonNode CreateTeam()
    {
        // Growth is not created in Dev seed (only Engineering + Product) — safe create example.
        var team = DemoOrganizationCatalog.GetTeam(4);
        return JsonNode.Parse(
            $$"""
            {
              "name": "{{Escape(team.Name)}}",
              "description": "{{Escape(team.Description)}}"
            }
            """)!;
    }

    public static JsonNode UpdateTeam() =>
        JsonNode.Parse(
            """
            {
              "description": "Backend, frontend, platform engineering, and shared libraries."
            }
            """)!;

    public static JsonNode AddMember() =>
        JsonNode.Parse(
            $$"""
            {
              "userId": "{{PlaceholderDemoUserId}}",
              "roleIds": ["{{PlaceholderOrgMemberRoleId}}"]
            }
            """)!;

    public static JsonNode UpdateMember() =>
        JsonNode.Parse(
            $$"""
            {
              "roleIds": ["{{PlaceholderOrgAdminRoleId}}"]
            }
            """)!;

    public static JsonNode TransferOwnership() =>
        JsonNode.Parse(
            $$"""
            {
              "newOwnerUserId": "{{PlaceholderDemoUserId}}"
            }
            """)!;

    public static JsonNode AddTeamMember() =>
        JsonNode.Parse(
            $$"""
            {
              "userId": "{{PlaceholderDemoUserId}}",
              "roleId": "{{PlaceholderTeamMemberRoleId}}",
              "jobTitle": "{{Escape(DemoOrganizationCatalog.GetJobTitle(0))}}"
            }
            """)!;

    public static JsonNode UpdateTeamMember() =>
        JsonNode.Parse(
            $$"""
            {
              "roleId": "{{PlaceholderTeamLeadRoleId}}",
              "jobTitle": "{{Escape(DemoOrganizationCatalog.GetJobTitle(3))}}"
            }
            """)!;

    public static JsonNode CreateInvitation() =>
        JsonNode.Parse(
            $$"""
            {
              "email": "{{Escape(DemoOrganizationCatalog.GetInvitationEmail(2))}}",
              "orgRoleIds": ["{{PlaceholderOrgMemberRoleId}}"],
              "teamId": "{{PlaceholderTeamId}}",
              "teamRoleId": "{{PlaceholderTeamMemberRoleId}}"
            }
            """)!;

    public static JsonNode CreateRole() =>
        JsonNode.Parse(
            """
            {
              "name": "Observer",
              "description": "Read-only access for auditors (demo seed example).",
              "scope": "ORG"
            }
            """)!;

    public static JsonNode UpdateRole() =>
        JsonNode.Parse(
            """
            {
              "description": "Read-only access for auditors and compliance reviews."
            }
            """)!;

    public static JsonNode AssignRolePermissions() =>
        JsonNode.Parse(
            $$"""
            {
              "permissionIds": ["{{PlaceholderPermissionId}}"]
            }
            """)!;

    public static JsonNode AssignRoleMember() =>
        JsonNode.Parse(
            $$"""
            {
              "userId": "{{PlaceholderDemoUserId}}"
            }
            """)!;

    public static JsonNode CreatePermission() =>
        JsonNode.Parse(
            """
            {
              "name": "View billing",
              "code": "org.billing.view",
              "description": "Custom permission example for Swagger try-out."
            }
            """)!;

    public static JsonNode UpdatePermission() =>
        JsonNode.Parse(
            """
            {
              "name": "View billing reports",
              "description": "Allows viewing billing reports without manage rights."
            }
            """)!;

    public static JsonNode CreateExport() =>
        JsonNode.Parse(
            """
            {
              "format": "csv",
              "datasets": ["members", "teams", "roles", "permissions", "organization"]
            }
            """)!;

    static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal);
}
