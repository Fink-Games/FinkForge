using System.Linq;
using UnityEngine;
using FinkForge.Config;
using FinkForge.Core;
using FinkForge.Players;
using FinkForge.Transport;

namespace FinkForge.Game
{
    internal sealed class GameService
    {
        private readonly PlayerService players;
        private readonly ConfigService config;
        private readonly FinishDeliveryOutbox outbox;
        private readonly HeartbeatService heartbeat;
        private readonly FinkRuntimeState runtimeState;
        private readonly FinkForgeSettingsService settings;
        private int lastRestartFrame = -1;
        public event System.Action<bool> ScoresSent;
        public event System.Action SessionStarted;

        public GameService(PlayerService players, ConfigService config, FinishDeliveryOutbox outbox,
            HeartbeatService heartbeat, FinkRuntimeState runtimeState, FinkForgeSettingsService settings)
        {
            this.players = players;
            this.config = config;
            this.outbox = outbox;
            this.heartbeat = heartbeat;
            this.runtimeState = runtimeState;
            this.settings = settings;
        }

        public void Start()
        {
            if (!CanRunOrDefer())
            {
                if (runtimeState.Initialization == FinkInitializationState.Initializing)
                    runtimeState.PendingStart = true;
                return;
            }

            var current = runtimeState.CurrentSession;
            if (current != null && (current.State == FinkSessionState.Active || current.State == FinkSessionState.Finishing))
            {
                Debug.LogWarning("[Fink Forge] App.Start ignored because a session is already active or finishing.");
                return;
            }

            runtimeState.PendingStart = false;
            players.ClearScores();
            var session = new FinkSession();
            runtimeState.CurrentSession = session;
            EnqueueAppStart(session);
        }

        public void FlushPendingStart()
        {
            if (!runtimeState.PendingStart || runtimeState.Initialization != FinkInitializationState.Ready) return;
            runtimeState.PendingStart = false;
            Start();
        }

        public void CancelPendingStart()
        {
            if (!runtimeState.PendingStart) return;
            runtimeState.PendingStart = false;
            Debug.LogError("[Fink Forge] Pending App.Start was discarded because initialization failed.");
        }

        public void End()
        {
            SendScores();
        }

        public void SendScores()
        {
            if (!CanRunOrDefer()) return;
            var session = runtimeState.CurrentSession;
            if (session == null || session.State != FinkSessionState.Active)
            {
                Debug.LogWarning("[Fink Forge] SendScores ignored because there is no active session.");
                return;
            }

            session.State = FinkSessionState.Finishing;
            heartbeat.Stop();
            var hasScores = players.ScoresTouched;
            var scoreDelivery = CreateScoreDelivery(session, success => CompleteScoreSend(session, success));
            if (scoreDelivery == null)
            {
                CompleteScoreSend(session, !hasScores);
                return;
            }

            outbox.EnqueueAndTryDeliver(scoreDelivery, Time.realtimeSinceStartup);
        }

        private void CompleteScoreSend(FinkSession session, bool success)
        {
            if (runtimeState.CurrentSession != session) return;
            session.State = FinkSessionState.Finished;
            ScoresSent?.Invoke(success);
        }

        public void Restart()
        {
            if (!CanRunOrDefer()) return;
            var current = runtimeState.CurrentSession;
            if (current == null || current.State == FinkSessionState.None) return;
            if (current.State == FinkSessionState.Finishing)
            {
                Debug.LogWarning("[Fink Forge] Restart ignored while score delivery is still running.");
                return;
            }
            if (lastRestartFrame == Time.frameCount)
            {
                Debug.LogWarning("[Fink Forge] Duplicate Restart ignored in the same frame.");
                return;
            }

            lastRestartFrame = Time.frameCount;
            heartbeat.Stop();
            var wasActive = current.State == FinkSessionState.Active;
            current.State = FinkSessionState.Finished;
            players.ClearScores();
            var nextSession = new FinkSession();
            runtimeState.CurrentSession = nextSession;
            if (wasActive)
                EnqueueAppRestart(nextSession);
            else
                EnqueueAppStart(nextSession);
        }

        public void Quit()
        {
            heartbeat.Stop();
            var session = runtimeState.CurrentSession;
            if (session == null || session.State == FinkSessionState.None || session.State == FinkSessionState.Finishing)
                return;

            if (session.State == FinkSessionState.Finished)
            {
                EnqueueAppEnd(session);
                return;
            }

            SendScoresForQuit(session);
        }

        private void SendScoresForQuit(FinkSession session)
        {
            session.State = FinkSessionState.Finishing;
            var scoreDelivery = CreateScoreDelivery(session, _ =>
            {
                session.State = FinkSessionState.Finished;
                EnqueueAppEnd(session);
            });
            if (scoreDelivery == null)
            {
                session.State = FinkSessionState.Finished;
                EnqueueAppEnd(session);
                return;
            }

            outbox.EnqueueAndTryDeliver(scoreDelivery, Time.realtimeSinceStartup);
        }

        private void EnqueueAppStart(FinkSession session)
        {
            var payload = new GameLifecyclePayload
            {
                status = "game_started",
                playerCount = config.PlayerCount,
                usesTeams = GetUsesTeams()
            };
            var delivery = new PendingDelivery(session.Id, FinkDeliveryType.AppStart, FinkJson.Serialize(payload),
                success =>
                {
                    if (success && runtimeState.CurrentSession == session && session.State == FinkSessionState.Active)
                    {
                        SessionStarted?.Invoke();
                        heartbeat.Start(Time.realtimeSinceStartup);
                        return;
                    }

                    if (!success && runtimeState.CurrentSession == session && session.State == FinkSessionState.Active)
                    {
                        session.State = FinkSessionState.Finished;
                        Debug.LogError("[Fink Forge] /v1/app/start was rejected; the session was not activated.");
                    }
                });
            outbox.EnqueueAndTryDeliver(delivery, Time.realtimeSinceStartup);
        }

        private void EnqueueAppRestart(FinkSession session)
        {
            var payload = new GameLifecyclePayload
            {
                status = "game_restart",
                playerCount = config.PlayerCount,
                usesTeams = GetUsesTeams()
            };
            var delivery = new PendingDelivery(session.Id, FinkDeliveryType.AppRestart, FinkJson.Serialize(payload),
                success =>
                {
                    if (success && runtimeState.CurrentSession == session && session.State == FinkSessionState.Active)
                    {
                        SessionStarted?.Invoke();
                        heartbeat.Start(Time.realtimeSinceStartup);
                        return;
                    }

                    if (!success && runtimeState.CurrentSession == session && session.State == FinkSessionState.Active)
                    {
                        session.State = FinkSessionState.Finished;
                        Debug.LogError("[Fink Forge] /v1/app/restart was rejected; the new session was not activated.");
                    }
                });
            outbox.EnqueueAndTryDeliver(delivery, Time.realtimeSinceStartup);
        }

        private PendingDelivery CreateScoreDelivery(FinkSession session, System.Action<bool> completion)
        {
            if (!config.ScoreEnabled) return null;
            if (session.ScoreDeliveryRequested || !players.ScoresTouched) return null;
            if (string.IsNullOrWhiteSpace(settings.GameName))
            {
                Debug.LogWarning("[Fink Forge] Score delivery skipped because the authoritative gameName is not configured.");
                return null;
            }

            session.ScoreDeliveryRequested = true;
            if (GetUsesTeams())
            {
                var teams = players.SnapshotTeamScores();
                if (teams.Length == 0)
                {
                    Debug.LogWarning("[Fink Forge] Team score delivery skipped because no valid team score could be built.");
                    return null;
                }

                var teamPayload = new TeamScorePayload
                {
                    gameName = settings.GameName,
                    playerCount = config.PlayerCount,
                    teams = System.Array.ConvertAll(teams, team => new TeamScoreEntry
                    {
                        teamId = team.TeamId,
                        score = team.Score
                    })
                };
                return new PendingDelivery(session.Id, FinkDeliveryType.TeamScores, FinkJson.Serialize(teamPayload), completion);
            }

            var scores = players.SnapshotScores();
            if (scores.Length == 0 || System.Array.Exists(scores, score => string.IsNullOrWhiteSpace(score.PlayerName)))
            {
                Debug.LogWarning("[Fink Forge] Individual score delivery skipped: authoritative playerName is unavailable.");
                return null;
            }

            var playerPayload = new PlayerScorePayload
            {
                gameName = settings.GameName,
                playerCount = config.PlayerCount,
                players = System.Array.ConvertAll(scores, score => new PlayerScoreEntry
                {
                    seatId = score.SeatId,
                    playerName = score.PlayerName,
                    score = score.Score
                })
            };
            return new PendingDelivery(session.Id, FinkDeliveryType.IndividualScores, FinkJson.Serialize(playerPayload), completion);
        }

        private void EnqueueAppEnd(FinkSession session)
        {
            if (session == null || session.EndRequested) return;
            session.EndRequested = true;
            var payload = new GameLifecyclePayload
            {
                status = "game_quit",
                playerCount = config.PlayerCount,
                usesTeams = GetUsesTeams()
            };
            outbox.EnqueueAndTryDeliver(new PendingDelivery(session.Id, FinkDeliveryType.AppEnd,
                FinkJson.Serialize(payload)), Time.realtimeSinceStartup);
        }

        private bool GetUsesTeams()
        {
            return players.GetPlayers().Any(player => player.TeamId.HasValue);
        }

        private bool CanRunOrDefer()
        {
            if (runtimeState.Initialization == FinkInitializationState.Ready) return true;
            Debug.LogWarning("[Fink Forge] Game lifecycle call ignored because Fink Forge is not ready.");
            return false;
        }
    }
}
