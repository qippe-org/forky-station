using Content.Server.Administration;
using Content.Shared._Qippe.Whitelist;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameWindow;
using Content.Shared.Players;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking
{
    [UsedImplicitly]
    public sealed partial class GameTicker
    {
        [Dependency] private IPlayerManager _playerManager = default!;

        private void InitializePlayer()
        {
            _playerManager.PlayerStatusChanged += PlayerStatusChanged;
        }

        private async void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            var session = args.Session;

            if (_mind.TryGetMind(session.UserId, out var mindId, out var mind))
            {
                if (args.NewStatus != SessionStatus.Disconnected)
                {
                    _pvsOverride.AddSessionOverride(mindId.Value, session);
                }
            }

            DebugTools.Assert(session.GetMind() == mindId);

            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                    {
                        AddPlayerToDb(args.Session.UserId.UserId);

                        // Always make sure the client has player data.
                        if (session.Data.ContentDataUncast == null)
                        {
                            var data = new ContentPlayerData(session.UserId, args.Session.Name);
                            data.Mind = mindId;
                            session.Data.ContentDataUncast = data;
                        }

                        // Make the player actually join the game.
                        // timer time must be > tick length
                        Timer.Spawn(0, () => _playerManager.JoinGame(args.Session));

                        var record = await _db.GetPlayerRecordByUserId(args.Session.UserId);
                        var firstConnection = record != null &&
                                            Math.Abs((record.FirstSeenTime - record.LastSeenTime).TotalMinutes) < 30; // 1 -> 30
                        var createdTime = "Error while getting createdTime";
                        var userId = args.Session.UserId.ToString();

                        if (!args.Session.Name.StartsWith("localhost"))
                        {
                            try
                            {
                                createdTime = await AdminApiHelpers.GetCreatedTime(userId);
                            }
                            catch (Exception ex)
                            {
                                _sawmill.Warning($"Error while getting createdTime: {ex.Message}");
                            }
                        }
                        else
                        {
                            _sawmill.Info($"Player {args.Session.Name} is a localhost, ignoring getting createdTime...");
                        }

                        if (createdTime != null)
                            _chatManager.SendAdminAnnouncement(firstConnection
                              ? Loc.GetString("player-first-join-message", ("name", args.Session.Name)) + " " +
                              Loc.GetString("player-created-time", ("createdTime", createdTime))
                              : Loc.GetString("player-join-message", ("name", args.Session.Name)));

                        RaiseNetworkEvent(GetConnectionStatusMsg(), session.Channel);

                        if (firstConnection && _cfg.GetCVar(CCVars.AdminNewPlayerJoinSound))
                            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/newplayerping.ogg"),
                                Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false,
                                audioParams: new AudioParams { Volume = -5f });

                        if (LobbyEnabled && _roundStartCountdownHasNotStartedYetDueToNoPlayers)
                        {
                            _roundStartCountdownHasNotStartedYetDueToNoPlayers = false;
                            _roundStartTime = _gameTiming.CurTime + LobbyDuration;
                        }

                        break;
                    }

                case SessionStatus.InGame:
                    {
                        _userDb.ClientConnected(session);

                        // A player the whitelist is blocking may only use the application form. Sending them to the
                        // lobby here is what would otherwise let them ready up and reach the round.
                        if (_whitelistGate.IsGated(session.UserId))
                        {
                            EnterWhitelistApplication(session);
                            break;
                        }

                        if (mind == null)
                        {
                            if (LobbyEnabled)
                                PlayerJoinLobby(session);
                            else
                                SpawnWaitDb();

                            _adminLogger.Add(LogType.Connection, LogImpact.Low, $"User {args.Session:Player} attached to {(args.Session.AttachedEntity != null ? ToPrettyString(args.Session.AttachedEntity) : "nothing"):entity} connected to the game.");
                            break;
                        }

                        if (mind.CurrentEntity == null || Deleted(mind.CurrentEntity))
                        {
                            DebugTools.Assert(mind.CurrentEntity == null, "a mind's current entity was deleted without updating the mind");

                            // This player is joining the game with an existing mind, but the mind has no entity.
                            // Their entity was probably deleted sometime while they were disconnected, or they were an observer.
                            // Instead of allowing them to spawn in, we will dump and their existing mind in an observer ghost.
                            SpawnObserverWaitDb();
                        }
                        else
                        {
                            if (_playerManager.SetAttachedEntity(session, mind.CurrentEntity))
                            {
                                PlayerJoinGame(session);
                            }
                            else
                            {
                                Log.Error(
                                    $"Failed to attach player {session} with mind {ToPrettyString(mindId)} to its current entity {ToPrettyString(mind.CurrentEntity)}");
                                SpawnObserverWaitDb();
                            }
                        }

                        _adminLogger.Add(LogType.Connection, LogImpact.Low, $"User {args.Session:Player} attached to {(args.Session.AttachedEntity != null ? ToPrettyString(args.Session.AttachedEntity) : "nothing"):entity} connected to the game.");

                        break;
                    }

                case SessionStatus.Disconnected:
                    {
                        _chatManager.SendAdminAnnouncement(Loc.GetString("player-leave-message", ("name", args.Session.Name)));
                        if (mindId != null)
                        {
                            _pvsOverride.RemoveSessionOverride(mindId.Value, session);
                        }

                        _userDb.ClientDisconnected(session);

                        _adminLogger.Add(LogType.Connection, LogImpact.Low, $"User {args.Session:Player} attached to {(args.Session.AttachedEntity != null ? ToPrettyString(args.Session.AttachedEntity) : "nothing"):entity} disconnected from the game.");
                        break;
                    }
            }
            //When the status of a player changes, update the server info text
            UpdateInfoText();

            async void SpawnWaitDb()
            {
                try
                {
                    await _userDb.WaitLoadComplete(session);
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                SpawnPlayer(session, EntityUid.Invalid);
            }

            async void SpawnObserverWaitDb()
            {
                try
                {
                    await _userDb.WaitLoadComplete(session);
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                JoinAsObserver(session);
            }

            async void AddPlayerToDb(Guid id)
            {
                if (RoundId != 0 && _runLevel != GameRunLevel.PreRoundLobby)
                {
                    await _db.AddRoundPlayers(RoundId, id);
                }
            }
        }

        public HumanoidCharacterProfile GetPlayerProfile(ICommonSession p)
        {
            return (HumanoidCharacterProfile) _prefsManager.GetPreferences(p.UserId).SelectedCharacter;
        }

        public void PlayerJoinGame(ICommonSession session, bool silent = false)
        {
            if (!silent)
                _chatManager.DispatchServerMessage(session, Loc.GetString("game-ticker-player-join-game-message"));

            _playerGameStatuses[session.UserId] = PlayerGameStatus.JoinedGame;
            _db.AddRoundPlayers(RoundId, session.UserId);

            if (_adminManager.HasAdminFlag(session, AdminFlags.Admin))
            {
                if (_allPreviousGameRules.Count > 0)
                {
                    var rulesMessage = GetGameRulesListMessage(true);
                    _chatManager.SendAdminAnnouncementMessage(session, Loc.GetString("starting-rule-selected-preset", ("preset", rulesMessage)));
                }
            }

            RaiseNetworkEvent(new TickerJoinGameEvent(), session.Channel);
        }

        /// <summary>
        /// Sends a player the whitelist application screen instead of the lobby.
        /// </summary>
        /// <remarks>
        /// No game status is recorded, so the player never counts towards the ready count and is never eligible to be
        /// spawned when the round starts.
        /// </remarks>
        private async void EnterWhitelistApplication(ICommonSession session)
        {
            var hasApplication = await _whitelistGate.HasApplicationAsync(session.UserId);

            // The player may have disconnected while the query was running.
            if (!_playerManager.TryGetSessionById(session.UserId, out var current) || current != session)
                return;

            RaiseNetworkEvent(new TickerJoinWhitelistApplicationEvent(hasApplication), session.Channel);
        }

        /// <summary>
        /// Spawns a player once their database load finishes, the same way the normal join path does.
        /// </summary>
        private async void SpawnWaitDbForSession(ICommonSession session)
        {
            try
            {
                await _userDb.WaitLoadComplete(session);
            }
            catch (OperationCanceledException)
            {
                // The player disconnected or the round restarted.
                return;
            }

            SpawnPlayer(session, EntityUid.Invalid);
        }

        /// <summary>
        /// Puts a player whose whitelist gate was just lifted back on the normal join path.
        /// </summary>
        public void SendToLobbyAfterWhitelist(NetUserId player)
        {
            if (!_playerManager.TryGetSessionById(player, out var session))
                return;

            // They never got a game status while gated, so this is their first real entry into the round flow.
            if (LobbyEnabled)
            {
                PlayerJoinLobby(session);
            }
            else
            {
                _playerGameStatuses[session.UserId] = PlayerGameStatus.JoinedGame;
                SpawnWaitDbForSession(session);
            }
        }

        /// <summary>
        /// Re-sends the application screen state to a gated player, so the screen stops offering the form once they
        /// have submitted an application.
        /// </summary>
        public void RefreshWhitelistApplicationScreen(NetUserId player)
        {
            if (!_whitelistGate.IsGated(player))
                return;

            if (!_playerManager.TryGetSessionById(player, out var session))
                return;

            EnterWhitelistApplication(session);
        }

        private void PlayerJoinLobby(ICommonSession session)
        {
            if (_whitelistGate.IsGated(session.UserId))
            {
                EnterWhitelistApplication(session);
                return;
            }

            _playerGameStatuses[session.UserId] = LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;

            // Before the first round starts there is no round row to record players against, and inserting one
            // against round 0 violates the foreign key. AddPlayerToDb guards the same way.
            if (RoundId != 0)
                _db.AddRoundPlayers(RoundId, session.UserId);

            var client = session.Channel;
            RaiseNetworkEvent(new TickerJoinLobbyEvent(), client);
            RaiseNetworkEvent(GetStatusMsg(session), client);
            RaiseNetworkEvent(GetInfoMsg(), client);
            RaiseLocalEvent(new PlayerJoinedLobbyEvent(session));
        }

        private void ReqWindowAttentionAll()
        {
            RaiseNetworkEvent(new RequestWindowAttentionEvent());
        }
    }

    public sealed class PlayerJoinedLobbyEvent : EntityEventArgs
    {
        public readonly ICommonSession PlayerSession;

        public PlayerJoinedLobbyEvent(ICommonSession playerSession)
        {
            PlayerSession = playerSession;
        }
    }
}
