using team_hub_organization.Controllers;
using team_hub_organization.Services;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers.Organizations;

public partial class OrganizationsController(
    IOrganizationService organizationService,
    IOrganizationAvatarService organizationAvatarService,
    ICurrentUserService currentUserService,
    ILogger<OrganizationsController> logger) : OrganizationApiController;
