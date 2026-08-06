using team_hub_organization.Dtos;

namespace team_hub_organization.Seeding.Internal;

public static class DemoOrganizationCatalog
{
    public sealed record CompanyProfile(
        string Name,
        string Description,
        OrganizationAddressDto Address);

    public sealed record TeamProfile(string Name, string Description);

    static readonly CompanyProfile[] Companies =
    [
        new(
            "Wilk Technologies Sp. z o.o.",
            "Software house delivering team collaboration and internal tooling for mid-size companies.",
            new OrganizationAddressDto { Country = "Poland", City = "Warsaw", PostalCode = "00-850" }),
        new(
            "Baltic Cloud Solutions S.A.",
            "Cloud infrastructure and DevOps consulting for regulated industries in Central Europe.",
            new OrganizationAddressDto { Country = "Poland", City = "Gdańsk", PostalCode = "80-809" }),
        new(
            "Kraków Data Labs Sp. z o.o.",
            "Analytics platform and data engineering services for retail and logistics clients.",
            new OrganizationAddressDto { Country = "Poland", City = "Kraków", PostalCode = "31-150" }),
        new(
            "Poznań Product Studio Sp. z o.o.",
            "Product design and full-stack delivery for B2B SaaS startups.",
            new OrganizationAddressDto { Country = "Poland", City = "Poznań", PostalCode = "61-704" }),
        new(
            "Silesia Secure Systems Sp. z o.o.",
            "Identity, access management, and security operations for enterprise customers.",
            new OrganizationAddressDto { Country = "Poland", City = "Katowice", PostalCode = "40-098" })
    ];

    static readonly TeamProfile[] Teams =
    [
        new("Engineering", "Backend, frontend, and platform engineering."),
        new("Product", "Product management, design, and discovery."),
        new("Operations", "Customer success, support, and internal ops."),
        new("Growth", "Marketing, sales enablement, and partnerships."),
        new("People", "People ops, recruiting, and workplace experience.")
    ];

    static readonly string[] JobTitles =
    [
        "Backend Developer",
        "Frontend Developer",
        "Full-Stack Developer",
        "Product Manager",
        "UX Designer",
        "DevOps Engineer",
        "QA Engineer",
        "Data Analyst",
        "Customer Success Manager",
        "Technical Writer"
    ];

    static readonly string[] InvitationEmails =
    [
        "anna.kowalska@example.com",
        "piotr.nowak@example.com",
        "magdalena.wisniewska@example.com",
        "tomasz.wojcik@example.com",
        "katarzyna.kaminska@example.com"
    ];

    /// <summary>1-based organization index.</summary>
    public static CompanyProfile GetCompany(int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index));

        return Companies[(index - 1) % Companies.Length];
    }

    /// <summary>1-based team index within an organization.</summary>
    public static TeamProfile GetTeam(int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index));

        return Teams[(index - 1) % Teams.Length];
    }

    public static string GetJobTitle(int ordinal) =>
        JobTitles[Math.Abs(ordinal) % JobTitles.Length];

    public static string GetInvitationEmail(int ordinal)
    {
        if (ordinal >= 0 && ordinal < InvitationEmails.Length)
            return InvitationEmails[ordinal];

        return $"invite{ordinal:D3}@example.com";
    }
}
