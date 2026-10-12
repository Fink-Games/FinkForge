namespace FinkForge.Environment
{
    internal sealed class FinkEnvironment
    {
        private const string ApiPortKey = "FINK_API_PORT";
        private const string ApiKeyKey = "FINK_API_KEY";

        public string BaseUrl { get; private set; }
        internal string ApiKey { get; private set; }
        internal bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(ApiKey);

        public void Resolve()
        {
            var port = System.Environment.GetEnvironmentVariable(ApiPortKey);
            ApiKey = System.Environment.GetEnvironmentVariable(ApiKeyKey);
            BaseUrl = BuildBaseUrl(port);
        }

        private static string BuildBaseUrl(string port)
        {
            if (!int.TryParse(port, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
                parsedPort = 3786;

            return $"http://localhost:{parsedPort}";
        }
    }
}
