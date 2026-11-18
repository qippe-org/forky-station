using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Approves a whitelist application and grants the regular server whitelist.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed partial class WhitelistApplicationApproveCommand : LocalizedCommands
{
    [Dependency] private WhitelistManager _whitelist = default!;

    public override string Command => "whitelistapplicationapprove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryGetId(shell, args, out var id))
            return;

        var (result, application) = await _whitelist.TryApproveAsync(id, shell.Player?.UserId);

        switch (result)
        {
            case WhitelistApplicationResult.Success:
                shell.WriteLine(Loc.GetString("cmd-whitelistapplicationapprove-approved",
                    ("id", application!.Id),
                    ("player", WhitelistApplicationsCommand.GetPlayerName(application))));
                break;
            case WhitelistApplicationResult.NotFound:
                shell.WriteError(Loc.GetString("cmd-whitelistapplication-not-found", ("id", id)));
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-whitelistapplicationapprove-failed", ("id", id)));
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint(Loc.GetString("cmd-whitelistapplication-hint-id"));

        return CompletionResult.Empty;
    }

    private bool TryGetId(IConsoleShell shell, string[] args, out int id)
    {
        id = 0;

        if (args.Length == 1 && int.TryParse(args[0], out id))
            return true;

        shell.WriteError(Help);
        return false;
    }
}

/// <summary>
/// Denies a whitelist application without touching the regular server whitelist.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed partial class WhitelistApplicationDenyCommand : LocalizedCommands
{
    [Dependency] private WhitelistManager _whitelist = default!;

    public override string Command => "whitelistapplicationdeny";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryGetId(shell, args, out var id))
            return;

        var (result, application) = await _whitelist.TryDenyAsync(id, shell.Player?.UserId);

        switch (result)
        {
            case WhitelistApplicationResult.Success:
                shell.WriteLine(Loc.GetString("cmd-whitelistapplicationdeny-denied",
                    ("id", application!.Id),
                    ("player", WhitelistApplicationsCommand.GetPlayerName(application))));
                break;
            case WhitelistApplicationResult.NotFound:
                shell.WriteError(Loc.GetString("cmd-whitelistapplication-not-found", ("id", id)));
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-whitelistapplicationdeny-failed", ("id", id)));
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint(Loc.GetString("cmd-whitelistapplication-hint-id"));

        return CompletionResult.Empty;
    }

    private bool TryGetId(IConsoleShell shell, string[] args, out int id)
    {
        id = 0;

        if (args.Length == 1 && int.TryParse(args[0], out id))
            return true;

        shell.WriteError(Help);
        return false;
    }
}