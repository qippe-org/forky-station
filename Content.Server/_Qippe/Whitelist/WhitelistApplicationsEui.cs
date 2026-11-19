using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Administration;
using Content.Shared.Eui;
using static Content.Shared._Qippe.Whitelist.WhitelistApplicationEuiMsg;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Admin window listing every whitelist application, with approve and deny actions.
/// </summary>
public sealed partial class WhitelistApplicationsEui : BaseEui
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private WhitelistManager _whitelist = default!;
    [Dependency] private ILogManager _log = default!;

    private readonly ISawmill _sawmill;
    private List<SharedWhitelistApplication> _applications = [];

    public WhitelistApplicationsEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("whitelist_application");
    }

    public override void Opened()
    {
        base.Opened();

        if (!_admins.HasAdminFlag(Player, AdminFlags.Ban))
        {
            _sawmill.Warning("{Player} tried to open the whitelist application list without the ban flag.", Player.UserId);
            Close();
            return;
        }

        _admins.OnPermsChanged += OnPermsChanged;
        _whitelist.ApplicationsChanged += OnApplicationsChanged;
        _ = ReloadApplications();
    }

    public override void Closed()
    {
        base.Closed();

        _admins.OnPermsChanged -= OnPermsChanged;
        _whitelist.ApplicationsChanged -= OnApplicationsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistApplicationsEuiState(_applications, CanReview());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        // Re-check on every message, a demoted admin may still have the window open.
        if (!CanReview())
            return;

        switch (msg)
        {
            case ApproveRequest request:
                _ = Approve(request.Id);
                break;
            case DenyRequest request:
                _ = Deny(request.Id);
                break;
        }
    }

    private bool CanReview()
    {
        return _admins.HasAdminFlag(Player, AdminFlags.Ban);
    }

    private async Task ReloadApplications()
    {
        var applications = await _whitelist.GetAllAsync();

        // The window may have been closed while the query was running.
        if (IsShutDown)
            return;

        // Pending first so the actionable rows are at the top, newest first within a status.
        _applications =
        [
            .. applications
                .Select(application => application.ToShared())
                .OrderBy(application => application.Status)
                .ThenByDescending(application => application.CreatedAt),
        ];

        StateDirty();
    }

    private async Task Approve(int id)
    {
        var (result, _) = await _whitelist.TryApproveAsync(id, Player.UserId);

        if (result == WhitelistApplicationResult.NotFound)
            _sawmill.Warning("{Player} tried to approve missing whitelist application #{Id}.", Player.UserId, id);

        await ReloadApplications();
    }

    private async Task Deny(int id)
    {
        var (result, _) = await _whitelist.TryDenyAsync(id, Player.UserId);

        if (result == WhitelistApplicationResult.NotFound)
            _sawmill.Warning("{Player} tried to deny missing whitelist application #{Id}.", Player.UserId, id);

        await ReloadApplications();
    }

    private void OnApplicationsChanged()
    {
        _ = ReloadApplications();
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
            return;

        if (!CanReview())
        {
            Close();
            return;
        }

        StateDirty();
    }
}
