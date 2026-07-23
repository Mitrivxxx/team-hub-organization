using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Services;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Controllers;

[ApiController]
[ApiVersion("0.1")]
[Route("api/organizations/v0.1.0")]
[Authorize]
public partial class OrganizationsController(
    IOrganizationService organizationService,
    IOrganizationAvatarService organizationAvatarService,
    ICurrentUserService currentUserService,
    ILogger<OrganizationsController> logger) : ControllerBase;
