using Microsoft.AspNetCore.Mvc;
using Voot.CodeGen.Domain.Common;

namespace Voot.CodeGen.Web.Filters;

public static class DomainRuleExtensions
{
    /// <summary>
    /// Runs a service call from a form action. A rule violation becomes a message on the form
    /// and the call returns default, so the action can check <c>ModelState</c> and show the
    /// form again with what the user entered. Missing and forbidden still propagate.
    /// </summary>
    public static async Task<T?> TryDomainAsync<T>(this Controller controller, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (DomainException ex) when (ex is not NotFoundException and not ForbiddenException)
        {
            controller.ModelState.AddModelError(string.Empty, ex.Message);
            return default;
        }
    }

    /// <inheritdoc cref="TryDomainAsync{T}(Controller, Func{Task{T}})"/>
    public static Task TryDomainAsync(this Controller controller, Func<Task> call) =>
        controller.TryDomainAsync(async () =>
        {
            await call();
            return true;
        });
}
