using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FinkForge.Transport
{
    internal sealed class FinishDeliveryOutbox
    {
        private const int MaxAttempts = 4;
        private static readonly float[] RetryDelaysSeconds = { 1f, 2f, 4f };
        private readonly IFinkTransport transport;
        private readonly Dictionary<string, PendingDelivery> pending = new Dictionary<string, PendingDelivery>();
        private readonly HashSet<string> inFlight = new HashSet<string>();

        public int PendingCount => pending.Count;

        public FinishDeliveryOutbox(IFinkTransport transport)
        {
            this.transport = transport;
        }

        public void EnqueueAndTryDeliver(PendingDelivery delivery, float now)
        {
            if (delivery == null || pending.ContainsKey(delivery.DeliveryId)) return;
            delivery.NextAttemptAt = now;
            pending.Add(delivery.DeliveryId, delivery);
            TryDeliverAsync(delivery, now).Forget();
        }

        public void ProcessPending(float now)
        {
            var deliveries = new List<PendingDelivery>(pending.Values);
            foreach (var delivery in deliveries)
            {
                if (!delivery.Exhausted && !inFlight.Contains(delivery.DeliveryId) && now >= delivery.NextAttemptAt)
                    TryDeliverAsync(delivery, now).Forget();
            }
        }

        internal PendingDelivery GetPending(string deliveryId)
        {
            pending.TryGetValue(deliveryId, out var delivery);
            return delivery;
        }

        private async UniTaskVoid TryDeliverAsync(PendingDelivery delivery, float now)
        {
            if (delivery.Exhausted || !inFlight.Add(delivery.DeliveryId)) return;
            delivery.AttemptCount++;
            try
            {
                var delivered = await transport.SendAsync(delivery);
                if (!delivered)
                    throw new FinkTransportException("Table Runtime delivery was not accepted.", false);
                pending.Remove(delivery.DeliveryId);
                delivery.Completion?.Invoke(true);
            }
            catch (FinkTransportException exception)
            {
                HandleFailure(delivery, exception.IsTransient, now, exception.Message);
            }
            catch (Exception exception)
            {
                HandleFailure(delivery, true, now, exception.Message);
            }
            finally
            {
                inFlight.Remove(delivery.DeliveryId);
            }
        }

        private void HandleFailure(PendingDelivery delivery, bool transient, float now, string message)
        {
            if (!transient || delivery.AttemptCount >= MaxAttempts)
            {
                delivery.Exhausted = true;
                Debug.LogError($"[Fink Forge] Delivery failed permanently: {delivery.DeliveryId}");
                delivery.Completion?.Invoke(false);
                return;
            }

            var delayIndex = delivery.AttemptCount - 1;
            delivery.NextAttemptAt = now + RetryDelaysSeconds[Mathf.Min(delayIndex, RetryDelaysSeconds.Length - 1)];
            Debug.LogWarning($"[Fink Forge] Delivery retry scheduled: {delivery.DeliveryId}");
        }
    }
}
