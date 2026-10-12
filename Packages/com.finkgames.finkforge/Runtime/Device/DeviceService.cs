using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using FinkForge.Transport;

namespace FinkForge.Device
{
    // Internal automatic error capture. This is intentionally not exposed through Fink's public facade.
    internal sealed class DeviceService
    {
        private readonly FinishDeliveryOutbox _outbox;
        private readonly Action _onIncidentDelivered;
        private string _lastAutomaticIncident;
        private int _incidentSequence;

        public DeviceService(FinishDeliveryOutbox outbox, Action onIncidentDelivered)
        {
            this._outbox = outbox;
            this._onIncidentDelivered = onIncidentDelivered;
        }

        internal void ReportAutomaticIncident(string reasonType, string message, string stackTrace)
        {
            var key = reasonType + "|" + message;
            if (key == _lastAutomaticIncident) return;
            _lastAutomaticIncident = key;

            var payload = new IncidentPayload
            {
                reasonType = reasonType,
                message = message,
                details = new IncidentDetails
                {
                    stackTrace = stackTrace,
                    scene = SceneManager.GetActiveScene().name
                }
            };
            var sequence = ++_incidentSequence;
            var delivery = new PendingDelivery(
                Guid.NewGuid().ToString("N"),
                FinkDeliveryType.Incident,
                FinkJson.Serialize(payload),
                delivered =>
                {
                    if (delivered)
                        _onIncidentDelivered?.Invoke();
                },
                "incident-" + sequence);
            _outbox.EnqueueAndTryDeliver(delivery, Time.realtimeSinceStartup);
        }
    }
}
