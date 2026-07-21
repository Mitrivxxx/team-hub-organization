using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Services;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

[ApiController]
[Route("api/team/organizations")]
[Authorize]
public partial class OrganizationsController(
    IOrganizationService organizationService,
    IOrganizationAvatarService organizationAvatarService,
    ICurrentUserService currentUserService,
    ILogger<OrganizationsController> logger) : ControllerBase;
