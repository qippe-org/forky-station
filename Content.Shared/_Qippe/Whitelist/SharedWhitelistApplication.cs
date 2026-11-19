using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Qippe.Whitelist;

/// <summary>
/// A whitelist application as shown to the client.
/// </summary>
[Serializable, NetSerializable]
public sealed record SharedWhitelistApplication(
    int Id,
    NetUserId PlayerUserId,
    string PlayerName,
    string Message,
    WhitelistApplicationStatus Status,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt);
