namespace FinkForge.Environment
{
    internal sealed class FinkEnvironment
    {
        private const string AccessTokenKey = "FINK_ACCESS_TOKEN";
        private const string InstallationIdKey = "FINK_INSTALLATION_ID";
        private const string DeviceIdKey = "FINK_DEVICE_ID";
        private const string LaunchNonceKey = "FINK_LAUNCH_NONCE";
        private const string ApiPortKey = "FINK_API_PORT";
        private const string ApiKeyKey = "FINK_API_KEY";

        public string BaseUrl { get; private set; }
        public string AccessToken { get; private set; }
        public string InstallationId { get; private set; }
        public string DeviceId { get; private set; }
        public string LaunchNonce { get; private set; }
        public string ApiKey { get; private set; }
        public string ApiPort { get; private set; }
        internal bool IsConfigured => TryGetConfigurationError(out _) == false;

        public void Resolve()
        {
            AccessToken = System.Environment.GetEnvironmentVariable(AccessTokenKey);
            InstallationId = System.Environment.GetEnvironmentVariable(InstallationIdKey);
            DeviceId = System.Environment.GetEnvironmentVariable(DeviceIdKey);
            LaunchNonce = System.Environment.GetEnvironmentVariable(LaunchNonceKey);
            ApiKey = System.Environment.GetEnvironmentVariable(ApiKeyKey);
            ApiPort = System.Environment.GetEnvironmentVariable(ApiPortKey);
            BaseUrl = BuildBaseUrl(ApiPort);
        }

        internal bool TryGetConfigurationError(out string error)
        {
            if (string.IsNullOrWhiteSpace(AccessToken))
            {
                error = "FINK_ACCESS_TOKEN is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(InstallationId))
            {
                error = "FINK_INSTALLATION_ID is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                error = "FINK_DEVICE_ID is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(LaunchNonce))
            {
                error = "FINK_LAUNCH_NONCE is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                error = "FINK_API_KEY is missing.";
                return true;
            }

            if (!int.TryParse(ApiPort, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
            {
                error = "FINK_API_PORT is missing or invalid.";
                return true;
            }

            error = string.Empty;
            return false;
        }

        private static string BuildBaseUrl(string port)
        {
            if (!int.TryParse(port, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
                return string.Empty;

            return $"http://localhost:{parsedPort}";
        }
    }
}
