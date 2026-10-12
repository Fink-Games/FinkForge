using System;
using System.Collections.Generic;
using FinkForge.Core;

namespace FinkForge
{
    public static class Fink
    {
        public static class Application
        {
            public static void Quit() => FinkRuntime.Instance.RequestQuit();
        }

        public static class Players
        {
            public static IReadOnlyList<FinkPlayer> GetPlayers() => FinkRuntime.Instance.Players.GetPlayers();
            public static bool UsesTeams => FinkRuntime.Instance.Players.UsesTeams;
            public static void RemovePlayer(int seatId) => FinkRuntime.Instance.Players.RemovePlayer(seatId);
            public static event Action OnPlayersReady
            {
                add => FinkRuntime.Instance.Players.PlayersReady += value;
                remove => FinkRuntime.Instance.Players.PlayersReady -= value;
            }
            public static event Action<FinkPlayer> OnPlayerLeft
            {
                add => FinkRuntime.Instance.Players.PlayerLeft += value;
                remove => FinkRuntime.Instance.Players.PlayerLeft -= value;
            }
            public static event Action<int, int> OnScoreChanged
            {
                add => FinkRuntime.Instance.Players.ScoreChanged += value;
                remove => FinkRuntime.Instance.Players.ScoreChanged -= value;
            }
            public static event Action OnScoresCleared
            {
                add => FinkRuntime.Instance.Players.ScoresCleared += value;
                remove => FinkRuntime.Instance.Players.ScoresCleared -= value;
            }
            public static void SetScore(int seatId, int score) => FinkRuntime.Instance.Players.SetScore(seatId, score);
            public static void AddScore(int seatId, int amount) => FinkRuntime.Instance.Players.AddScore(seatId, amount);
            public static bool TryGetScore(int seatId, out int score) => FinkRuntime.Instance.Players.TryGetScore(seatId, out score);
        }

        public static class Config
        {
            public static float MusicVolume => FinkRuntime.Instance.Config.MusicVolume;
            public static float SFXVolume => FinkRuntime.Instance.Config.SfxVolume;
            public static string Language => FinkRuntime.Instance.Config.Language;
        }

        public static class App
        {
            public static event Action<bool> OnScoresSent
            {
                add => FinkRuntime.Instance.App.ScoresSent += value;
                remove => FinkRuntime.Instance.App.ScoresSent -= value;
            }
            public static event Action OnSessionStarted
            {
                add => FinkRuntime.Instance.App.SessionStarted += value;
                remove => FinkRuntime.Instance.App.SessionStarted -= value;
            }
            public static void Start() => FinkRuntime.Instance.App.Start();
            public static void Restart() => FinkRuntime.Instance.App.Restart();
            public static void End() => FinkRuntime.Instance.App.End();
            public static void SendScores() => FinkRuntime.Instance.App.SendScores();
        }

    }
}
