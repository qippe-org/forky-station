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
    private const string PlayerPath = "/player";

    void IPostInjectInit.PostInject()
    {
    }

    public void Initialize()
    {
        _statusHost.AddHandler(HandlePlayerList);
        _statusHost.AddHandler(HandlePlayerRecord);
        _statusHost.AddHandler(HandleBanNote);
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
        var jObject = new JsonObject();

        foreach (var player in _sharedPlayerManager.Sessions)
        {
            playerList.Add(player.Name);
        }

        jObject["playerCount"] = playerCount;
        jObject["players"] = playerList;

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

        var jObject = new JsonObject();
        jObject["netUserId"] = playerRecord.UserId.ToString();
        jObject["lastSeenName"] = playerRecord.LastSeenUserName;
        jObject["firstSeenTime"] = playerRecord.FirstSeenTime.ToString();
        jObject["lastSeenTime"] = playerRecord.LastSeenTime.ToString();

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
            var banDef = _db.GetBanAsNoteAsync(result)
                .GetAwaiter()
                .GetResult();

            if (banDef is null)
                return false;

            var jObject = new JsonObject();
            var rounds = new JsonArray();
            var players = new JsonArray();
            var roles = new JsonArray();

            foreach (var round in banDef.Rounds)
            {
                rounds.Add(round);
            }

            foreach (var player in banDef.Players)
            {
                players.Add(new PlayerRecord(player.UserId,
                    player.FirstSeenTime,
                    player.LastSeenUserName,
                    player.LastSeenTime,
                    null,
                    null));
            }

            foreach (var role in banDef.Roles)
            {
                roles.Add(role.RoleId);
            }

            jObject["id"] = banDef.Id;
            jObject["type"] = banDef.Type.ToString();
            jObject["severity"] = banDef.Severity.ToString();
            jObject["message"] = banDef.Message;
            jObject["rounds"] = rounds;
            jObject["players"] = players;
            jObject["roles"] = roles;
            jObject["playtimeAtNote"] = banDef.PlaytimeAtNote.ToString();
            jObject["createdAt"] = banDef.CreatedAt;

            await statusHandlerContext.RespondJsonAsync(jObject);
        }

        return true;
    }
}
