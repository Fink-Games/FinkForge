using System;
using UnityEngine;

namespace FinkForge.Environment
{
    internal sealed class FinkEnvironment
    {
        private const string PublicKeyResourcePath = "FinkForge/access-token-public-key";
        private const string AccessTokenKey = "FINK_ACCESS_TOKEN";
        private const string InstallationIdKey = "FINK_INSTALLATION_ID";
        private const string DeviceIdKey = "FINK_DEVICE_ID";
        private const string LaunchNonceKey = "FINK_LAUNCH_NONCE";
        private const string ApiPortKey = "FINK_API_PORT";
        private const string ApiKeyKey = "FINK_API_KEY";
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

        private readonly Func<string> publicKeyLoader;

        public FinkEnvironment(Func<string> publicKeyLoader = null)
        {
            this.publicKeyLoader = publicKeyLoader;
        }
        internal bool IsConfigured => TryGetConfigurationError(out _) == false;

        public void Resolve()
        {
            AccessToken = System.Environment.GetEnvironmentVariable(AccessTokenKey);
            InstallationId = System.Environment.GetEnvironmentVariable(InstallationIdKey);
            DeviceId = System.Environment.GetEnvironmentVariable(DeviceIdKey);
            LaunchNonce = System.Environment.GetEnvironmentVariable(LaunchNonceKey);
            ApiKey = System.Environment.GetEnvironmentVariable(ApiKeyKey);
            ApiPort = System.Environment.GetEnvironmentVariable(ApiPortKey);
            AccessTokenIssuer = System.Environment.GetEnvironmentVariable(IssuerKey);
            AccessTokenAudience = System.Environment.GetEnvironmentVariable(AudienceKey);
            AccessTokenPurpose = System.Environment.GetEnvironmentVariable(PurposeKey);
            AccessTokenAlgorithm = System.Environment.GetEnvironmentVariable(AlgorithmKey);
            var clockSkew = System.Environment.GetEnvironmentVariable(ClockSkewKey);
            TokenClockSkewSeconds = int.TryParse(clockSkew, out var parsedClockSkew) && parsedClockSkew >= 0
                ? parsedClockSkew
                : -1;
            AccessTokenPublicKeyPem = LoadPublicKeyPem();
            AccessTokenPublicKeyB64 = ExtractPublicKeyBase64(AccessTokenPublicKeyPem);
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

            if (string.IsNullOrWhiteSpace(AccessTokenPublicKeyB64) || string.IsNullOrWhiteSpace(AccessTokenPublicKeyPem) || !IsValidPublicKeyBase64(AccessTokenPublicKeyB64))
            {
                error = "Assets/Resources/FinkForge/access-token-public-key.pem is missing or invalid.";
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

        private string LoadPublicKeyPem()
        {
            var pem = publicKeyLoader != null
                ? publicKeyLoader()
                : Resources.Load<TextAsset>(PublicKeyResourcePath)?.text;
            return string.IsNullOrWhiteSpace(pem) ? string.Empty : pem.Trim();
        }

        private static string ExtractPublicKeyBase64(string pem)
        {
            if (string.IsNullOrWhiteSpace(pem)) return string.Empty;
            return pem
                .Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
                .Replace("-----END PUBLIC KEY-----", string.Empty)
                .Replace("-----BEGIN RSA PUBLIC KEY-----", string.Empty)
                .Replace("-----END RSA PUBLIC KEY-----", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();
        }

        private static bool IsValidPublicKeyBase64(string encodedKey)
        {
            try
            {
                return Convert.FromBase64String(encodedKey).Length > 0;
            }
            catch (FormatException)
            {
                return false;
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
