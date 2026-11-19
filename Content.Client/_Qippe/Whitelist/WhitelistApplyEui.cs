using Content.Client.Eui;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Eui;
using JetBrains.Annotations;
using static Content.Shared._Qippe.Whitelist.WhitelistApplicationEuiMsg;

namespace Content.Client._Qippe.Whitelist;

[UsedImplicitly]
public sealed class WhitelistApplyEui : BaseEui
{
    private readonly WhitelistApplyWindow _window;

    public WhitelistApplyEui()
    {
        _window = new WhitelistApplyWindow();

        _window.SubmitPressed += message => SendMessage(new SubmitRequest(message));
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
        if (state is not WhitelistApplyEuiState s)
            return;

        _window.SetState(s);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is SubmitRejected rejected)
            _window.ShowResult(rejected.Result);
    }
}
