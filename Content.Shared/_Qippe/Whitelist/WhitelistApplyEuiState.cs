using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Qippe.Whitelist;

/// <summary>
/// State of the player application form.
/// </summary>
[Serializable, NetSerializable]
public sealed class WhitelistApplyEuiState(
    SharedWhitelistApplication? existing,
    bool enabled,
    int maxLength) : EuiStateBase
{
    /// <summary>
    /// The player's existing application, if they already submitted one. The form is read-only in that case.
    /// </summary>
    public SharedWhitelistApplication? Existing { get; } = existing;

    /// <summary>
    /// False when the whitelist is disabled, so there is nothing to apply for.
    /// </summary>
    public bool Enabled { get; } = enabled;

    public int MaxLength { get; } = maxLength;
}
