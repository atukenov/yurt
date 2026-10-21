using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class BrandSettingsTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task Brand_SavedBySuperAdmin_IsServedInAppConfig_AndHonoursBrandingModule()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var anon = factory.CreateClient();

        // Defaults before anything is saved
        var before = (await anon.GetFromJsonAsync<JsonElement>("/api/v1/app/config")).GetProperty("brand");
        Assert.Equal("#ffd119", before.GetProperty("primaryColor").GetString());

        // Only the platform operator can change it; bad values are rejected
        var denied = await admin.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Nope", primaryColor = "#112233", accentColor = "#445566" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var bad = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "blue", accentColor = "#445566" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badLogo = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", logoUrl = "http://insecure/logo.png" });
        Assert.Equal(HttpStatusCode.BadRequest, badLogo.StatusCode);
        var badDarkLogo = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", logoDarkUrl = "http://insecure/logo-dark.png" });
        Assert.Equal(HttpStatusCode.BadRequest, badDarkLogo.StatusCode);
        var badIcon = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", iconUrl = "http://insecure/icon.png" });
        Assert.Equal(HttpStatusCode.BadRequest, badIcon.StatusCode);
        var badSplash = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", splashUrl = "javascript:alert(1)" });
        Assert.Equal(HttpStatusCode.BadRequest, badSplash.StatusCode);
        var badLink = await super.PutAsJsonAsync("/api/v1/platform/brand", new
        {
            name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9",
            links = new[] { new { label = "Site", url = "http://insecure" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, badLink.StatusCode);
        var manyLinks = await super.PutAsJsonAsync("/api/v1/platform/brand", new
        {
            name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9",
            links = Enumerable.Range(0, 7).Select(i => new { label = $"L{i}", url = $"https://x.example/{i}" }).ToArray()
        });
        Assert.Equal(HttpStatusCode.BadRequest, manyLinks.StatusCode);
        var badRadius = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", radius = "pill" });
        Assert.Equal(HttpStatusCode.BadRequest, badRadius.StatusCode);
        var badFont = await super.PutAsJsonAsync("/api/v1/platform/brand",
            new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", font = "Comic Sans" });
        Assert.Equal(HttpStatusCode.BadRequest, badFont.StatusCode);
        foreach (var badFontUrl in new[] { "http://cdn.example.com/brand.woff2", "https://cdn.example.com/brand.png", "not a url" })
        {
            var res = await super.PutAsJsonAsync("/api/v1/platform/brand",
                new { name = "Blue Cafe", primaryColor = "#1d4ed8", accentColor = "#0ea5e9", fontUrl = badFontUrl });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        try
        {
            var ok = await super.PutAsJsonAsync("/api/v1/platform/brand", new
            {
                name = "Blue Cafe", primaryColor = "#1D4ED8", accentColor = "#0ea5e9",
                logoUrl = "https://cdn.example.com/logo.png", logoDarkUrl = "https://cdn.example.com/logo-dark.png", iconUrl = "https://cdn.example.com/icon.png",
                splashUrl = "https://cdn.example.com/splash.png", welcomeText = "Fresh coffee, every day",
                links = new[] { new { label = "Instagram", url = "https://instagram.com/bluecafe" }, new { label = "", url = "" } }, supportContact = "+7 700 000 00 00",
                radius = "round", font = "serif", fontUrl = " https://cdn.example.com/fonts/Brand.WOFF2?v=2 "
            });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            var served = (await anon.GetFromJsonAsync<JsonElement>("/api/v1/app/config")).GetProperty("brand");
            Assert.Equal("Blue Cafe", served.GetProperty("name").GetString());
            Assert.Equal("#1d4ed8", served.GetProperty("primaryColor").GetString());
            Assert.Equal("https://cdn.example.com/logo.png", served.GetProperty("logoUrl").GetString());
            Assert.Equal("https://cdn.example.com/logo-dark.png", served.GetProperty("logoDarkUrl").GetString());
            Assert.Equal("https://cdn.example.com/icon.png", served.GetProperty("iconUrl").GetString());
            Assert.Equal("https://cdn.example.com/splash.png", served.GetProperty("splashUrl").GetString());
            Assert.Equal("Fresh coffee, every day", served.GetProperty("welcomeText").GetString());
            Assert.Equal("round", served.GetProperty("radius").GetString());
            Assert.Equal("serif", served.GetProperty("font").GetString());
            Assert.Equal("https://cdn.example.com/fonts/Brand.WOFF2?v=2", served.GetProperty("fontUrl").GetString());
            var links = served.GetProperty("links");
            Assert.Equal(1, links.GetArrayLength()); // the blank row is dropped
            Assert.Equal("Instagram", links[0].GetProperty("label").GetString());

            // Branding module off: the apps get the Yurt default, the saved brand is kept
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Branding}", License(false));
            var off = (await anon.GetFromJsonAsync<JsonElement>("/api/v1/app/config")).GetProperty("brand");
            Assert.Equal("Yurt", off.GetProperty("name").GetString());
            Assert.Equal("#ffd119", off.GetProperty("primaryColor").GetString());
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Branding}", License(true));
            await super.PutAsJsonAsync("/api/v1/platform/brand",
                new { name = "Yurt", primaryColor = "#ffd119", accentColor = "#ea580c" });
        }

        var back = (await anon.GetFromJsonAsync<JsonElement>("/api/v1/app/config")).GetProperty("brand");
        Assert.Equal("Yurt", back.GetProperty("name").GetString());
    }
}
