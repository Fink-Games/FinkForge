using Cysharp.Threading.Tasks;
using FinkForge.Transport;

namespace FinkForge.Core
{
    internal sealed class PauseService
    {
        private const float PollIntervalSeconds = 1f;
        private readonly IFinkTransport transport;
        private readonly System.Action<bool, FinkPauseState> stateChanged;
        private bool running;
        private bool requestInFlight;
        private float nextPollAt;
        private bool paused;

        public PauseService(IFinkTransport transport, System.Action<bool, FinkPauseState> stateChanged)
        {
            this.transport = transport;
            this.stateChanged = stateChanged;
        }

        public bool IsPaused => paused;

        public void Start(float now)
        {
            running = true;
            nextPollAt = now;
            Poll(now);
        }

        public void Stop()
        {
            running = false;
            requestInFlight = false;
            SetPaused(false, null);
        }

        public void Tick(float now)
        {
            if (!running || requestInFlight || now < nextPollAt) return;
            Poll(now);
        }

        private void Poll(float now)
        {
            nextPollAt = now + PollIntervalSeconds;
            PollAsync().Forget();
        }

        private async UniTaskVoid PollAsync()
        {
            if (requestInFlight) return;
            requestInFlight = true;
            try
            {
                var state = await transport.GetPauseStateAsync();
                if (running && state != null && state.Success)
                    SetPaused(state.Paused, state);
            }
            catch
            {
                // Pause polling is advisory; keep the current state on a failed poll.
            }
            finally
            {
                requestInFlight = false;
            }
        }

        private void SetPaused(bool value, FinkPauseState state)
        {
            if (paused == value) return;
            paused = value;
            stateChanged?.Invoke(value, state);
        }
    }
}
