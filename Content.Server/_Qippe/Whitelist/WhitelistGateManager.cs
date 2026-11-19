using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Qippe.Whitelist;

/// <summary>
/// Tracks players the whitelist is blocking but who are still allowed to apply.
/// A gated player stays connected so they can fill in the application form, but is kept out of the lobby and the
/// round until an admin approves them.
/// </summary>
/// <remarks>
/// The gate is deliberately server side. The client state is only a presentation of it, so a modified client cannot
/// leave the state and join.
/// </remarks>
public sealed partial class WhitelistGateManager : IPostInjectInit
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    // Written from the connection thread, read from the main thread.
    private readonly ConcurrentDictionary<NetUserId, byte> _gated = new();
    private ISawmill _sawmill = default!;

    /// <summary>
    /// Marks a player as allowed to connect for the sole purpose of submitting an application.
    /// </summary>
    public void Gate(NetUserId player)
    {
        if (_gated.TryAdd(player, 0))
            _sawmill.Info("Gating {Player} behind the whitelist application form.", player);
    }

    /// <summary>
    /// True when the player is connected but must not reach the lobby, spawn or observer.
    /// </summary>
    public bool IsGated(NetUserId player)
    {
        return _gated.ContainsKey(player);
    }

    /// <summary>
    /// Lifts the gate, letting the player through on their next connection or round.
    /// </summary>
    public void Clear(NetUserId player)
    {
        _gated.TryRemove(player, out _);
    }

    /// <summary>
    /// Lifts the gate and, when the player is connected, puts them on the normal join path right away.
    /// </summary>
    /// <remarks>
    /// Call this after granting the whitelist, so a player sitting on the application screen does not have to
    /// reconnect to notice they were let in.
    /// </remarks>
    public void Release(NetUserId player)
    {
        if (!_gated.TryRemove(player, out _))
            return;

        _entityManager.SystemOrNull<GameTicker>()?.SendToLobbyAfterWhitelist(player);
    }

    /// <summary>
    /// Lifts the gate on every tracked player, for when the whitelist itself is turned off.
    /// </summary>
    public void ReleaseAll()
    {
        // Snapshot first: Release removes from the dictionary while we iterate.
        foreach (var player in _gated.Keys.ToArray())
        {
            Release(player);
        }
    }

    /// <summary>
    /// Re-sends the application screen state to a gated player, so the screen stops offering the form once they have
    /// submitted an application.
    /// </summary>
    public void RefreshApplicationScreen(NetUserId player)
    {
        if (_gated.ContainsKey(player))
            _entityManager.SystemOrNull<GameTicker>()?.RefreshWhitelistApplicationScreen(player);
    }

    /// <summary>
    /// True when the player already has an application on file, whatever its status.
    /// </summary>
    public async Task<bool> HasApplicationAsync(NetUserId player)
    {
        return await _db.GetWhitelistApplicationByPlayerAsync(player) != null;
    }

    void IPostInjectInit.PostInject()
    {
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        _sawmill = _logManager.GetSawmill("whitelist_application");
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            Clear(args.Session.UserId);
    }
}
