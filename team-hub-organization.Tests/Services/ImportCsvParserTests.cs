using System.Text;
using team_hub_organization.Services.Members.ImportExport;

namespace team_hub_organization.Tests.Services;

public class ImportCsvParserTests
{
    [Fact]
    public void Parse_ValidCsv_ReturnsRows()
    {
        var csv = "email,username,org_roles,team,team_role,job_title\n" +
                  "a@example.com,,Member,Eng,Member,Dev\n" +
                  ",bob,Admin,,,,\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = ImportCsvParser.Parse(stream);

        Assert.Equal(2, rows.Count);
        Assert.Equal("a@example.com", rows[0].Email);
        Assert.Equal("Member", rows[0].OrgRoles);
        Assert.Equal("Eng", rows[0].Team);
        Assert.Equal("bob", rows[1].Username);
        Assert.Equal("Admin", rows[1].OrgRoles);
    }

    [Fact]
    public void Parse_HeaderAliases_Accepted()
    {
        var csv = "E-mail,User,Roles\nuser@x.com,alice,Member\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = ImportCsvParser.Parse(stream);

        Assert.Single(rows);
        Assert.Equal("user@x.com", rows[0].Email);
        Assert.Equal("alice", rows[0].Username);
        Assert.Equal("Member", rows[0].OrgRoles);
    }

    [Fact]
    public void Parse_MissingIdentityColumn_Throws()
    {
        var csv = "org_roles\nMember\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        Assert.Throws<InvalidOperationException>(() => ImportCsvParser.Parse(stream));
    }

    [Fact]
    public void BuildErrorCsv_IncludesErrorColumns()
    {
        var row = new ImportCsvRow
        {
            RowNumber = 2,
            Email = "missing@example.com",
            OrgRoles = "Member"
        };

        var csv = ImportCsvParser.BuildErrorCsv([(row, "user_not_found", "User does not exist.")]);

        Assert.Contains("error_code", csv);
        Assert.Contains("user_not_found", csv);
        Assert.Contains("missing@example.com", csv);
    }
}
