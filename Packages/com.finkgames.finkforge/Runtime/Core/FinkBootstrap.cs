using UnityEngine;

namespace FinkForge.Core
{
    internal static class FinkBootstrap
    {
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            if (_started) return;
            _started = true;
            var driver = new GameObject("Fink Forge Runtime")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(driver);
            driver.AddComponent<FinkRuntimeDriver>();
            FinkRuntime.Instance.BeginInitialization();
        }
    }

    internal sealed class FinkRuntimeDriver : MonoBehaviour
    {
        private void Update() => FinkRuntime.Instance.Tick();
    }
}
