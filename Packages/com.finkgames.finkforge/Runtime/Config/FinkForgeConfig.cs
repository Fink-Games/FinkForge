using UnityEngine;

namespace FinkForge.Config
{
    [CreateAssetMenu(fileName = "FinkForgeConfig", menuName = "Fink Forge/Config")]
    public sealed class FinkForgeConfig : ScriptableObject
    {
        [SerializeField] private string gameName;
        [Header("Access Token Validation")]
        [SerializeField] private string accessTokenIssuer = "fink-backend";
        [SerializeField] private string accessTokenAudience = "fink-game-runtime";
        [SerializeField] private string accessTokenPurpose = "game_startup";
        [SerializeField] private string accessTokenAlgorithm = "RS256";
        [Min(0)] [SerializeField] private int tokenClockSkewSeconds = 30;

        public string GameName => gameName;
        public string AccessTokenIssuer => accessTokenIssuer;
        public string AccessTokenAudience => accessTokenAudience;
        public string AccessTokenPurpose => accessTokenPurpose;
        public string AccessTokenAlgorithm => accessTokenAlgorithm;
        public int TokenClockSkewSeconds => tokenClockSkewSeconds;
    }
}
