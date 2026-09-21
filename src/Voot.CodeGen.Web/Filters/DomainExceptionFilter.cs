using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Web.Filters;

/// <summary>
/// Turns domain failures into the right HTTP response instead of a 500: a rule violation
/// becomes a message on the form the user just submitted, a missing entity a 404, and a
/// denied one a 403.
/// </summary>
public sealed class DomainExceptionFilter(ILogger<DomainExceptionFilter> logger) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Nothing to do before the action runs.
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
                // Re-render the submitted form with the message attached, when there is one.
                if (context.Controller is Controller controller && context.HttpContext.Request.Method == HttpMethods.Post)
                {
                    controller.ModelState.AddModelError(string.Empty, domain.Message);
                    controller.TempData["Error"] = domain.Message;
                }

                logger.LogInformation("Domain rule rejected the request: {Message}", domain.Message);
                return;
        }
    }
}
