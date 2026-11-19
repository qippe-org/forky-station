using Content.Server._Qippe.Whitelist;
using Content.Server.Database;
using Content.Shared.Database;

namespace Content.IntegrationTests.Tests.Whitelist;

/// <summary>
/// Covers the database-to-shared mapping, which is a pure function so it needs no server.
/// </summary>
[TestFixture]
[TestOf(typeof(WhitelistApplicationExtensions))]
public sealed class WhitelistApplicationExtensionsTest
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Test]
    public void MapsPendingApplicationWithoutReviewer()
    {
        var application = new WhitelistApplication
        {
            Id = 7,
            PlayerUserId = PlayerId,
            Player = new Player { UserId = PlayerId, LastSeenUserName = "Urist" },
            Message = "hello",
            Status = WhitelistApplicationStatus.Pending,
            CreatedAt = CreatedAt,
        };

        var shared = application.ToShared();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shared.Id, Is.EqualTo(7));
            Assert.That(shared.PlayerUserId.UserId, Is.EqualTo(PlayerId));
            Assert.That(shared.PlayerName, Is.EqualTo("Urist"));
            Assert.That(shared.Message, Is.EqualTo("hello"));
            Assert.That(shared.Status, Is.EqualTo(WhitelistApplicationStatus.Pending));
            Assert.That(shared.CreatedAt, Is.EqualTo(CreatedAt));
            Assert.That(shared.ReviewedByName, Is.Null);
            Assert.That(shared.ReviewedAt, Is.Null);
        }
    }

    [Test]
    public void MapsReviewedApplication()
    {
        var reviewedAt = CreatedAt.AddHours(1);

        var application = new WhitelistApplication
        {
            Id = 8,
            PlayerUserId = PlayerId,
            Player = new Player { UserId = PlayerId, LastSeenUserName = "Urist" },
            Message = "hello",
            Status = WhitelistApplicationStatus.Denied,
            CreatedAt = CreatedAt,
            ReviewedById = ReviewerId,
            ReviewedBy = new Player { UserId = ReviewerId, LastSeenUserName = "Admeen" },
            ReviewedAt = reviewedAt,
        };

        var shared = application.ToShared();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shared.Status, Is.EqualTo(WhitelistApplicationStatus.Denied));
            Assert.That(shared.ReviewedByName, Is.EqualTo("Admeen"));
            Assert.That(shared.ReviewedAt, Is.EqualTo(reviewedAt));
        }
    }

    [Test]
    public void FallsBackToUserIdWhenPlayerRowIsMissing()
    {
        var application = new WhitelistApplication
        {
            Id = 9,
            PlayerUserId = PlayerId,
            Message = "hello",
            Status = WhitelistApplicationStatus.Pending,
            CreatedAt = CreatedAt,
        };

        Assert.That(application.ToShared().PlayerName, Is.EqualTo(PlayerId.ToString()));
    }
}
