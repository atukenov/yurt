using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Workers;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class WorkerServiceTests
{
    private static (WorkerService svc, IApplicationDbContext db) Build(IEnumerable<AdminUser>? workers = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var hasher = Substitute.For<IPasswordHasher>();
        var audit = Substitute.For<IAuditLogService>();
        hasher.Hash(Arg.Any<string>()).Returns(ci => $"hashed:{ci.Arg<string>()}");

        var set = (workers ?? []).ToList().BuildMockDbSet();
        db.AdminUsers.Returns(set);

        return (new WorkerService(db, hasher, audit), db);
    }

    [Fact]
    public async Task GetWorkersAsync_SortsByUsername()
    {
        var z = new AdminUser { Username = "zeke", Role = AdminRole.Worker };
        var a = new AdminUser { Username = "amina", Role = AdminRole.Worker };
        var (svc, _) = Build([z, a]);

        var result = await svc.GetWorkersAsync();

        Assert.Equal(["amina", "zeke"], result.Select(r => r.Username));
    }

    [Fact]
    public async Task CreateWorkerAsync_DuplicateUsername_ReturnsConflict()
    {
        var existing = new AdminUser { Username = "taken", Role = AdminRole.Worker };
        var (svc, _) = Build([existing]);

        var result = await svc.CreateWorkerAsync(new CreateWorkerDto { Username = "taken", Password = "pw" });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task CreateWorkerAsync_NewUsername_HashesPasswordAndSaves()
    {
        var (svc, db) = Build();

        var result = await svc.CreateWorkerAsync(new CreateWorkerDto { Username = "newhire", Password = "plainpw" });

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.AdminUsers.Received(1).Add(Arg.Is<AdminUser>(u =>
            u.Username == "newhire" && u.PasswordHash == "hashed:plainpw" && u.IsActive));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkerAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _) = Build();

        var result = await svc.UpdateWorkerAsync(Guid.NewGuid(), new UpdateWorkerDto { Username = "x", IsActive = true });

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkerAsync_RenamingToAnotherWorkersUsername_ReturnsConflict()
    {
        var worker = new AdminUser { Username = "worker1", Role = AdminRole.Worker };
        var other = new AdminUser { Username = "worker2", Role = AdminRole.Worker };
        var (svc, _) = Build([worker, other]);

        var result = await svc.UpdateWorkerAsync(worker.Id, new UpdateWorkerDto { Username = "worker2", IsActive = true });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkerAsync_ValidChange_UpdatesAndSaves()
    {
        var worker = new AdminUser { Username = "old", IsActive = true, Role = AdminRole.Worker };
        var (svc, db) = Build([worker]);

        var result = await svc.UpdateWorkerAsync(worker.Id, new UpdateWorkerDto { Username = "new", IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("new", worker.Username);
        Assert.False(worker.IsActive);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _) = Build();
        var result = await svc.SetActiveAsync(Guid.NewGuid(), false);
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task SetActiveAsync_TogglesFlag()
    {
        var worker = new AdminUser { Username = "w", IsActive = true, Role = AdminRole.Worker };
        var (svc, _) = Build([worker]);

        var result = await svc.SetActiveAsync(worker.Id, false);

        Assert.True(result.Succeeded);
        Assert.False(worker.IsActive);
    }

    [Fact]
    public async Task ResetPasswordAsync_HashesNewPassword()
    {
        var worker = new AdminUser { Username = "w", PasswordHash = "old-hash", Role = AdminRole.Worker };
        var (svc, _) = Build([worker]);

        var result = await svc.ResetPasswordAsync(worker.Id, new ResetWorkerPasswordDto { NewPassword = "newpw" });

        Assert.True(result.Succeeded);
        Assert.Equal("hashed:newpw", worker.PasswordHash);
    }
}
