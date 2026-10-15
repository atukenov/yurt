using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.AppUpdate;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class AppReleaseServiceTests
{
    private static AppReleaseService Build(IEnumerable<AppRelease> releases, AppUpdateOptions? options = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = releases.ToList().BuildMockDbSet();
        db.AppReleases.Returns(set);
        return new AppReleaseService(db, Substitute.For<IAuditLogService>(), options ?? new AppUpdateOptions());
    }

    [Theory]
    [InlineData("5.3.0", true)]
    [InlineData("5", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("", false)]
    [InlineData("5.x", false)]
    [InlineData("1.2.3.4.5", false)]
    public void IsValid_ChecksFormat(string v, bool expected) => Assert.Equal(expected, AppVersion.IsValid(v));

    [Fact]
    public void Compare_TreatsMissingPartsAsZero_AndIsNumeric()
    {
        Assert.Equal(0, AppVersion.Compare("5.3", "5.3.0"));
        Assert.True(AppVersion.Compare("5.10.0", "5.9.0") > 0);
    }

    [Fact]
    public async Task GetUpdateInfo_UsesNewestReleaseForNotes()
    {
        var svc = Build([
            new AppRelease { Version = "5.9.0", NotesEn = "old" },
            new AppRelease { Version = "5.10.0", NotesEn = "new" },
        ]);

        var info = await svc.GetUpdateInfoAsync();

        Assert.Equal("5.10.0", info.LatestVersion);
        Assert.Equal("new", info.LatestNotesEn);
        Assert.Equal("", info.MinVersionIos);
    }

    [Fact]
    public async Task GetUpdateInfo_MandatoryRelease_RaisesConfiguredMinimum()
    {
        var options = new AppUpdateOptions { MinVersionAndroid = "5.0.0", MinVersionIos = "6.0.0" };
        var svc = Build([new AppRelease { Version = "5.3.0", NotesEn = "x", IsMandatory = true }], options);

        var info = await svc.GetUpdateInfoAsync();

        Assert.Equal("5.3.0", info.MinVersionAndroid);
        Assert.Equal("6.0.0", info.MinVersionIos);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidVersionAndMissingNotes()
    {
        var svc = Build([]);

        Assert.False((await svc.CreateAsync(new SaveAppReleaseDto { Version = "abc", NotesEn = "x" })).Succeeded);
        Assert.False((await svc.CreateAsync(new SaveAppReleaseDto { Version = "5.3.0", NotesEn = " " })).Succeeded);
    }
}
