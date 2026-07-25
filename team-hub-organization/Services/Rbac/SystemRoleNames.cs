namespace team_hub_organization.Services.Rbac;

public static class SystemRoleNames
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string TeamLead = "TeamLead";

    public static bool IsSystemOrgRole(string name) =>
        name is Owner or Admin or Member;

    public static bool IsSystemTeamRole(string name) =>
        name is TeamLead or Member;
}
