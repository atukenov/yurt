using Serilog.Context;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Tenants;
using Yurt.Domain.Entities;

namespace Yurt.WebApi.Middleware;

/// <summary>
/// Works out which cafe the request is for (by host name) and puts it in <see cref="ITenantContext"/>.
/// Nothing filters by tenant yet; later Phase 5 steps read the context.
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenant, ITenantDirectory directory, ILogger<TenantMiddleware> logger)
    {
        IReadOnlyList<Tenant> tenants;
        try { tenants = await directory.GetActiveAsync(ctx.RequestAborted); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-open: a database hiccup must not take the current cafe offline, it just stays on the default tenant.
            logger.LogWarning(ex, "Could not load tenants, using the default tenant.");
            tenants = [];
        }
        var (id, slug) = TenantResolver.Resolve(tenants, ctx.Request.Host.Host);
        tenant.Set(id, slug);
        ctx.Items["TenantSlug"] = slug;

        using (LogContext.PushProperty("Tenant", slug))
            await next(ctx);
    }
}
