using System;
using Cysharp.Threading.Tasks;
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
            ShutdownAsync().Forget();
        }

        private async UniTaskVoid ShutdownAsync()
        {
            // Give the score/end outbox a bounded window to complete its first request.
            await UniTask.Delay(TimeSpan.FromSeconds(3));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
