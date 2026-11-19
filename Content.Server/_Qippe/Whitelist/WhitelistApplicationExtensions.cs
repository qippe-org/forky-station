using Content.Server.Database;
using Content.Shared._Qippe.Whitelist;
using Robust.Shared.Network;

namespace Content.Server._Qippe.Whitelist;

public static class WhitelistApplicationExtensions
{
    /// <summary>
    /// Converts a database row into the DTO sent to clients. Both navigations must be loaded.
    /// </summary>
    public static SharedWhitelistApplication ToShared(this WhitelistApplication application)
    {
        return new SharedWhitelistApplication(
            application.Id,
            new NetUserId(application.PlayerUserId),
            application.Player?.LastSeenUserName ?? application.PlayerUserId.ToString(),
            application.Message,
            application.Status,
            application.CreatedAt,
            application.ReviewedBy?.LastSeenUserName,
            application.ReviewedAt);
    }
}
