using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ServiceDesk.Web.Security;

// AGENTS.md §11: a page refused by its [Authorize(Roles = …)] (AGENTS.md §8, level 1) is a denied access, logged as a
// Warning. The framework logs it at Information only, which the "Microsoft.AspNetCore": "Warning" filter hides. The
// default handler still answers: the forbid leads to the AccessDenied page, the challenge of an anonymous user to the
// login page (that is not a denial, so it is not logged).
public sealed partial class AccessDeniedLoggingResultHandler(ILogger<AccessDeniedLoggingResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Forbidden)
        {
            // Only the Id and the path (without the query string); no cookie, no claim values beyond the Id.
            LogPageAccessDenied(logger, context.Request.Path.Value ?? string.Empty, context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Page {Path} denied access to user {UserId}")]
    private static partial void LogPageAccessDenied(ILogger logger, string path, string? userId);
}
