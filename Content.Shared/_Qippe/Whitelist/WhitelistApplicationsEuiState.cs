using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Qippe.Whitelist;

/// <summary>
/// State of the admin review window.
/// </summary>
[Serializable, NetSerializable]
public sealed class WhitelistApplicationsEuiState(List<SharedWhitelistApplication> applications, bool canReview)
    : EuiStateBase
{
    public List<SharedWhitelistApplication> Applications { get; } = applications;

    /// <summary>
    /// Whether the admin may approve or deny. The server re-checks this on every message.
    /// </summary>
    public bool CanReview { get; } = canReview;
}
