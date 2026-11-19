using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server._Qippe.Whitelist;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using ClientWhitelist = Content.Client._Qippe.Whitelist;
using ClientWhitelistUi = Content.Client._Qippe.Whitelist.UI;

namespace Content.IntegrationTests.Tests.Whitelist;

/// <summary>
/// Opens both whitelist EUIs so the client loads the XAML and handles the state without crashing.
/// </summary>
public sealed class WhitelistApplicationUiTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true
    };

    [Test]
    [Retry(2)]
    public async Task OpenApplyAndReviewEuis()
    {
        var pair = Pair;
        var server = pair.Server;

        var players = server.ResolveDependency<IPlayerManager>();
        var admins = server.ResolveDependency<IAdminManager>();
        var euis = server.ResolveDependency<EuiManager>();

        await server.WaitAssertion(() =>
        {
            var session = players.Sessions.Single();

            // The player form is open to everyone.
            euis.OpenEui(new WhitelistApplyEui(), session);

            // The review window closes itself without the ban flag, so grant it first.
            admins.PromoteHost(session);
            euis.OpenEui(new WhitelistApplicationsEui(), session);
        });

        await RunUntilSynced();
    }

    /// <summary>
    /// Pressing View on a row must open the popup without double-parenting it into the modal root.
    /// </summary>
    [Test]
    [Retry(2)]
    public async Task ViewButtonOpensPopup()
    {
        var pair = Pair;
        var client = pair.Client;

        var application = new SharedWhitelistApplication(
            1,
            new NetUserId(Guid.Empty),
            "TestPlayer",
            "Please let me in.",
            WhitelistApplicationStatus.Pending,
            DateTime.UtcNow,
            null,
            null);

        ClientWhitelist.WhitelistApplicationLine line = null;

        await client.WaitPost(() =>
        {
            line = new ClientWhitelist.WhitelistApplicationLine(application, canReview: true);
            client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
                .StateRoot.AddChild(line);
        });

        await client.WaitIdleAsync();

        await client.WaitPost(() =>
        {
            Assert.That(line, Is.Not.Null);

            // This used to throw "This component is still parented" because the popup was added manually.
            Assert.DoesNotThrow(() => line!.OpenPopup());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(line!.Popup, Is.Not.Null);
                Assert.That(line.Popup!.Parent, Is.Not.Null);
            }
        });

        await client.WaitPost(() =>
        {
            line!.Popup?.Close();
            line.Orphan();
        });
    }

    /// <summary>
    /// The panel must be centered rather than pinned to the top left.
    /// </summary>
    /// <remarks>
    /// A <c>UIScreen</c> is a <c>LayoutContainer</c>, which positions children by anchor and ignores
    /// <c>HorizontalAlignment</c>/<c>VerticalAlignment</c>. With default anchors the panel used to land at (0, 0) and
    /// the parallax background collapsed to zero size, so this asserts the anchors are actually applied.
    /// </remarks>
    [Test]
    [Retry(2)]
    public async Task PanelIsCentered()
    {
        var client = Pair.Client;

        var screenSize = new Vector2(1920, 1080);

        ClientWhitelistUi.WhitelistApplicationScreen screen = null;

        await client.WaitPost(() =>
        {
            screen = new ClientWhitelistUi.WhitelistApplicationScreen();
            screen.Measure(screenSize);
            screen.Arrange(UIBox2.FromDimensions(Vector2.Zero, screenSize));
        });

        await client.WaitPost(() =>
        {
            Assert.That(screen, Is.Not.Null);

            var panel = screen!.MainPanel;
            var background = screen.Background;

            using (Assert.EnterMultipleScope())
            {
                // The background is anchored to the whole screen, so the parallax actually covers it.
                Assert.That(background.Size.X, Is.EqualTo(screenSize.X).Within(1));
                Assert.That(background.Size.Y, Is.EqualTo(screenSize.Y).Within(1));

                // The panel shrinks to its content instead of stretching across the screen.
                Assert.That(panel.Size.X, Is.LessThan(screenSize.X));
                Assert.That(panel.Size.Y, Is.LessThan(screenSize.Y));

                // ...and sits in the middle of it.
                Assert.That(panel.Position.X, Is.EqualTo((screenSize.X - panel.Size.X) / 2).Within(1));
                Assert.That(panel.Position.Y, Is.EqualTo((screenSize.Y - panel.Size.Y) / 2).Within(1));
            }

            screen.Orphan();
        });
    }
}
