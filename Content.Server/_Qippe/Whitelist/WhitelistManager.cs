using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Database;
using Robust.Shared.Network;
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
    public const int MaxMessageLength = WhitelistApplicationConstants.MaxMessageLength;

    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private PlayerWhitelistManager _whitelist = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private WhitelistGateManager _gate = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Raised after an application is created, approved or denied, so open admin windows can refresh.
    /// </summary>
    public event Action? ApplicationsChanged;

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
        ApplicationsChanged?.Invoke();

        // The player may be sitting on the application screen, which should now stop offering the form.
        _gate.RefreshApplicationScreen(player);

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
    /// Returns the player's application, or null if they have not applied yet.
    /// </summary>
    public Task<WhitelistApplication?> GetByPlayerAsync(NetUserId player)
    {
        return _db.GetWhitelistApplicationByPlayerAsync(player);
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

        // A gated player is stuck on the application screen, so let them through immediately instead of making them
        // reconnect to notice the approval.
        _gate.Release(player);

        _sawmill.Info("{Reviewer} approved whitelist application #{Id} for {Player}.",
            reviewer?.ToString() ?? "Server console",
            application.Id,
            player);

        ApplicationsChanged?.Invoke();
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

        ApplicationsChanged?.Invoke();
        return (WhitelistApplicationResult.Success, application);
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("whitelist_application");
    }
}
