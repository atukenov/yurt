using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The cached customer menu is kept per cafe, so one cafe never sees another's items.</summary>
[Collection("Integration")]
public class MenuCacheTenantTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task CachedMenu_OfOneCafe_IsNotServedToAnother()
    {
        Guid otherCafe;
        string slug = $"mc-{Guid.NewGuid():N}"[..12];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cafe = new Tenant { Slug = slug, Name = slug };
            db.Tenants.Add(cafe);
            await db.SaveChangesAsync();
            otherCafe = cafe.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant.DefaultId, "default");
            var first = await scope.ServiceProvider.GetRequiredService<MenuService>().GetItemsAsync(null, null, "en");
            Assert.NotEmpty(first); // now cached
        }

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(otherCafe, slug);
            var theirs = await scope.ServiceProvider.GetRequiredService<MenuService>().GetItemsAsync(null, null, "en");
            Assert.Empty(theirs); // the new cafe has no menu yet
        }
    }
}
