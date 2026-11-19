using Robust.Shared.Serialization;

namespace Content.Shared._Qippe.Whitelist;

/// <summary>
/// Sent instead of <c>TickerJoinLobbyEvent</c> to a player the whitelist is blocking but who is still allowed to
/// apply. The client answers by entering <c>WhitelistApplicationState</c>, which offers nothing but the application
/// form, so a gated player can never reach the lobby or the round.
/// </summary>
[Serializable, NetSerializable]
public sealed class TickerJoinWhitelistApplicationEvent(bool hasApplication) : EntityEventArgs
{
    /// <summary>
    /// True when the player already has an application on file, so the screen asks them to wait for review
    /// instead of offering the form again.
    /// </summary>
    public bool HasApplication { get; } = hasApplication;
}
