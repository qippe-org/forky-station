using Content.IntegrationTests.Fixtures;
using Content.Server._Qippe.Whitelist;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Whitelist;

/// <summary>
/// Covers the Discord embeds sent for whitelist application activity.
/// </summary>
/// <remarks>
/// Only the payload is checked, not delivery, so these run without a webhook or any network access.
/// </remarks>
[TestFixture]
[TestOf(typeof(WhitelistApplicationWebhook))]
public sealed class WhitelistApplicationWebhookTest : GameTest
{
    private static WhitelistApplication MakeApplication(WhitelistApplicationStatus status)
    {
        var playerId = Guid.NewGuid();

        return new WhitelistApplication
        {
            Id = 42,
            PlayerUserId = playerId,
            Player = new Player { UserId = playerId, LastSeenUserName = "Urist" },
            Message = "Please let me in.",
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Test]
    public async Task CreatedEmbedDescribesPendingApplication()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            var payload = webhook.BuildPayload(
                MakeApplication(WhitelistApplicationStatus.Pending),
                WhitelistApplicationWebhookEvent.Created);

            Assert.That(payload.Embeds, Is.Not.Null);

            var embed = payload.Embeds![0];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(embed.Title, Does.Contain("42"));
                Assert.That(embed.Description, Is.EqualTo("Please let me in."));

                // The player field must use the name, not the raw user id.
                Assert.That(embed.Fields, Has.Count.EqualTo(2));
                Assert.That(embed.Fields[0].Value, Is.EqualTo("Urist"));
                Assert.That(embed.Fields[1].Value, Is.EqualTo(Loc.GetString("whitelist-application-webhook-status-pending")));
            }
        });
    }

    [Test]
    public async Task ReviewedEmbedAddsReviewerAndUsesStatusColor()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            var application = MakeApplication(WhitelistApplicationStatus.Approved);
            application.ReviewedById = Guid.NewGuid();
            application.ReviewedBy = new Player
            {
                UserId = application.ReviewedById.Value,
                LastSeenUserName = "Admeen",
            };

            var payload = webhook.BuildPayload(application, WhitelistApplicationWebhookEvent.Approved);
            var embed = payload.Embeds![0];

            using (Assert.EnterMultipleScope())
            {
                // A reviewer only shows up once the application has been reviewed.
                Assert.That(embed.Fields, Has.Count.EqualTo(3));
                Assert.That(embed.Fields[2].Value, Is.EqualTo("Admeen"));
                Assert.That(embed.Fields[1].Value,
                    Is.EqualTo(Loc.GetString("whitelist-application-webhook-status-approved")));

                // Approved and denied must not look the same in the channel.
                Assert.That(embed.Color, Is.Not.Zero);
            }
        });
    }

    [Test]
    public async Task DeniedAndApprovedUseDifferentColors()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            var approved = webhook.BuildPayload(
                MakeApplication(WhitelistApplicationStatus.Approved),
                WhitelistApplicationWebhookEvent.Approved);

            var denied = webhook.BuildPayload(
                MakeApplication(WhitelistApplicationStatus.Denied),
                WhitelistApplicationWebhookEvent.Denied);

            Assert.That(approved.Embeds![0].Color, Is.Not.EqualTo(denied.Embeds![0].Color));
        });
    }

    [Test]
    public async Task EmptyMessageFallsBackInsteadOfBlankDescription()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            var application = MakeApplication(WhitelistApplicationStatus.Pending);
            application.Message = "   ";

            var embed = webhook.BuildPayload(application, WhitelistApplicationWebhookEvent.Created).Embeds![0];

            // Discord rejects an empty description, so a blank message must be replaced.
            Assert.That(embed.Description, Is.Not.Empty);
        });
    }

    [Test]
    public async Task MissingPlayerRowFallsBackToUserId()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            var application = MakeApplication(WhitelistApplicationStatus.Pending);
            application.Player = null!;

            var embed = webhook.BuildPayload(application, WhitelistApplicationWebhookEvent.Created).Embeds![0];

            Assert.That(embed.Fields[0].Value, Is.EqualTo(application.PlayerUserId.ToString()));
        });
    }

    /// <summary>
    /// A missing locale string comes back as the raw message id, so check the embeds are really localized.
    /// </summary>
    [Test]
    public async Task EmbedStringsAreLocalized()
    {
        var server = Pair.Server;
        var webhook = server.ResolveDependency<WhitelistApplicationWebhook>();

        await server.WaitAssertion(() =>
        {
            foreach (var kind in new[]
                     {
                         WhitelistApplicationWebhookEvent.Created,
                         WhitelistApplicationWebhookEvent.Approved,
                         WhitelistApplicationWebhookEvent.Denied,
                     })
            {
                var application = MakeApplication(WhitelistApplicationStatus.Pending);
                application.ReviewedById = Guid.NewGuid();
                application.ReviewedBy = new Player
                {
                    UserId = application.ReviewedById.Value,
                    LastSeenUserName = "Admeen",
                };

                var payload = webhook.BuildPayload(application, kind);
                var embed = payload.Embeds![0];

                using (Assert.EnterMultipleScope())
                {
                    // Fallbacks return the message id itself, which always looks like this.
                    Assert.That(payload.Username, Does.Not.StartWith("whitelist-application-webhook"));
                    Assert.That(embed.Title, Does.Not.StartWith("whitelist-application-webhook"));
                    Assert.That(embed.Footer!.Value.Text, Does.Not.StartWith("whitelist-application-webhook"));

                    foreach (var field in embed.Fields)
                    {
                        Assert.That(field.Name, Does.Not.StartWith("whitelist-application-webhook"),
                            $"Field name for {kind} is not localized.");
                        Assert.That(field.Value, Does.Not.StartWith("whitelist-application-webhook"),
                            $"Field value for {kind} is not localized.");
                    }
                }
            }
        });
    }
}
