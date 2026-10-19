using UnityEngine;
using FinkForge.Config;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FinkForge.Inactivity
{
    internal sealed class InactivityService
    {
        private readonly ConfigService config;
        private readonly System.Func<bool> sessionActive;
        private readonly System.Func<bool> paused;
        private float lastInputTime;
        private bool started;
        private bool warningActive;
        private bool timeoutRaised;
        private float warningStartedAt;
        private bool hasPointerPosition;
        private Vector2 lastPointerPosition;
        private const float PointerMovementThreshold = 0.25f;
        private const float ScrollThreshold = 0.01f;
        public event System.Action TimedOut;
        public event System.Action<float> WarningStarted;
        public event System.Action<float> WarningUpdated;
        public event System.Action WarningCancelled;
        public InactivityService(ConfigService config, System.Func<bool> sessionActive, System.Func<bool> paused)
        {
            this.config = config;
            this.sessionActive = sessionActive;
            this.paused = paused;
        }

        public void Start() { started = true; lastInputTime = Time.realtimeSinceStartup; }

        public bool IsWarningActive => warningActive;

        public float WarningRemainingSeconds
        {
            get
            {
                if (!warningActive) return 0f;
                return Mathf.Max(0f, config.InactivityQuitCountdownSeconds -
                    (Time.realtimeSinceStartup - warningStartedAt));
            }
        }

        // Called by the runtime host when an input-frame integration is available.
        public void Tick()
        {
            if (!started || !config.InactivityTimerEnabled) return;
            if (!sessionActive() || paused())
            {
                CancelWarning();
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (HasUserActivity())
            {
                lastInputTime = now;
                timeoutRaised = false;
                CancelWarning();
                return;
            }

            if (timeoutRaised) return;

            if (!warningActive && now - lastInputTime >= config.InactivityTimeoutSeconds)
            {
                warningActive = true;
                warningStartedAt = now;
                WarningStarted?.Invoke(config.InactivityQuitCountdownSeconds);
            }

            if (!warningActive) return;

            var remaining = WarningRemainingSeconds;
            WarningUpdated?.Invoke(remaining);
            if (remaining <= 0f)
            {
                warningActive = false;
                timeoutRaised = true;
                TimedOut?.Invoke();
            }
        }

        private void CancelWarning()
        {
            if (!warningActive) return;
            warningActive = false;
            WarningCancelled?.Invoke();
        }

        private bool HasUserActivity()
        {
            var activity = false;
#if ENABLE_INPUT_SYSTEM
            activity |= HasNewInputSystemActivity();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            activity |= HasLegacyInputActivity();
#endif
            return activity;
        }

#if ENABLE_INPUT_SYSTEM
        private bool HasNewInputSystemActivity()
        {
            var activity = false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) activity = true;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                activity |= mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                activity |= mouse.delta.ReadValue().sqrMagnitude > PointerMovementThreshold * PointerMovementThreshold;
                activity |= mouse.scroll.ReadValue().sqrMagnitude > ScrollThreshold * ScrollThreshold;
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                activity |= pointer.press != null && pointer.press.wasPressedThisFrame;
                var position = pointer.position.ReadValue();
                if (hasPointerPosition)
                    activity |= (position - lastPointerPosition).sqrMagnitude > PointerMovementThreshold * PointerMovementThreshold;
                lastPointerPosition = position;
                hasPointerPosition = true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.press.isPressed || touch.press.wasPressedThisFrame)
                    {
                        activity = true;
                        break;
                    }
                }
            }

            return activity;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static bool HasLegacyInputActivity()
        {
            if (Input.anyKey || Input.touchCount > 0) return true;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return true;
            if (Input.mouseScrollDelta.sqrMagnitude > ScrollThreshold * ScrollThreshold) return true;
            return Mathf.Abs(Input.GetAxisRaw("Mouse X")) > PointerMovementThreshold ||
                   Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > PointerMovementThreshold;
        }
#endif
    }
}
