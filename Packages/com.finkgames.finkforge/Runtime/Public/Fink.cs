using System;
using System.Collections.Generic;
using FinkForge.Core;

namespace FinkForge
{
    public static class Fink
    {
        public static class Application
        {
            public static bool IsPaused => FinkRuntime.Instance.IsPaused;
            public static event Action<bool, string, string> OnPauseStateChanged
            {
                add => FinkRuntime.Instance.PauseStateChanged += value;
                remove => FinkRuntime.Instance.PauseStateChanged -= value;
            }
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

        public static class Inactivity
        {
            public static bool IsWarningActive => FinkRuntime.Instance.InactivityWarningActive;
            public static float WarningRemainingSeconds => FinkRuntime.Instance.InactivityWarningRemainingSeconds;
            public static event Action OnTimeout
            {
                add => FinkRuntime.Instance.InactivityTimedOut += value;
                remove => FinkRuntime.Instance.InactivityTimedOut -= value;
            }
            public static event Action<float> OnWarningStarted
            {
                add => FinkRuntime.Instance.InactivityWarningStarted += value;
                remove => FinkRuntime.Instance.InactivityWarningStarted -= value;
            }
            public static event Action<float> OnWarningUpdated
            {
                add => FinkRuntime.Instance.InactivityWarningUpdated += value;
                remove => FinkRuntime.Instance.InactivityWarningUpdated -= value;
            }
            public static event Action OnWarningCancelled
            {
                add => FinkRuntime.Instance.InactivityWarningCancelled += value;
                remove => FinkRuntime.Instance.InactivityWarningCancelled -= value;
            }
        }

        public static class Lights
        {
            public static void SendEvent(string type, int? seatId = null, int[] seats = null, string intensity = null) =>
                FinkRuntime.Instance.SendLightEvent(type, seatId, seats, intensity);
            public static void SetTurn(int seatId) => FinkRuntime.Instance.SetLightTurn(seatId);
            public static void ClearTurn() => FinkRuntime.Instance.SetLightTurn(null);
            public static void SetTimer(int durationMs, int? seatId = null) => FinkRuntime.Instance.SetLightTimer(durationMs, seatId);
            public static void ClearTimer() => FinkRuntime.Instance.ClearLightTimer();
            public static void ClearState() => FinkRuntime.Instance.ClearLightState();
        }

        public static class Launch
        {
            public static bool IsConfigured => FinkRuntime.Instance.LaunchIsConfigured;
            public static string AccessToken => FinkRuntime.Instance.LaunchAccessToken;
            public static string InstallationId => FinkRuntime.Instance.LaunchInstallationId;
            public static string DeviceId => FinkRuntime.Instance.LaunchDeviceId;
            public static string LaunchNonce => FinkRuntime.Instance.LaunchNonce;
            public static string ApiKey => FinkRuntime.Instance.LaunchApiKey;
            public static string ApiPort => FinkRuntime.Instance.LaunchApiPort;
            public static string AccessTokenPublicKeyB64 => FinkRuntime.Instance.LaunchAccessTokenPublicKeyB64;
            public static string AccessTokenIssuer => FinkRuntime.Instance.LaunchAccessTokenIssuer;
            public static string AccessTokenAudience => FinkRuntime.Instance.LaunchAccessTokenAudience;
            public static string AccessTokenPurpose => FinkRuntime.Instance.LaunchAccessTokenPurpose;
            public static string AccessTokenAlgorithm => FinkRuntime.Instance.LaunchAccessTokenAlgorithm;
            public static int TokenClockSkewSeconds => FinkRuntime.Instance.LaunchTokenClockSkewSeconds;
            public static string AccessTokenPublicKeyPem => FinkRuntime.Instance.LaunchAccessTokenPublicKeyPem;
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
            public static void ConfigureGame(string gameName) => FinkRuntime.Instance.ConfigureGame(gameName);
        }

    }
}
