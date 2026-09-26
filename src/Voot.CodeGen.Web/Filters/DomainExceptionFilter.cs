using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Web.Filters;

/// <summary>
/// Turns domain failures into the right HTTP response instead of a 500: a rule violation
/// becomes a message on the page the user came from, a missing entity a 404, and a denied
/// one a 403.
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Nothing to do before the action runs.
    }

    /// <summary>
    /// The referring page when it is on this site and is not the request that just failed
    /// (which would loop); otherwise the home page.
    /// </summary>
    private static string ReturnPath(HttpContext httpContext)
    {
        var request = httpContext.Request;

        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
            string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            var path = referer.PathAndQuery;
            var current = request.Path + request.QueryString;

            if (!(HttpMethods.IsGet(request.Method) && string.Equals(path, current, StringComparison.OrdinalIgnoreCase)))
            {
                return path;
            }
        }

        return "/";
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        switch (context.Exception)
        {
            case null:
                return;

            case NotFoundException notFound:
                logger.LogInformation("Returning 404: {Message}", notFound.Message);
                context.Result = new NotFoundObjectResult(notFound.Message);
                context.ExceptionHandled = true;
                return;

            case ForbiddenException forbidden:
                logger.LogWarning("Returning 403: {Message}", forbidden.Message);
                context.Result = new ForbidResult();
                context.ExceptionHandled = true;
                return;

            case DomainException domain:
                // Form actions catch rule violations themselves (TryDomainAsync) so the form comes
                // back with its input. Anywhere else, go back to the page the request came from
                // with the message, rather than to the error page.
                logger.LogInformation("Domain rule rejected the request: {Message}", domain.Message);

                if (context.Controller is Controller controller)
                {
                    controller.TempData["Error"] = domain.Message;
                }

                context.Result = new LocalRedirectResult(ReturnPath(context.HttpContext));
                context.ExceptionHandled = true;
                return;
        }
    }
}
