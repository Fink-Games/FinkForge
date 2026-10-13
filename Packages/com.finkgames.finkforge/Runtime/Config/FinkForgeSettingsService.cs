using UnityEngine;

namespace FinkForge.Config
{
    internal sealed class FinkForgeSettingsService
    {
        private const string ResourceName = "FinkForgeConfig";
        private FinkForgeConfig _cachedConfig;
        private string _runtimeGameName;
        private bool _hasLoaded;

        internal FinkForgeSettingsService() { }

        internal FinkForgeSettingsService(FinkForgeConfig config)
        {
            _cachedConfig = config;
            _hasLoaded = true;
        }

        public FinkForgeConfig Config
        {
            get
            {
                if (_hasLoaded) return _cachedConfig;
                _cachedConfig = Resources.Load<FinkForgeConfig>(ResourceName);
                _hasLoaded = true;

                return _cachedConfig;
            }
        }

        public string GameName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_runtimeGameName))
                    return _runtimeGameName;

                return Config?.GameName?.Trim() ?? string.Empty;
            }
        }

        internal bool ConfigureGame(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
            {
                Debug.LogWarning("[Fink Forge] ConfigureGame ignored because gameName is empty.");
                return false;
            }

            _runtimeGameName = gameName.Trim();
            return true;
        }

        public bool IsConfigured => Config != null;
    }
}
