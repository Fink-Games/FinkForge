using UnityEngine;

namespace FinkForge.Config
{
    [CreateAssetMenu(fileName = "FinkForgeConfig", menuName = "Fink Forge/Config")]
    public sealed class FinkForgeConfig : ScriptableObject
    {
        [SerializeField] private string gameName;

        public string GameName => gameName;
    }
}
