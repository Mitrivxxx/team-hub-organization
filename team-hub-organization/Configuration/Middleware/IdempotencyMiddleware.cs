using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamHub.Observability;
using team_hub_organization.Data;
using team_hub_organization.Models;
using team_hub_organization.Services;

namespace team_hub_organization.Configuration.Middleware;

/// <summary>Optional Idempotency-Key for create org, invite, and import.</summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";

    public async Task InvokeAsync(HttpContext context, OrganizationDbContext db, ICurrentUserService currentUser)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !TryMatchPath(context.Request.Path, out _)
            || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString().Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request.",
                Detail = "Idempotency-Key must be 1..128 characters.",
                Type = ProblemTypes.ValidationFailed
            });
            return;
        }

        Guid userId;
        try
        {
            userId = currentUser.GetRequiredUserId();
        }
        catch
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
            context.Request.Body.Position = 0;
        }

        var requestPath = context.Request.Path.Value ?? "";
        var requestHash = ComputeHash(body);

        var existing = await db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.UserId == userId && r.Key == key && r.RequestPath == requestPath,
                context.RequestAborted);

        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict.",
                    Detail = "Idempotency-Key was reused with a different request body.",
                    Type = ProblemTypes.Conflict
                });
                return;
            }

            context.Response.StatusCode = existing.ResponseStatus;
            if (!string.IsNullOrEmpty(existing.ResponseContentType))
                context.Response.ContentType = existing.ResponseContentType;
            if (!string.IsNullOrEmpty(existing.ResponseBody))
                await context.Response.WriteAsync(existing.ResponseBody, context.RequestAborted);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    UserId = userId,
                    RequestPath = requestPath,
                    RequestHash = requestHash,
                    ResponseStatus = context.Response.StatusCode,
                    ResponseBody = string.IsNullOrWhiteSpace(responseBody) ? null : responseBody,
                    ResponseContentType = context.Response.ContentType,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                try
                {
                    await db.SaveChangesAsync(context.RequestAborted);
                }
                catch (DbUpdateException)
                {
                    // Concurrent duplicate key — ignore; next request will replay.
                }
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    static bool TryMatchPath(PathString path, out string kind)
    {
        kind = "";
        var value = path.Value ?? "";
        // /api/organizations/v1
        if (!value.StartsWith("/api/organizations/v1", StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = value["/api/organizations/v1".Length..];
        if (remainder is "" or "/")
        {
            kind = "create-org";
            return true;
        }

        // /{orgId}/invitations
        if (System.Text.RegularExpressions.Regex.IsMatch(
                remainder,
                @"^/[0-9a-fA-F-]{36}/invitations/?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            kind = "invite";
            return true;
        }

        // /{orgId}/imports
        if (System.Text.RegularExpressions.Regex.IsMatch(
                remainder,
                @"^/[0-9a-fA-F-]{36}/imports/?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            kind = "import";
            return true;
        }

        return false;
    }

    static string ComputeHash(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
