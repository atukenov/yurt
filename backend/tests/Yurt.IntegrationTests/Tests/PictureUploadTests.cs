using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Uploading logos and menu photos, and serving them only under the link of the cafe that owns them.</summary>
[Collection("Integration")]
public class PictureUploadTests(YurtWebAppFactory factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3];

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username: $"pic-adm-{Guid.NewGuid():N}"[..20]));
        return client;
    }

    private static MultipartFormDataContent Form(byte[] bytes, string type = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { file, "file", "logo.png" } };
    }

    [Fact]
    public async Task Upload_ReturnsPublicLink_ThatServesTheSameBytes_WithoutSignIn()
    {
        var admin = await AdminAsync();
        var res = await admin.PostAsync("/api/v1/admin/pictures", Form(Png));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var path = body.GetProperty("path").GetString()!;
        Assert.StartsWith($"/api/v1/app/pictures/{Tenant.DefaultId}/", path);

        var get = await factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await get.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", get.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Upload_RefusesNonPictures_EvenWhenTheBrowserSaysPng()
    {
        var admin = await AdminAsync();
        var html = "<svg onload=alert(1)>"u8.ToArray();
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await admin.PostAsync("/api/v1/admin/pictures", Form(html))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/pictures", new MultipartFormDataContent())).StatusCode);

        var big = new byte[2 * 1024 * 1024 + 1];
        Png.CopyTo(big, 0);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await admin.PostAsync("/api/v1/admin/pictures", Form(big))).StatusCode);
    }

    [Fact]
    public async Task Upload_NeedsStaffSignIn()
    {
        var res = await factory.CreateClient().PostAsync("/api/v1/admin/pictures", Form(Png));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AnotherCafesPicture_IsNotFound_UnderThisCafesLink_AndNotListedForThisCafe()
    {
        var slug = $"pic-{Guid.NewGuid():N}"[..12];
        Guid otherCafe;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = new Tenant { Slug = slug, Name = slug };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            otherCafe = tenant.Id;
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();

        Guid pictureId;
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(otherCafe, slug);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var picture = new Picture { ContentType = "image/png", Data = Png, Size = Png.Length };
            db.Pictures.Add(picture);
            await db.SaveChangesAsync();
            pictureId = picture.Id;
        }

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/app/pictures/{otherCafe}/{pictureId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/app/pictures/{Tenant.DefaultId}/{pictureId}")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant.DefaultId, "default");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Pictures.AnyAsync(p => p.Id == pictureId));
        }
    }
}
