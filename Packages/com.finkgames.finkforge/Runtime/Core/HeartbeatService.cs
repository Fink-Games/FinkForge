using System;
using Cysharp.Threading.Tasks;
using FinkForge.Transport;

namespace FinkForge.Core
{
    internal sealed class HeartbeatService
    {
        private const float HeartbeatIntervalSeconds = 10f;
        private readonly IFinkTransport transport;
        private bool running;
        private bool requestInFlight;
        private float nextHeartbeatAt;

        public HeartbeatService(IFinkTransport transport)
        {
            this.transport = transport;
        }

        public void Start(float now)
        {
            if (running) return;
            running = true;
            nextHeartbeatAt = now;
            Tick(now);
        }

        public void Stop()
        {
            running = false;
        }

        public void Tick(float now)
        {
            if (!running || requestInFlight || now < nextHeartbeatAt) return;

            nextHeartbeatAt = now + HeartbeatIntervalSeconds;
            SendHeartbeatAsync().Forget();
        }

        private async UniTaskVoid SendHeartbeatAsync()
        {
            if (requestInFlight) return;
            requestInFlight = true;

            try
            {
                // The heartbeat contract is a body-less API-key authenticated POST.
                var delivery = new PendingDelivery(
                    Guid.NewGuid().ToString("N"),
                    FinkDeliveryType.Heartbeat,
                    null);
                await transport.SendAsync(delivery);
            }
            catch
            {
                // Heartbeat is telemetry. A failed heartbeat must not stop gameplay;
                // the next interval retries it.
            }
            finally
            {
                requestInFlight = false;
            }
        }
    }
}
