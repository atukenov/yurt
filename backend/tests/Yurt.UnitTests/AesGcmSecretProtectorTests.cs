using Microsoft.Extensions.Configuration;
using Yurt.Infrastructure.Services;

namespace Yurt.UnitTests;

public class AesGcmSecretProtectorTests
{
    private static AesGcmSecretProtector With(string? key)
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Secrets:MasterKey"] = key }).Build());

    private static readonly string KeyA = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string KeyB = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    [Fact]
    public void RoundTrips_AndDoesNotStorePlainText()
    {
        var p = With(KeyA);
        var tenant = Guid.NewGuid();
        var cipher = p.Protect(tenant, "Iiko:ApiKey", "super-secret");
        Assert.DoesNotContain("super-secret", cipher);
        Assert.Equal("super-secret", p.Unprotect(tenant, "Iiko:ApiKey", cipher));
    }

    [Fact]
    public void SamePlainText_GivesDifferentCipherText()
    {
        var p = With(KeyA);
        var t = Guid.NewGuid();
        Assert.NotEqual(p.Protect(t, "k", "v"), p.Protect(t, "k", "v"));
    }

    [Fact]
    public void ValueMovedToAnotherCafeOrKey_CannotBeRead()
    {
        var p = With(KeyA);
        var cipher = p.Protect(Guid.NewGuid(), "Iiko:ApiKey", "v");
        Assert.Null(p.Unprotect(Guid.NewGuid(), "Iiko:ApiKey", cipher));
        Assert.Null(With(KeyA).Unprotect(Guid.Empty, "Other:Key", cipher));
    }

    [Fact]
    public void WrongMasterKeyOrGarbage_ReturnsNull()
    {
        var t = Guid.NewGuid();
        var cipher = With(KeyA).Protect(t, "k", "v");
        Assert.Null(With(KeyB).Unprotect(t, "k", cipher));
        Assert.Null(With(KeyA).Unprotect(t, "k", "not-a-cipher"));
        Assert.Null(With(KeyA).Unprotect(t, "k", "v1:!!!"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("c2hvcnQ=")] // not 32 bytes
    [InlineData("not base64")]
    public void MissingOrBadMasterKey_IsUnavailable(string? key)
    {
        var p = With(key);
        Assert.False(p.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => p.Protect(Guid.NewGuid(), "k", "v"));
    }
}
