namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationAvatarStorageUnavailableException : Exception
{
    public OrganizationAvatarStorageUnavailableException()
        : base("Blob storage is not configured.")
    {
    }
}
