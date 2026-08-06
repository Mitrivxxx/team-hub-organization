using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using team_hub_organization.Dtos;

namespace team_hub_organization.Configuration.Swagger;

/// <summary>
/// Attaches demo-seed-aligned JSON examples to request DTOs for Swagger Try it out.
/// </summary>
public sealed class DemoSeedRequestExampleSchemaFilter : ISchemaFilter
{
    static readonly Dictionary<Type, Func<JsonNode>> Examples = new()
    {
        [typeof(CreateOrganizationRequest)] = DemoSeedSwaggerExamples.CreateOrganization,
        [typeof(UpdateOrganizationRequest)] = DemoSeedSwaggerExamples.UpdateOrganization,
        [typeof(CreateTeamRequest)] = DemoSeedSwaggerExamples.CreateTeam,
        [typeof(UpdateTeamRequest)] = DemoSeedSwaggerExamples.UpdateTeam,
        [typeof(AddMemberRequest)] = DemoSeedSwaggerExamples.AddMember,
        [typeof(UpdateMemberRequest)] = DemoSeedSwaggerExamples.UpdateMember,
        [typeof(AddTeamMemberRequest)] = DemoSeedSwaggerExamples.AddTeamMember,
        [typeof(UpdateTeamMemberRequest)] = DemoSeedSwaggerExamples.UpdateTeamMember,
        [typeof(CreateInvitationRequest)] = DemoSeedSwaggerExamples.CreateInvitation,
        [typeof(CreateRoleRequest)] = DemoSeedSwaggerExamples.CreateRole,
        [typeof(UpdateRoleRequest)] = DemoSeedSwaggerExamples.UpdateRole,
        [typeof(AssignRolePermissionsRequest)] = DemoSeedSwaggerExamples.AssignRolePermissions,
        [typeof(AssignRoleMemberRequest)] = DemoSeedSwaggerExamples.AssignRoleMember,
        [typeof(CreatePermissionRequest)] = DemoSeedSwaggerExamples.CreatePermission,
        [typeof(UpdatePermissionRequest)] = DemoSeedSwaggerExamples.UpdatePermission,
        [typeof(CreateExportRequest)] = DemoSeedSwaggerExamples.CreateExport
    };

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema openApiSchema)
            return;

        if (!Examples.TryGetValue(context.Type, out var factory))
            return;

        openApiSchema.Example = factory();
    }
}
