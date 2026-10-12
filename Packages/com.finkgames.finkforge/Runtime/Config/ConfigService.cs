using UnityEngine;

namespace FinkForge.Config
{
    internal sealed class ConfigService
    {
        public int PlayerCount { get; private set; }
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        public float MusicVolume => Mathf.Clamp01(_musicVolume);
        public float SfxVolume => Mathf.Clamp01(_sfxVolume);
        public string Language { get; private set; } = string.Empty;
        public bool InactivityTimerEnabled { get; private set; }
        public float InactivityTimeoutSeconds { get; private set; }
        public float InactivityQuitCountdownSeconds { get; private set; }
        public bool ScoreEnabled { get; private set; }

        internal void ApplyRemoteConfig(int playerCount, string language, float musicVolume,
            float sfxVolume,
            bool inactivityTimerEnabled, float inactivityTimeoutSeconds, float inactivityQuitCountdownSeconds,
            bool scoreEnabled)
        {
            PlayerCount = Mathf.Max(0, playerCount);
            Language = language ?? string.Empty;
            this._musicVolume = Mathf.Clamp01(musicVolume);
            this._sfxVolume = Mathf.Clamp01(sfxVolume);
            InactivityTimerEnabled = inactivityTimerEnabled;
            InactivityTimeoutSeconds = Mathf.Max(0f, inactivityTimeoutSeconds);
            InactivityQuitCountdownSeconds = Mathf.Max(0f, inactivityQuitCountdownSeconds);
            ScoreEnabled = scoreEnabled;
        }
    }
}
