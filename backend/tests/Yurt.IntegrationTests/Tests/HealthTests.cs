using System.Net;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class HealthTests(YurtWebAppFactory factory)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Health_probes_answer_without_login(string url)
    {
        var res = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Healthy", await res.Content.ReadAsStringAsync());
    }
}
