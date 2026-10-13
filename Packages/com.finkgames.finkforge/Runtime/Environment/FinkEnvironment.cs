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
        private const string PublicKeyB64Key = "FINK_ACCESS_TOKEN_PUBLIC_KEY_B64";
        private const string IssuerKey = "FINK_ACCESS_TOKEN_ISSUER";
        private const string AudienceKey = "FINK_ACCESS_TOKEN_AUDIENCE";
        private const string PurposeKey = "FINK_ACCESS_TOKEN_PURPOSE";
        private const string AlgorithmKey = "FINK_ACCESS_TOKEN_ALGORITHM";
        private const string ClockSkewKey = "FINK_ACCESS_TOKEN_CLOCK_SKEW";

        public string BaseUrl { get; private set; }
        public string AccessToken { get; private set; }
        public string InstallationId { get; private set; }
        public string DeviceId { get; private set; }
        public string LaunchNonce { get; private set; }
        public string ApiKey { get; private set; }
        public string ApiPort { get; private set; }
        public string AccessTokenPublicKeyB64 { get; private set; }
        public string AccessTokenIssuer { get; private set; }
        public string AccessTokenAudience { get; private set; }
        public string AccessTokenPurpose { get; private set; }
        public string AccessTokenAlgorithm { get; private set; }
        public int TokenClockSkewSeconds { get; private set; }
        public string AccessTokenPublicKeyPem { get; private set; }
        internal bool IsConfigured => TryGetConfigurationError(out _) == false;

        public void Resolve()
        {
            AccessToken = System.Environment.GetEnvironmentVariable(AccessTokenKey);
            InstallationId = System.Environment.GetEnvironmentVariable(InstallationIdKey);
            DeviceId = System.Environment.GetEnvironmentVariable(DeviceIdKey);
            LaunchNonce = System.Environment.GetEnvironmentVariable(LaunchNonceKey);
            ApiKey = System.Environment.GetEnvironmentVariable(ApiKeyKey);
            ApiPort = System.Environment.GetEnvironmentVariable(ApiPortKey);
            AccessTokenPublicKeyB64 = System.Environment.GetEnvironmentVariable(PublicKeyB64Key);
            AccessTokenIssuer = System.Environment.GetEnvironmentVariable(IssuerKey);
            AccessTokenAudience = System.Environment.GetEnvironmentVariable(AudienceKey);
            AccessTokenPurpose = System.Environment.GetEnvironmentVariable(PurposeKey);
            AccessTokenAlgorithm = System.Environment.GetEnvironmentVariable(AlgorithmKey);
            var clockSkew = System.Environment.GetEnvironmentVariable(ClockSkewKey);
            TokenClockSkewSeconds = int.TryParse(clockSkew, out var parsedClockSkew) && parsedClockSkew >= 0
                ? parsedClockSkew
                : -1;
            AccessTokenPublicKeyPem = TryConvertPublicKeyToPem(AccessTokenPublicKeyB64);
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

            if (string.IsNullOrWhiteSpace(AccessTokenPublicKeyB64) || string.IsNullOrWhiteSpace(AccessTokenPublicKeyPem))
            {
                error = "FINK_ACCESS_TOKEN_PUBLIC_KEY_B64 is missing or invalid.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(AccessTokenIssuer))
            {
                error = "FINK_ACCESS_TOKEN_ISSUER is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(AccessTokenAudience))
            {
                error = "FINK_ACCESS_TOKEN_AUDIENCE is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(AccessTokenPurpose))
            {
                error = "FINK_ACCESS_TOKEN_PURPOSE is missing.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(AccessTokenAlgorithm))
            {
                error = "FINK_ACCESS_TOKEN_ALGORITHM is missing.";
                return true;
            }

            if (TokenClockSkewSeconds < 0)
            {
                error = "FINK_ACCESS_TOKEN_CLOCK_SKEW is missing or invalid.";
                return true;
            }

            error = string.Empty;
            return false;
        }

        private static string TryConvertPublicKeyToPem(string encodedKey)
        {
            if (string.IsNullOrWhiteSpace(encodedKey)) return string.Empty;

            try
            {
                var keyBytes = System.Convert.FromBase64String(encodedKey);
                if (keyBytes.Length == 0) return string.Empty;

                var decodedText = System.Text.Encoding.UTF8.GetString(keyBytes).Trim();
                if (decodedText.Contains("-----BEGIN")) return decodedText;

                var derBase64 = System.Convert.ToBase64String(keyBytes);
                var lines = new System.Text.StringBuilder();
                lines.AppendLine("-----BEGIN PUBLIC KEY-----");
                for (var index = 0; index < derBase64.Length; index += 64)
                    lines.AppendLine(derBase64.Substring(index, System.Math.Min(64, derBase64.Length - index)));
                lines.Append("-----END PUBLIC KEY-----");
                return lines.ToString();
            }
            catch (System.FormatException)
            {
                return string.Empty;
            }
        }

        private static string BuildBaseUrl(string port)
        {
            if (!int.TryParse(port, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
                return string.Empty;

            return $"http://localhost:{parsedPort}";
        }
    }
}
