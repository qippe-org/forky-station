using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Qippe.QVars;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._Qippe.ServerStatus;

public sealed partial class ServerStatusManager : IPostInjectInit
{
    [Dependency] private IStatusHost _statusHost = default!;
    [Dependency] private ISharedPlayerManager _sharedPlayerManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerDbManager _db = default!;

    private const string PlayerListPath = "/playerlist";
    private const string BanNotePath = "/bannote";
    private const string AdminNotePath = "/adminnote";
    private const string AdminWatchlistPath = "/adminwatchlist";
    private const string AdminMessagePath = "/adminmessage";
    private const string PlayerPath = "/player";

    void IPostInjectInit.PostInject()
    {
    }

    public void Initialize()
    {
        _statusHost.AddHandler(HandlePlayerList);
        _statusHost.AddHandler(HandlePlayerRecord);
        _statusHost.AddHandler(HandleBanNote);
        _statusHost.AddHandler(HandleAdminNote);
        _statusHost.AddHandler(HandleAdminWatchlist);
        _statusHost.AddHandler(HandleAdminMessage);
    }

    private async Task<bool> HandlePlayerList(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not PlayerListPath)
            return false;

        var enabled = _cfg.GetCVar(QVars.StatusPlayerList);
        if (!enabled)
        {
            await statusHandlerContext.RespondErrorAsync(HttpStatusCode.Forbidden);
            return false;
        }

        var playerCount = _sharedPlayerManager.PlayerCount;
        var playerList = new JsonArray();
        var jObject = new JsonObject()
        {
            ["playerCount"] = playerCount,
            ["players"] = playerList
        };

        foreach (var player in _sharedPlayerManager.Sessions)
        {
            playerList.Add(player.Name);
        }

        await statusHandlerContext.RespondJsonAsync(jObject);
        return true;
    }

    private async Task<bool> HandlePlayerRecord(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not PlayerPath)
            return false;

        var enabled = _cfg.GetCVar(QVars.StatusPlayerRecord);
        if (!enabled)
        {
            await statusHandlerContext.RespondErrorAsync(HttpStatusCode.Forbidden);
            return false;
        }

        var nameString = statusHandlerContext.Url.Query.TrimStart("?").ToString();
        var playerRecord = _db.GetPlayerRecordByUserName(nameString)
            .GetAwaiter()
            .GetResult();

        if (playerRecord is null)
            return false;

        var jObject = new JsonObject
        {
            ["netUserId"] = playerRecord.UserId.ToString(),
            ["lastSeenName"] = playerRecord.LastSeenUserName,
            ["firstSeenTime"] = playerRecord.FirstSeenTime.ToString(),
            ["lastSeenTime"] = playerRecord.LastSeenTime.ToString()
        };

        await statusHandlerContext.RespondJsonAsync(jObject);
        return true;
    }

    private async Task<bool> HandleBanNote(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not BanNotePath)
            return false;

        var enabled = _cfg.GetCVar(QVars.StatusBanNote);
        if (!enabled)
        {
            await statusHandlerContext.RespondErrorAsync(HttpStatusCode.Forbidden);
            return false;
        }

        var banNameString = statusHandlerContext.Url.Query.TrimStart("?");

        if (int.TryParse(banNameString, null, out var result))
        {
            var banNoteRecord = _db.GetBanAsNoteAsync(result)
                .GetAwaiter()
                .GetResult();

            if (banNoteRecord is null)
                return false;

            var rounds = new JsonArray();
            var players = new JsonArray();
            var roles = new JsonArray();
            var jObject = new JsonObject()
            {
                ["id"] = banNoteRecord.Id,
                ["type"] = banNoteRecord.Type.ToString(),
                ["severity"] = banNoteRecord.Severity.ToString(),
                ["message"] = banNoteRecord.Message,
                ["rounds"] = rounds,
                ["players"] = players,
                ["roles"] = roles,
                ["playtimeAtNote"] = banNoteRecord.PlaytimeAtNote.ToString(),
                ["createdAt"] = banNoteRecord.CreatedAt
            };

            foreach (var round in banNoteRecord.Rounds)
            {
                rounds.Add(round);
            }

            foreach (var player in banNoteRecord.Players)
            {
                players.Add(new PlayerRecord(player.UserId,
                    player.FirstSeenTime,
                    player.LastSeenUserName,
                    player.LastSeenTime,
                    null,
                    null));
            }

            foreach (var role in banNoteRecord.Roles)
            {
                roles.Add(role.RoleId);
            }

            await statusHandlerContext.RespondJsonAsync(jObject);
        }

        return true;
    }

    private async Task<bool> HandleAdminNote(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not AdminNotePath)
            return false;

        var adminNoteId = statusHandlerContext.Url.Query.TrimStart("?");

        if (int.TryParse(adminNoteId, null, out var result))
        {
            var adminNote = _db.GetAdminNote(result)
                .GetAwaiter()
                .GetResult();

            if (adminNote is null)
                return false;

            if (adminNote.Secret is true)
            {
                await statusHandlerContext.RespondErrorAsync(HttpStatusCode.Forbidden);
                return false;
            }

            var jObject = new JsonObject
            {
                ["id"] = adminNote.Id,
                ["severity"] = adminNote.Severity.ToString(),
                ["message"] = adminNote.Message,
                ["createdAt"] = adminNote.CreatedAt,
                ["expirationTime"] = adminNote.ExpirationTime
            };

            if (adminNote.Round is not null)
                jObject["round"] = adminNote.Round.Id;

            if (adminNote.Player is not null)
                jObject["player"] = adminNote.Player.LastSeenUserName;

            await statusHandlerContext.RespondJsonAsync(jObject);
        }

        return true;
    }

    private async Task<bool> HandleAdminWatchlist(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not AdminWatchlistPath)
            return false;

        var adminWatchlistId = statusHandlerContext.Url.Query.TrimStart("?");

        if (int.TryParse(adminWatchlistId, null, out var result))
        {
            var adminWatchlist = _db.GetAdminWatchlist(result)
                .GetAwaiter()
                .GetResult();

            if (adminWatchlist is null)
                return false;

            var jObject = new JsonObject
            {
                ["id"] = adminWatchlist.Id,
                ["message"] = adminWatchlist.Message,
                ["createdAt"] = adminWatchlist.CreatedAt,
                ["expirationTime"] = adminWatchlist.ExpirationTime,
            };

            if (adminWatchlist.Round is not null)
                jObject["round"] = adminWatchlist.Round.Id;

            if (adminWatchlist.Player is not null)
                jObject["player"] = adminWatchlist.Player.LastSeenUserName;

            await statusHandlerContext.RespondJsonAsync(jObject);
        }

        return true;
    }

    private async Task<bool> HandleAdminMessage(IStatusHandlerContext statusHandlerContext)
    {
        if (!statusHandlerContext.IsGetLike || statusHandlerContext.Url.AbsolutePath is not AdminMessagePath)
            return false;

        var adminMessageId = statusHandlerContext.Url.Query.TrimStart("?");

        if (int.TryParse(adminMessageId, null, out var result))
        {
            var adminMessage = _db.GetAdminMessage(result)
                .GetAwaiter()
                .GetResult();

            if (adminMessage is null)
                return false;

            var jObject = new JsonObject
            {
                ["id"] = adminMessage.Id,
                ["message"] = adminMessage.Message,
                ["createdAt"] = adminMessage.CreatedAt,
                ["expirationTime"] = adminMessage.ExpirationTime,
                ["seen"] = adminMessage.Seen,
                ["dismissed"] = adminMessage.Dismissed
            };

            if (adminMessage.Round is not null)
                jObject["round"] = adminMessage.Round.Id;

            if (adminMessage.Player is not null)
                jObject["player"] = adminMessage.Player.LastSeenUserName;

            await statusHandlerContext.RespondJsonAsync(jObject);
        }

        return true;
    }
}
