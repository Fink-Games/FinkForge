using System;
using UnityEngine;
using FinkForge.Application;
using FinkForge.Config;
using FinkForge.Device;
using FinkForge.Environment;
using FinkForge.Game;
using FinkForge.Inactivity;
using FinkForge.Players;
using FinkForge.Transport;
using Cysharp.Threading.Tasks;

namespace FinkForge.Core
{
    internal sealed class FinkRuntime
    {
        private static readonly Lazy<FinkRuntime> Lazy = new Lazy<FinkRuntime>(() => new FinkRuntime());
        public static FinkRuntime Instance => Lazy.Value;

        private readonly FinkRuntimeState _state = new FinkRuntimeState();
        private readonly FinkEnvironment _environment;
        private readonly FinkHttpTransport _transport;
        private readonly LaunchSecurityService _launchSecurity;
        private readonly FinishDeliveryOutbox _finishOutbox;
        private readonly FinkForgeSettingsService _settings;

        public readonly PlayerService Players;
        public readonly ConfigService Config;
        public readonly GameService App;
        public readonly DeviceService Device;
        private readonly HeartbeatService _heartbeat;
        private readonly InactivityService _inactivity;
        private readonly PauseService _pause;
        private readonly LightService _lights;
        private readonly ApplicationService _application;

        private FinkRuntime()
        {
            _settings = new FinkForgeSettingsService();
            _environment = new FinkEnvironment(null, _settings.Config);
            _transport = new FinkHttpTransport(_environment);
            _launchSecurity = new LaunchSecurityService();
            _finishOutbox = new FinishDeliveryOutbox(_transport);
            Config = new ConfigService();
            Players = new PlayerService(() => _state.CurrentSession?.State ?? FinkSessionState.None,
                () => Config.ScoreEnabled);
            _heartbeat = new HeartbeatService(_transport);
            App = new GameService(Players, Config, _finishOutbox, _heartbeat, _state, _settings);
            _pause = new PauseService(_transport, OnPauseStateChanged);
            _lights = new LightService(_transport);
            App.SessionStarted += () => _pause.Start(Time.realtimeSinceStartup);
            _inactivity = new InactivityService(Config,
                () => _state.CurrentSession != null && _state.CurrentSession.State == FinkSessionState.Active,
                () => _pause != null && _pause.IsPaused);
            _application = new ApplicationService(App.Quit);
            Device = new DeviceService(_finishOutbox, RequestQuit);
            Players.AllPlayersLeft += RequestQuit;
            UnityEngine.Application.logMessageReceived += OnLogMessage;
        }

        public void BeginInitialization()
        {
            if (_state.Initialization != FinkInitializationState.None) return;
            _state.Initialization = FinkInitializationState.Initializing;
            InitializeAsync().Forget();
        }

        private async UniTask InitializeAsync()
        {
            try
            {
                await UniTask.SwitchToMainThread();
                _environment.Resolve();
                if (_environment.TryGetConfigurationError(out var configurationError))
                    throw new InvalidOperationException(configurationError);
                if (!_launchSecurity.Validate(_environment, out var tokenError))
                    throw new InvalidOperationException(tokenError);
                _transport.Prepare();
                await _transport.LoadBootstrapDataAsync(Players, Config);
                _inactivity.Start();
                _state.Initialization = FinkInitializationState.Ready;
                App.FlushPendingStart();
            }
            catch (Exception exception)
            {
                _state.Initialization = FinkInitializationState.Failed;
                App.CancelPendingStart();
                Debug.LogError($"[Fink Forge] Initialization failed: {exception.Message}");
            }
        }

        public void Tick()
        {
            _inactivity.Tick();
            _heartbeat.Tick(Time.realtimeSinceStartup);
            if (_state.CurrentSession == null || _state.CurrentSession.State != FinkSessionState.Active)
                _pause.Stop();
            _pause.Tick(Time.realtimeSinceStartup);
            _finishOutbox.ProcessPending(Time.realtimeSinceStartup);
        }

        public void RequestQuit() => _application.Quit();

        public bool InactivityWarningActive => _inactivity.IsWarningActive;
        public float InactivityWarningRemainingSeconds => _inactivity.WarningRemainingSeconds;
        public bool IsPaused => _pause.IsPaused;
        public void SendLightEvent(string type, int? seatId, int[] seats, string intensity) =>
            _lights.SendEvent(type, seatId, seats, intensity);
        public void SetLightTurn(int? seatId) => _lights.SetTurn(seatId);
        public void SetLightTimer(int durationMs, int? seatId) => _lights.SetTimer(durationMs, seatId);
        public void ClearLightTimer() => _lights.ClearTimer();
        public void ClearLightState() => _lights.ClearState();
        public event Action<bool, string, string> PauseStateChanged;
        public event Action InactivityTimedOut
        {
            add => _inactivity.TimedOut += value;
            remove => _inactivity.TimedOut -= value;
        }
        public event Action<float> InactivityWarningStarted
        {
            add => _inactivity.WarningStarted += value;
            remove => _inactivity.WarningStarted -= value;
        }
        public event Action<float> InactivityWarningUpdated
        {
            add => _inactivity.WarningUpdated += value;
            remove => _inactivity.WarningUpdated -= value;
        }
        public event Action InactivityWarningCancelled
        {
            add => _inactivity.WarningCancelled += value;
            remove => _inactivity.WarningCancelled -= value;
        }

        private void OnPauseStateChanged(bool paused, FinkPauseState state)
        {
            PauseStateChanged?.Invoke(paused, state?.Reason ?? string.Empty, state?.Until ?? string.Empty);
        }

        public void ConfigureGame(string gameName)
        {
            _settings.ConfigureGame(gameName);
        }

        public bool LaunchIsConfigured => _environment.IsConfigured;
        public string LaunchAccessToken => _environment.AccessToken;
        public string LaunchInstallationId => _environment.InstallationId;
        public string LaunchDeviceId => _environment.DeviceId;
        public string LaunchNonce => _environment.LaunchNonce;
        public string LaunchApiKey => _environment.ApiKey;
        public string LaunchApiPort => _environment.ApiPort;
        public string LaunchAccessTokenPublicKeyB64 => _environment.AccessTokenPublicKeyB64;
        public string LaunchAccessTokenIssuer => _environment.AccessTokenIssuer;
        public string LaunchAccessTokenAudience => _environment.AccessTokenAudience;
        public string LaunchAccessTokenPurpose => _environment.AccessTokenPurpose;
        public string LaunchAccessTokenAlgorithm => _environment.AccessTokenAlgorithm;
        public int LaunchTokenClockSkewSeconds => _environment.TokenClockSkewSeconds;
        public string LaunchAccessTokenPublicKeyPem => _environment.AccessTokenPublicKeyPem;

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[Fink Forge]", StringComparison.Ordinal))
                return;

            var reasonType = type == LogType.Exception ? "crash" : "error";
            Device.ReportAutomaticIncident(reasonType, condition, stackTrace);
        }

    }
}
