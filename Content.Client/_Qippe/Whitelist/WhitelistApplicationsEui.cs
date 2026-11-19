using System.Linq;
using Content.Client.Eui;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Database;
using Content.Shared.Eui;
using JetBrains.Annotations;
using static Content.Shared._Qippe.Whitelist.WhitelistApplicationEuiMsg;

namespace Content.Client._Qippe.Whitelist;

[UsedImplicitly]
public sealed class WhitelistApplicationsEui : BaseEui
{
    private readonly WhitelistApplicationsWindow _window;
    private bool _canReview;

    public WhitelistApplicationsEui()
    {
        _window = new WhitelistApplicationsWindow();

        _window.AllList.ApprovePressed += id => SendMessage(new ApproveRequest(id));
        _window.AllList.DenyPressed += id => SendMessage(new DenyRequest(id));
        _window.PendingList.ApprovePressed += id => SendMessage(new ApproveRequest(id));
        _window.PendingList.DenyPressed += id => SendMessage(new DenyRequest(id));
        _window.ApprovedList.ApprovePressed += id => SendMessage(new ApproveRequest(id));
        _window.ApprovedList.DenyPressed += id => SendMessage(new DenyRequest(id));
        _window.DeniedList.ApprovePressed += id => SendMessage(new ApproveRequest(id));
        _window.DeniedList.DenyPressed += id => SendMessage(new DenyRequest(id));

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not WhitelistApplicationsEuiState s)
            return;

        _canReview = s.CanReview;

        _window.SetCounts(s.Applications);
        _window.AllList.SetApplications(s.Applications, _canReview);
        _window.PendingList.SetApplications(
            [.. s.Applications.Where(application => application.Status == WhitelistApplicationStatus.Pending)],
            _canReview);
        _window.ApprovedList.SetApplications(
            [.. s.Applications.Where(application => application.Status == WhitelistApplicationStatus.Approved)],
            _canReview);
        _window.DeniedList.SetApplications(
            [.. s.Applications.Where(application => application.Status == WhitelistApplicationStatus.Denied)],
            _canReview);
    }
}
