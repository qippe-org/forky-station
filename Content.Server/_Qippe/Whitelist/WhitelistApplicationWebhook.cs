using Content.Server.Database;
using Content.Server.Discord;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Server;
using Robust.Shared.Configuration;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// What happened to a whitelist application, so the embed can say so.
/// </summary>
public enum WhitelistApplicationWebhookEvent : byte
{
    /// <summary>
    /// The player submitted an application.
    /// </summary>
    Created,

    /// <summary>
    /// An admin approved the application and whitelisted the player.
    /// </summary>
    Approved,

    /// <summary>
    /// An admin denied the application.
    /// </summary>
    Denied,
}

/// <summary>
/// Mirrors whitelist application activity to a Discord webhook as an embed.
/// </summary>
/// <remarks>
/// Sending is best effort: a missing url or a broken connection only logs, it never fails the application itself.
/// </remarks>
public sealed partial class WhitelistApplicationWebhook : IPostInjectInit
{
    // Same colors the vote webhook uses, so statuses read the same in a channel that shows both.
    private const int ColorCreated = 15844367; // #F1C40F
    private const int ColorApproved = 2353993; // #23EB49
    private const int ColorDenied = 13438992; // #CD1010

    [Dependency] private DiscordWebhook _discord = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IBaseServer _baseServer = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;
    private string _webhookUrl = string.Empty;

    /// <summary>
    /// True when a webhook url is configured, so callers can skip the work of building a payload.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookUrl);

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("whitelist_application");
        _cfg.OnValueChanged(CCVars.DiscordWhitelistApplicationWebhook, SetWebhookUrl, true);
    }

    private void SetWebhookUrl(string url)
    {
        _webhookUrl = url;
    }

    /// <summary>
    /// Sends the embed for an application event, if a webhook is configured.
    /// </summary>
    public void Notify(WhitelistApplication application, WhitelistApplicationWebhookEvent kind)
    {
        if (!IsConfigured)
            return;

        Send(BuildPayload(application, kind));
    }

    /// <summary>
    /// Builds the embed for an application event.
    /// </summary>
    /// <remarks>
    /// Kept separate from sending so the embed contents can be checked without touching the network.
    /// </remarks>
    public WebhookPayload BuildPayload(WhitelistApplication application, WhitelistApplicationWebhookEvent kind)
    {
        var eventKey = GetEventKey(kind);

        var fields = new List<WebhookEmbedField>
        {
            new()
            {
                Name = Loc.GetString("whitelist-application-webhook-field-player"),
                // The player row can be missing for an old application, this falls back to the user id.
                Value = WhitelistApplicationsCommand.GetPlayerName(application),
            },
            new()
            {
                Name = Loc.GetString("whitelist-application-webhook-field-status"),
                Value = Loc.GetString(GetStatusKey(application.Status)),
            },
        };
        if (application.ReviewedBy != null)
        {
            fields.Add(new WebhookEmbedField
            {
                Name = Loc.GetString("whitelist-application-webhook-field-reviewer"),
                Value = application.ReviewedBy.LastSeenUserName,
            });
        }

        // Discord rejects an empty description, and an application is never empty anyway.
        var message = string.IsNullOrWhiteSpace(application.Message)
            ? Loc.GetString("whitelist-application-webhook-no-message")
            : application.Message;

        return new WebhookPayload
        {
            Username = Loc.GetString($"whitelist-application-webhook-{eventKey}-username"),
            Embeds =
            [
                new()
                {
                    Title = Loc.GetString($"whitelist-application-webhook-{eventKey}-title",
                        ("id", application.Id)),
                    Color = GetColor(kind),
                    Description = message,
                    Fields = fields,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = Loc.GetString("whitelist-application-webhook-footer",
                            ("serverName", _baseServer.ServerName),
                            ("id", application.Id)),
                    },
                },
            ],
        };
    }

    private async void Send(WebhookPayload payload)
    {
        try
        {
            if (await _discord.GetWebhook(_webhookUrl) is not { } data)
                return;

            await _discord.CreateMessage(data.ToIdentifier(), payload);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending whitelist application webhook to Discord:\n{e}");
        }
    }

    private static string GetEventKey(WhitelistApplicationWebhookEvent kind)
    {
        return kind switch
        {
            WhitelistApplicationWebhookEvent.Approved => "approved",
            WhitelistApplicationWebhookEvent.Denied => "denied",
            _ => "created",
        };
    }

    private static string GetStatusKey(WhitelistApplicationStatus status)
    {
        return status switch
        {
            WhitelistApplicationStatus.Approved => "whitelist-application-webhook-status-approved",
            WhitelistApplicationStatus.Denied => "whitelist-application-webhook-status-denied",
            _ => "whitelist-application-webhook-status-pending",
        };
    }

    private static int GetColor(WhitelistApplicationWebhookEvent kind)
    {
        return kind switch
        {
            WhitelistApplicationWebhookEvent.Approved => ColorApproved,
            WhitelistApplicationWebhookEvent.Denied => ColorDenied,
            _ => ColorCreated,
        };
    }
}
