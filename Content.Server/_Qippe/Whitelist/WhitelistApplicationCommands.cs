using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Lets a player submit a single whitelist application.
/// </summary>
[AnyCommand]
public sealed partial class WhitelistApplyCommand : LocalizedCommands
{
    [Dependency] private WhitelistManager _whitelist = default!;

    public override string Command => "whitelistapply";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length == 0)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistapply-message-missing"));
            shell.WriteLine(Help);
            return;
        }

        var message = string.Join(" ", args);
        var (result, application) = await _whitelist.TryCreateAsync(player.UserId, message);

        switch (result)
        {
            case WhitelistApplicationResult.Success:
                shell.WriteLine(Loc.GetString("cmd-whitelistapply-created", ("id", application!.Id)));
                break;
            case WhitelistApplicationResult.EmptyMessage:
                shell.WriteError(Loc.GetString("cmd-whitelistapply-message-missing"));
                break;
            case WhitelistApplicationResult.MessageTooLong:
                shell.WriteError(Loc.GetString("cmd-whitelistapply-message-too-long",
                    ("max", WhitelistManager.MaxMessageLength)));
                break;
            case WhitelistApplicationResult.AlreadyApplied:
                shell.WriteError(Loc.GetString("cmd-whitelistapply-already-applied"));
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-whitelistapply-failed"));
                break;
        }
    }
}

/// <summary>
/// Lists every whitelist application with its player, status, creation time and message.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed partial class WhitelistApplicationsCommand : LocalizedCommands
{
    [Dependency] private WhitelistManager _whitelist = default!;

    public override string Command => "whitelistapplications";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 0),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        var applications = await _whitelist.GetAllAsync();

        if (applications.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-whitelistapplications-empty"));
            return;
        }

        foreach (var application in applications)
        {
            shell.WriteLine(Loc.GetString("cmd-whitelistapplications-entry",
                ("id", application.Id),
                ("player", GetPlayerName(application)),
                ("status", GetStatusName(application.Status)),
                ("createdAt", $"{application.CreatedAt:yyyy-MM-dd HH:mm} UTC"),
                ("message", application.Message)));
        }

        shell.WriteLine(Loc.GetString("cmd-whitelistapplications-total", ("count", applications.Count)));
    }

    public static string GetPlayerName(WhitelistApplication application)
    {
        return application.Player?.LastSeenUserName ?? application.PlayerUserId.ToString();
    }

    private string GetStatusName(WhitelistApplicationStatus status)
    {
        return status switch
        {
            WhitelistApplicationStatus.Approved => Loc.GetString("cmd-whitelistapplication-status-approved"),
            WhitelistApplicationStatus.Denied => Loc.GetString("cmd-whitelistapplication-status-denied"),
            _ => Loc.GetString("cmd-whitelistapplication-status-pending"),
        };
    }
}