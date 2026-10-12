namespace FinkForge.Environment
{
    internal sealed class LaunchSecurityService
    {
        private readonly FinkForge.Config.FinkForgeSettingsService settings;

        public LaunchSecurityService(FinkForge.Config.FinkForgeSettingsService settings)
        {
            this.settings = settings;
        }

        // TODO: Bind RS256/JWT validation to the authoritative trusted-key onboarding contract.
        // No public key is present in this repository, so launch tokens are not treated as valid here.
        public bool HasTrustedKeys => false;
        public bool IsConfigured => false;
    }
}
