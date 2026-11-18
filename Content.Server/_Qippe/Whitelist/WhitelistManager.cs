using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;
using PlayerWhitelistManager = Content.Server.Players.Whitelist.WhitelistManager;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Outcome of a whitelist application action, so console commands can print a localized message.
/// </summary>
public enum WhitelistApplicationResult : byte
{
    Success,
    EmptyMessage,
    MessageTooLong,
    AlreadyApplied,
    NotFound,
}

/// <summary>
/// Handles player whitelist applications: creating them and reviewing them.
/// Approving an application also grants the regular server whitelist through
/// <see cref="Content.Server.Players.Whitelist.WhitelistManager"/>.
/// </summary>
public sealed partial class WhitelistManager : IPostInjectInit
{
    /// <summary>
    /// Maximum length of an application message, matching the database column.
    /// </summary>
    public const int MaxMessageLength = 4096;

    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private PlayerWhitelistManager _whitelist = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Creates a pending application for the player, if they do not have one yet.
    /// </summary>
    public async Task<(WhitelistApplicationResult Result, WhitelistApplication? Application)> TryCreateAsync(
        NetUserId player,
        string message)
    {
        message = message.Trim();

        if (message.Length == 0)
            return (WhitelistApplicationResult.EmptyMessage, null);

        if (message.Length > MaxMessageLength)
            return (WhitelistApplicationResult.MessageTooLong, null);

        if (await _db.GetWhitelistApplicationByPlayerAsync(player) != null)
            return (WhitelistApplicationResult.AlreadyApplied, null);

        var application = await _db.AddWhitelistApplicationAsync(player, message);
        _sawmill.Info("New whitelist application #{Id} from {Player}.", application.Id, player);
        return (WhitelistApplicationResult.Success, application);
    }

    /// <summary>
    /// Returns every application for the admin list command.
    /// </summary>
    public Task<List<WhitelistApplication>> GetAllAsync()
    {
        return _db.GetAllWhitelistApplicationsAsync();
    }

    /// <summary>
    /// Approves an application and whitelists the applicant if they are not whitelisted already.
    /// </summary>
    public async Task<(WhitelistApplicationResult Result, WhitelistApplication? Application)> TryApproveAsync(
        int id,
        NetUserId? reviewer)
    {
        var application = await _db.SetWhitelistApplicationStatusAsync(
            id,
            WhitelistApplicationStatus.Approved,
            reviewer?.UserId);

        if (application == null)
            return (WhitelistApplicationResult.NotFound, null);

        var player = new NetUserId(application.PlayerUserId);

        if (!await _db.GetWhitelistStatusAsync(player))
            _whitelist.AddWhitelist(player);

        _sawmill.Info("{Reviewer} approved whitelist application #{Id} for {Player}.",
            reviewer?.ToString() ?? "Server console",
            application.Id,
            player);

        return (WhitelistApplicationResult.Success, application);
    }

    /// <summary>
    /// Denies an application. Does not touch the regular whitelist.
    /// </summary>
    public async Task<(WhitelistApplicationResult Result, WhitelistApplication? Application)> TryDenyAsync(
        int id,
        NetUserId? reviewer)
    {
        var application = await _db.SetWhitelistApplicationStatusAsync(
            id,
            WhitelistApplicationStatus.Denied,
            reviewer?.UserId);

        if (application == null)
            return (WhitelistApplicationResult.NotFound, null);

        _sawmill.Info("{Reviewer} denied whitelist application #{Id} from {Player}.",
            reviewer?.ToString() ?? "Server console",
            application.Id,
            new NetUserId(application.PlayerUserId));

        return (WhitelistApplicationResult.Success, application);
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("whitelist_application");
    }
}