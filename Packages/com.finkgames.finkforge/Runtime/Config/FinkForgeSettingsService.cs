using UnityEngine;

namespace FinkForge.Config
{
    internal sealed class FinkForgeSettingsService
    {
        private const string ResourceName = "FinkForgeConfig";
        private FinkForgeConfig _cachedConfig;
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

        public string GameName => Config?.GameName ?? string.Empty;
        public bool IsConfigured => Config != null;
    }
}
