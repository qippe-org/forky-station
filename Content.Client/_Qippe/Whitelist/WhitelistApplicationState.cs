using Content.Client.GameTicking.Managers;
using Content.Client._Qippe.Whitelist.UI;
using Robust.Client.UserInterface;

namespace Content.Client._Qippe.Whitelist;

/// <summary>
/// The only screen a player sees when the whitelist is blocking them but they may still apply.
/// </summary>
/// <remarks>
/// This state is a presentation of a server side gate, not the gate itself. Leaving it does not let the player into
/// the round, because <c>GameTicker</c> refuses to put a gated player in the lobby or spawn them.
/// </remarks>
public sealed class WhitelistApplicationState : Robust.Client.State.State
{
    protected override Type? LinkedScreenType { get; } = typeof(WhitelistApplicationScreen);

    protected override void Startup()
    {
        var gameTicker = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ClientGameTicker>();

        if (IoCManager.Resolve<IUserInterfaceManager>().ActiveScreen is WhitelistApplicationScreen screen)
            screen.SetState(gameTicker.HasWhitelistApplication);
    }

    protected override void Shutdown()
    {
    }
}
