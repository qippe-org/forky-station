using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Qippe.Whitelist;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Whitelist;

/// <summary>
/// Covers the player facing <c>whitelistapply</c> command and the admin review commands.
/// </summary>
/// <remarks>
/// The test harness fails a test whenever a console command reports an error, so the failure paths
/// (empty and over-long messages, duplicate applications, unknown ids) are exercised on
/// <see cref="WhitelistManager"/> directly.
/// </remarks>
[TestFixture]
[TestOf(typeof(WhitelistManager))]
public sealed class WhitelistApplicationTest : GameTest
{
    private const string Message = "I would like to join this station.";

    [Test]
    public async Task ApplyListApproveAndDeny()
    {
        var pair = Pair;
        var server = pair.Server;
        var db = server.ResolveDependency<IServerDbManager>();
        var console = server.ResolveDependency<IServerConsoleHost>();
        var players = server.ResolveDependency<IPlayerManager>();
        var manager = server.ResolveDependency<WhitelistManager>();

        var session = players.Sessions.Single();
        var userId = session.UserId;
        var initialCount = (await db.GetAllWhitelistApplicationsAsync()).Count;

        // Validation lives on the manager, since the console turns these outcomes into errors.
        Assert.That((await manager.TryCreateAsync(userId, "   ")).Result,
            Is.EqualTo(WhitelistApplicationResult.EmptyMessage));

        var tooLong = new string('a', WhitelistManager.MaxMessageLength + 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await manager.TryCreateAsync(userId, tooLong)).Result,
                Is.EqualTo(WhitelistApplicationResult.MessageTooLong));
            Assert.That(await db.GetWhitelistApplicationByPlayerAsync(userId), Is.Null);
        }

        // A player submits one application through their own console shell.
        // The manager event is what refreshes open admin windows, so it must fire exactly once here.
        var changed = 0;
        manager.ApplicationsChanged += () => changed++;

        await server.WaitPost(() => console.GetSessionShell(session).ExecuteCommand($"whitelistapply {Message}"));

        await PoolManager.WaitUntil(server, async () => await db.GetWhitelistApplicationByPlayerAsync(userId) != null);

        Assert.That(changed, Is.EqualTo(1));

        var application = await db.GetWhitelistApplicationByPlayerAsync(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(application, Is.Not.Null);

            // The player form reads its own row through the manager, and an unknown player has none.
            Assert.That((await manager.GetByPlayerAsync(userId))?.Id, Is.EqualTo(application!.Id));
            Assert.That(await manager.GetByPlayerAsync(new NetUserId(Guid.NewGuid())), Is.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(application!.PlayerUserId, Is.EqualTo(userId.UserId));
            Assert.That(application.Message, Is.EqualTo(Message));
            Assert.That(application.Status, Is.EqualTo(WhitelistApplicationStatus.Pending));
            Assert.That(application.CreatedAt, Is.Not.Default);
            Assert.That(application.ReviewedById, Is.Null);
            Assert.That(application.ReviewedAt, Is.Null);
            Assert.That(application.Player.LastSeenUserName, Is.EqualTo(session.Name));
        }

        using (Assert.EnterMultipleScope())
        {
            // One application per player, so a second attempt is rejected without inserting another row.
            Assert.That((await manager.TryCreateAsync(userId, "a second application")).Result,
                Is.EqualTo(WhitelistApplicationResult.AlreadyApplied));
            Assert.That(await db.GetAllWhitelistApplicationsAsync(), Has.Count.EqualTo(initialCount + 1));
        }

        // The admin list command prints every application.
        await server.WaitPost(() => console.ExecuteCommand("whitelistapplications"));
        await pair.RunTicksSync(5);

        using (Assert.EnterMultipleScope())
        {
            // Reviewing an unknown id reports not found and changes nothing.
            Assert.That((await manager.TryApproveAsync(9999, null)).Result, Is.EqualTo(WhitelistApplicationResult.NotFound));
            Assert.That((await manager.TryDenyAsync(9999, null)).Result, Is.EqualTo(WhitelistApplicationResult.NotFound));
            Assert.That(await db.GetAllWhitelistApplicationsAsync(), Has.Count.EqualTo(initialCount + 1));
        }

        // Approving also grants the regular whitelist.
        await server.WaitPost(() => console.ExecuteCommand($"whitelistapplicationapprove {application.Id}"));

        await PoolManager.WaitUntil(server, async () => await db.GetWhitelistStatusAsync(userId));

        Assert.That(changed, Is.EqualTo(2));

        application = await db.GetWhitelistApplicationByPlayerAsync(userId);
        Assert.That(application, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(application!.Status, Is.EqualTo(WhitelistApplicationStatus.Approved));
            Assert.That(application.ReviewedAt, Is.Not.Null);
        }

        // Denying only changes the application status, it never revokes the whitelist.
        await server.WaitPost(() => console.ExecuteCommand($"whitelistapplicationdeny {application.Id}"));

        await PoolManager.WaitUntil(server, async () =>
            (await db.GetWhitelistApplicationByPlayerAsync(userId))?.Status == WhitelistApplicationStatus.Denied);

        Assert.That(changed, Is.EqualTo(3));

        application = await db.GetWhitelistApplicationByPlayerAsync(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(application!.Status, Is.EqualTo(WhitelistApplicationStatus.Denied));
            Assert.That(await db.GetWhitelistStatusAsync(userId), Is.True);
        }
    }
}
