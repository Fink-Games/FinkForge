using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FinkForge.Core;

namespace FinkForge.Players
{
    internal sealed class PlayerService
    {
        private readonly List<FinkPlayer> players = new List<FinkPlayer>();
        private readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        private readonly Func<FinkSessionState> sessionState;
        private readonly Func<bool> scoreEnabled;
        public event Action<FinkPlayer> PlayerLeft;
        public event Action PlayersReady;
        public event Action<int, int> ScoreChanged;
        public event Action ScoresCleared;
        internal event Action AllPlayersLeft;
        internal bool ScoresTouched { get; private set; }

        public PlayerService(Func<FinkSessionState> sessionState, Func<bool> scoreEnabled)
        {
            this.sessionState = sessionState;
            this.scoreEnabled = scoreEnabled;
        }

        public IReadOnlyList<FinkPlayer> GetPlayers() => players.AsReadOnly();
        public bool UsesTeams => players.Any(player => player.TeamId.HasValue);

        public void SetScore(int seatId, int score)
        {
            if (!scoreEnabled()) return;
            if (!IsActiveSession())
            {
                Debug.LogWarning("[Fink Forge] SetScore ignored because there is no active session.");
                return;
            }

            scores[seatId] = score;
            ScoresTouched = true;
            ScoreChanged?.Invoke(seatId, score);
        }

        public void AddScore(int seatId, int amount)
        {
            scores.TryGetValue(seatId, out var currentScore);
            SetScore(seatId, currentScore + amount);
        }

        public bool TryGetScore(int seatId, out int score) => scores.TryGetValue(seatId, out score);

        public PlayerScoreSnapshot[] SnapshotScores()
        {
            return scores.Select(score =>
            {
                var player = players.FirstOrDefault(candidate => candidate.SeatId == score.Key);
                return new PlayerScoreSnapshot(score.Key, player?.Nickname ?? string.Empty, score.Value);
            }).ToArray();
        }

        public TeamScoreSnapshot[] SnapshotTeamScores()
        {
            return scores
                .Select(score => new
                {
                    Score = score,
                    Player = players.FirstOrDefault(candidate => candidate.SeatId == score.Key)
                })
                .Where(entry => entry.Player != null && entry.Player.TeamId.HasValue)
                .GroupBy(entry => entry.Player.TeamId.Value)
                .Select(team => new TeamScoreSnapshot(team.Key, team.Sum(entry => entry.Score.Value)))
                .ToArray();
        }


        internal void ReplacePlayers(IEnumerable<FinkPlayer> incoming)
        {
            players.Clear();
            if (incoming != null) players.AddRange(incoming.OrderBy(player => player.SeatId));
            PlayersReady?.Invoke();
        }

        public void RemovePlayer(int seatId)
        {
            var player = players.FirstOrDefault(candidate => candidate.SeatId == seatId);
            if (player == null || !players.Remove(player)) return;

            PlayerLeft?.Invoke(player);
            if (players.Count == 0) AllPlayersLeft?.Invoke();
        }

        internal void ClearScores()
        {
            scores.Clear();
            ScoresTouched = false;
            ScoresCleared?.Invoke();
        }

        private bool IsActiveSession() => sessionState() == FinkSessionState.Active;
    }

    internal readonly struct PlayerScoreSnapshot
    {
        public readonly int SeatId;
        public readonly string PlayerName;
        public readonly int Score;
        public PlayerScoreSnapshot(int seatId, string playerName, int score)
        {
            SeatId = seatId;
            PlayerName = playerName;
            Score = score;
        }
    }

    internal readonly struct TeamScoreSnapshot
    {
        public readonly int TeamId;
        public readonly int Score;

        public TeamScoreSnapshot(int teamId, int score)
        {
            TeamId = teamId;
            Score = score;
        }
    }

}
