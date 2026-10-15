using UnityEngine;

namespace FinkForge.Application
{
    internal sealed class ApplicationService
    {
        private readonly System.Action onBeforeQuit;
        private bool quitting;
        public ApplicationService(System.Action onBeforeQuit) { this.onBeforeQuit = onBeforeQuit; }

        public void Quit()
        {
            if (quitting) return;
            quitting = true;
            onBeforeQuit?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
