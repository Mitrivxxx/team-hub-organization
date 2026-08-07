namespace team_hub_organization.Models;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public Guid UserId { get; set; }
    public string RequestPath { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseContentType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
