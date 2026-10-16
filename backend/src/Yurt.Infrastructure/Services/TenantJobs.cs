using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Runs a background job once per active cafe. Each cafe gets its own scope with the tenant context set, so the
/// database filters and every service in the scope see that cafe only. A failure in one cafe is logged and does not
/// stop the others. With no tenants in the table it runs for the default tenant (fail-open, like the request path).
/// </summary>
public static class TenantJobs
{
    public static async Task ForEachTenantAsync(
        IServiceScopeFactory scopes, ILogger logger,
        Func<IServiceProvider, CancellationToken, Task> work, CancellationToken ct)
    {
        IReadOnlyList<Tenant> tenants;
        await using (var lookup = scopes.CreateAsyncScope())
            tenants = await lookup.ServiceProvider.GetRequiredService<ITenantDirectory>().GetActiveAsync(ct);
        var targets = tenants.Count > 0
            ? tenants.Select(t => (t.Id, t.Slug)).ToList()
            : [(Tenant.DefaultId, Tenant.DefaultSlug)];

        foreach (var (id, slug) in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(id, slug);
                await work(scope.ServiceProvider, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job failed for tenant {Tenant}", slug);
            }
        }
    }
}
