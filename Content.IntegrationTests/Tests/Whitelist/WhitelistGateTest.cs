using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Qippe.Whitelist;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Whitelist;

/// <summary>
/// Covers the gate that lets a non-whitelisted player connect just far enough to submit an application.
/// </summary>
/// <remarks>
/// The connection pipeline itself is not driven here, because it needs a real join handshake. Instead this covers the
/// pieces the pipeline depends on: the gate flag, the application lookup, and the release that happens on approval.
/// </remarks>
[TestFixture]
[TestOf(typeof(WhitelistGateManager))]
public sealed class WhitelistGateTest : GameTest
{
    [Test]
    public async Task GateTracksPlayersAndClearsOnDisconnect()
    {
        var pair = Pair;
        var server = pair.Server;

        var players = server.ResolveDependency<IPlayerManager>();
        var gate = server.ResolveDependency<WhitelistGateManager>();

        var session = players.Sessions.Single();
        var userId = session.UserId;

        await server.WaitAssertion(() =>
        {
            // A fresh player is not gated.
            gate.Clear(userId);
            Assert.That(gate.IsGated(userId), Is.False);

            gate.Gate(userId);
            Assert.That(gate.IsGated(userId), Is.True);
        });

        // Gating the same player twice is harmless.
        await server.WaitAssertion(() =>
        {
            gate.Gate(userId);
            Assert.That(gate.IsGated(userId), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            gate.Clear(userId);
            Assert.That(gate.IsGated(userId), Is.False);
        });
    }

    [Test]
    public async Task HasApplicationReflectsSubmittedRows()
    {
        var pair = Pair;
        var server = pair.Server;

        var players = server.ResolveDependency<IPlayerManager>();
        var gate = server.ResolveDependency<WhitelistGateManager>();
        var db = server.ResolveDependency<IServerDbManager>();
        var whitelist = server.ResolveDependency<WhitelistManager>();

        var session = players.Sessions.Single();
        var userId = session.UserId;

        // Start from a clean slate, since the pool may be reused between tests.
        var existing = await db.GetWhitelistApplicationByPlayerAsync(userId);
        if (existing == null)
        {
            Assert.That(await gate.HasApplicationAsync(userId), Is.False);

            var (result, _) = await whitelist.TryCreateAsync(userId, "Please let me in.");
            Assert.That(result, Is.EqualTo(WhitelistApplicationResult.Success));
        }

        Assert.That(await gate.HasApplicationAsync(userId), Is.True);
        Assert.That(await gate.HasApplicationAsync(new NetUserId(Guid.NewGuid())), Is.False);
    }

    [Test]
    public async Task ApprovalReleasesGatedPlayer()
    {
        var pair = Pair;
        var server = pair.Server;

        var players = server.ResolveDependency<IPlayerManager>();
        var gate = server.ResolveDependency<WhitelistGateManager>();
        var db = server.ResolveDependency<IServerDbManager>();
        var whitelist = server.ResolveDependency<WhitelistManager>();

        var session = players.Sessions.Single();
        var userId = session.UserId;

        var application = await db.GetWhitelistApplicationByPlayerAsync(userId);
        if (application == null)
        {
            var (createResult, created) = await whitelist.TryCreateAsync(userId, "Let me in, please.");
            Assert.That(createResult, Is.EqualTo(WhitelistApplicationResult.Success));
            application = created;
        }

        Assert.That(application, Is.Not.Null);

        // Denying must not lift the gate, otherwise a denied player could keep reconnecting and reapplying.
        await server.WaitAssertion(() => gate.Gate(userId));
        Assert.That(gate.IsGated(userId), Is.True);

        var (denyResult, _) = await whitelist.TryDenyAsync(application!.Id, null);
        Assert.That(denyResult, Is.EqualTo(WhitelistApplicationResult.Success));
        Assert.That(gate.IsGated(userId), Is.True);

        // Approving does lift it, so the player does not have to reconnect to get into the lobby.
        var (approveResult, _) = await whitelist.TryApproveAsync(application.Id, null);
        Assert.That(approveResult, Is.EqualTo(WhitelistApplicationResult.Success));
        Assert.That(gate.IsGated(userId), Is.False);
        Assert.That(await db.GetWhitelistStatusAsync(userId), Is.True);
    }
}
