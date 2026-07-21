namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationAvatarValidationException : Exception
{
    public OrganizationAvatarValidationException(string message) : base(message)
    {
    }
}
