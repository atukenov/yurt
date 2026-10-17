using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Tenants;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Services;

namespace Yurt.WebApi.Middleware;

/// <summary>Rejects a signed-in request whose token was issued for another cafe than the one the host belongs to.</summary>
public class TenantTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenant)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var isSuperAdmin = ctx.User.IsInRole(AdminRole.SuperAdmin.ToString());
            if (!TenantTokenPolicy.IsAllowed(ctx.User.FindFirstValue(JwtTokenService.TenantClaim), tenant.TenantId, isSuperAdmin))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 401,
                    Title = "Wrong cafe.",
                    Detail = "This sign-in belongs to a different cafe. Sign in again.",
                    Instance = ctx.Request.Path
                });
                return;
            }
        }
        await next(ctx);
    }
}
