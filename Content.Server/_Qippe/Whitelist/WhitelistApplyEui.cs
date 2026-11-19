using System.Threading.Tasks;
using Content.Server.EUI;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.CCVar;
using Content.Shared.Eui;
using Robust.Shared.Configuration;
using static Content.Shared._Qippe.Whitelist.WhitelistApplicationEuiMsg;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Player facing form for submitting a single whitelist application.
/// </summary>
public sealed partial class WhitelistApplyEui : BaseEui
{
    [Dependency] private WhitelistManager _whitelist = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _log = default!;

    private readonly ISawmill _sawmill;
    private SharedWhitelistApplication? _existing;
    private bool _enabled;

    public WhitelistApplyEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("whitelist_application");
    }

    public override void Opened()
    {
        base.Opened();

        _enabled = _cfg.GetCVar(CCVars.WhitelistEnabled);
        _ = LoadExisting();
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistApplyEuiState(
            _existing,
            _enabled,
            WhitelistApplicationConstants.MaxMessageLength);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is SubmitRequest request)
            _ = Submit(request.Message);
    }

    private async Task LoadExisting()
    {
        var application = await _whitelist.GetByPlayerAsync(Player.UserId);

        // The window may have been closed while the query was running.
        if (IsShutDown)
            return;

        _existing = application?.ToShared();
        StateDirty();
    }

    private async Task Submit(string message)
    {
        if (!_enabled)
            return;

        // The disabled button on the client is cosmetic, so enforce one application per player here too.
        if (_existing != null)
        {
            SendMessage(new SubmitRejected(WhitelistApplicationSubmitResult.AlreadyApplied));
            return;
        }

        var (result, _) = await _whitelist.TryCreateAsync(Player.UserId, message);

        // The window may have been closed while the request was running.
        if (IsShutDown)
            return;

        switch (result)
        {
            case WhitelistApplicationResult.Success:
                // Reload so the state carries the player name from the database row.
                await LoadExisting();
                break;
            case WhitelistApplicationResult.EmptyMessage:
                SendMessage(new SubmitRejected(WhitelistApplicationSubmitResult.EmptyMessage));
                break;
            case WhitelistApplicationResult.MessageTooLong:
                SendMessage(new SubmitRejected(WhitelistApplicationSubmitResult.MessageTooLong));
                break;
            case WhitelistApplicationResult.AlreadyApplied:
                SendMessage(new SubmitRejected(WhitelistApplicationSubmitResult.AlreadyApplied));
                break;
            default:
                _sawmill.Error("Whitelist application from {Player} failed with {Result}.", Player.UserId, result);
                SendMessage(new SubmitRejected(WhitelistApplicationSubmitResult.Failed));
                break;
        }
    }
}
