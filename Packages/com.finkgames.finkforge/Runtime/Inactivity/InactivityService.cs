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
        private float lastInputTime;
        private bool started;
        private bool hasPointerPosition;
        private Vector2 lastPointerPosition;
        private const float PointerMovementThreshold = 0.25f;
        private const float ScrollThreshold = 0.01f;
        public event System.Action TimedOut;
        public InactivityService(ConfigService config) { this.config = config; }

        public void Start() { started = true; lastInputTime = Time.realtimeSinceStartup; }

        // Called by the runtime host when an input-frame integration is available.
        public void Tick()
        {
            if (!started || !config.InactivityTimerEnabled) return;
            if (HasUserActivity()) lastInputTime = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - lastInputTime >= config.InactivityTimeoutSeconds)
            {
                lastInputTime = Time.realtimeSinceStartup;
                TimedOut?.Invoke();
            }
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
