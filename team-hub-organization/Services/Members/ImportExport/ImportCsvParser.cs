using System.Text;

namespace team_hub_organization.Services.Members.ImportExport;

public sealed class ImportCsvRow
{
    public int RowNumber { get; init; }
    public string? Email { get; init; }
    public string? Username { get; init; }
    public string? OrgRoles { get; init; }
    public string? Team { get; init; }
    public string? TeamRole { get; init; }
    public string? JobTitle { get; init; }
    public IReadOnlyDictionary<string, string> Raw { get; init; } = new Dictionary<string, string>();
}

public static class ImportCsvParser
{
    public const int MaxRows = 5000;

    static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "email",
        ["e-mail"] = "email",
        ["mail"] = "email",
        ["username"] = "username",
        ["user"] = "username",
        ["user_name"] = "username",
        ["org_roles"] = "org_roles",
        ["orgroles"] = "org_roles",
        ["roles"] = "org_roles",
        ["role"] = "org_roles",
        ["team"] = "team",
        ["team_name"] = "team",
        ["team_role"] = "team_role",
        ["teamrole"] = "team_role",
        ["job_title"] = "job_title",
        ["jobtitle"] = "job_title",
        ["title"] = "job_title"
    };

    public static IReadOnlyList<ImportCsvRow> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var headerLine = reader.ReadLine()
            ?? throw new InvalidOperationException("CSV file is empty.");

        var headers = SplitCsvLine(headerLine);
        if (headers.Count == 0)
            throw new InvalidOperationException("CSV header row is empty.");

        var columnMap = new Dictionary<int, string>();
        for (var i = 0; i < headers.Count; i++)
        {
            var raw = headers[i].Trim();
            if (HeaderAliases.TryGetValue(raw, out var canonical))
                columnMap[i] = canonical;
        }

        if (!columnMap.Values.Contains("email") && !columnMap.Values.Contains("username"))
            throw new InvalidOperationException("CSV must include an email or username column.");

        if (!columnMap.Values.Contains("org_roles"))
            throw new InvalidOperationException("CSV must include an org_roles column.");

        var rows = new List<ImportCsvRow>();
        var rowNumber = 1;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (rows.Count >= MaxRows)
                throw new InvalidOperationException($"CSV exceeds maximum of {MaxRows} data rows.");

            var cells = SplitCsvLine(line);
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? email = null, username = null, orgRoles = null, team = null, teamRole = null, jobTitle = null;

            foreach (var (index, canonical) in columnMap)
            {
                var value = index < cells.Count ? cells[index].Trim() : "";
                raw[canonical] = value;
                switch (canonical)
                {
                    case "email": email = NullIfEmpty(value); break;
                    case "username": username = NullIfEmpty(value); break;
                    case "org_roles": orgRoles = NullIfEmpty(value); break;
                    case "team": team = NullIfEmpty(value); break;
                    case "team_role": teamRole = NullIfEmpty(value); break;
                    case "job_title": jobTitle = NullIfEmpty(value); break;
                }
            }

            rows.Add(new ImportCsvRow
            {
                RowNumber = rowNumber,
                Email = email,
                Username = username,
                OrgRoles = orgRoles,
                Team = team,
                TeamRole = teamRole,
                JobTitle = jobTitle,
                Raw = raw
            });
        }

        return rows;
    }

    public static string BuildErrorCsv(IEnumerable<(ImportCsvRow Row, string Code, string Message)> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("email,username,org_roles,team,team_role,job_title,error_code,error_message");
        foreach (var (row, code, message) in errors)
        {
            sb.Append(Escape(row.Email)).Append(',')
                .Append(Escape(row.Username)).Append(',')
                .Append(Escape(row.OrgRoles)).Append(',')
                .Append(Escape(row.Team)).Append(',')
                .Append(Escape(row.TeamRole)).Append(',')
                .Append(Escape(row.JobTitle)).Append(',')
                .Append(Escape(code)).Append(',')
                .Append(Escape(message))
                .AppendLine();
        }

        return sb.ToString();
    }

    public static string TemplateCsv =>
        "email,username,org_roles,team,team_role,job_title\n" +
        "user@example.com,,Member,Engineering,Member,Developer\n";

    static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static string Escape(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return value;
    }

    static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
