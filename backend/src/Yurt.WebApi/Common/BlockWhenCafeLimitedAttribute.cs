using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Yurt.Application.Features.Tenants;

namespace Yurt.WebApi.Common;

/// <summary>
/// Refuses an endpoint that starts a new order while the cafe is in limited mode (stopped paying). Responds
/// <c>403 { code: "cafe_limited", title }</c>. Sign-in, menus and order history are never marked with it.
/// Fail-open like the module gate: if the state cannot be read, the request goes through.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BlockWhenCafeLimitedAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = context.HttpContext.RequestServices.GetRequiredService<CafeAccessService>();
        if (!(await access.GetAsync(context.HttpContext.RequestAborted)).IsLimited)
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new { code = CafeAccess.RefusedCode, title = CafeAccess.RefusedMessage })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
