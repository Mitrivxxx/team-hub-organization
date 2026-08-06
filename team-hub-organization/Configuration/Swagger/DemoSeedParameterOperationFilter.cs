using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace team_hub_organization.Configuration.Swagger;

/// <summary>
/// Documents path parameters for demo-seed try-out (orgId / teamId / userId).
/// </summary>
public sealed class DemoSeedParameterOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null)
            return;

        foreach (var parameter in operation.Parameters)
        {
            if (parameter is not OpenApiParameter openApiParameter)
                continue;

            var name = openApiParameter.Name;
            if (string.Equals(name, "orgId", StringComparison.OrdinalIgnoreCase))
            {
                AppendDescription(
                    openApiParameter,
                    "After demo seed: GET /demo/context → organizationId (slug demo-org-1).");
            }
            else if (string.Equals(name, "teamId", StringComparison.OrdinalIgnoreCase))
            {
                AppendDescription(
                    openApiParameter,
                    "After demo seed: GET /demo/context → teams[].id (Engineering/Product).");
            }
            else if (string.Equals(name, "userId", StringComparison.OrdinalIgnoreCase))
            {
                AppendDescription(
                    openApiParameter,
                    "After demo seed: GET /demo/context → sampleMemberUserIds or addMemberUserId.");
            }
            else if (string.Equals(name, "roleId", StringComparison.OrdinalIgnoreCase))
            {
                AppendDescription(
                    openApiParameter,
                    "After demo seed: GET /demo/context → roles.* (org/team system roles).");
            }
            else if (string.Equals(name, "invitationId", StringComparison.OrdinalIgnoreCase))
            {
                AppendDescription(
                    openApiParameter,
                    "After demo seed: GET /demo/context → pendingInvitations[].id.");
            }
        }
    }

    static void AppendDescription(OpenApiParameter parameter, string hint)
    {
        parameter.Description = string.IsNullOrWhiteSpace(parameter.Description)
            ? hint
            : $"{parameter.Description} {hint}";
    }
}
