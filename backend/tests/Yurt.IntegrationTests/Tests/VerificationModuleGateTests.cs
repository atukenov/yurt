using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class VerificationModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task Verification_SwitchedOff_RegistrationSkipsTheCode_AndComesBackWhenOn()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var client = factory.CreateClient();

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Verification}", License(false));

            var off = await client.PostAsJsonAsync("/api/v1/auth/register/start", new
            {
                mobileNumber = "+77002990011", pin4 = "1234", firstName = "No", lastName = "Code"
            });
            Assert.True(off.IsSuccessStatusCode);
            var offBody = await off.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(offBody.GetProperty("verificationRequired").GetBoolean());
            Assert.False(string.IsNullOrEmpty(offBody.GetProperty("auth").GetProperty("accessToken").GetString()));
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Verification}", License(true));
        }

        var on = await client.PostAsJsonAsync("/api/v1/auth/register/start", new
        {
            mobileNumber = "+77002990012", pin4 = "1234", firstName = "With", lastName = "Code"
        });
        Assert.True(on.IsSuccessStatusCode);
        var onBody = await on.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(onBody.GetProperty("verificationRequired").GetBoolean());
    }
}
