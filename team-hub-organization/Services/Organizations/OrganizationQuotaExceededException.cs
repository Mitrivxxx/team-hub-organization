namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationQuotaExceededException : Exception
{
    public OrganizationQuotaExceededException(string message) : base(message) { }
}
