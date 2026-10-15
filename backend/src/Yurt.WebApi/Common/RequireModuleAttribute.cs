using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Yurt.Application.Common.Interfaces;

namespace Yurt.WebApi.Common;

/// <summary>
/// Blocks an endpoint (or a whole controller) while the named module is switched off. Responds
/// <c>403 { code: "module_disabled", module }</c>. Fail-open: if the gate cannot decide, the request goes through.
/// Not applied to any endpoint yet; each module is gated in its own release (Phase 3).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireModuleAttribute : Attribute, IAsyncActionFilter
{
    public string ModuleKey { get; }

    public RequireModuleAttribute(string moduleKey) => ModuleKey = moduleKey;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var gate = context.HttpContext.RequestServices.GetRequiredService<IModuleGate>();
        if (await gate.IsEnabledAsync(ModuleKey, context.HttpContext.RequestAborted))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new { code = "module_disabled", module = ModuleKey })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
