using Cysharp.Threading.Tasks;
using FinkForge.Transport;

namespace FinkForge.Core
{
    internal sealed class LightService
    {
        private readonly IFinkTransport transport;

        public LightService(IFinkTransport transport)
        {
            this.transport = transport;
        }

        public void SendEvent(string type, int? seatId, int[] seats, string intensity)
        {
            if (string.IsNullOrWhiteSpace(type)) return;
            SendAsync("POST", "/v1/lights/event", BuildEventJson(type.Trim(), seatId, seats, intensity)).Forget();
        }

        public void SetTurn(int? seatId)
        {
            SendAsync("PATCH", "/v1/lights/state", seatId.HasValue
                ? "{\"turn\":" + seatId.Value + "}"
                : "{\"turn\":null}").Forget();
        }

        public void SetTimer(int durationMs, int? seatId)
        {
            if (durationMs < 1000 || durationMs > 600000) return;
            var timer = "{\"durationMs\":" + durationMs;
            if (seatId.HasValue) timer += ",\"seat\":" + seatId.Value;
            timer += "}";
            SendAsync("PATCH", "/v1/lights/state", "{\"timer\":" + timer + "}").Forget();
        }

        public void ClearTimer()
        {
            SendAsync("PATCH", "/v1/lights/state", "{\"timer\":null}").Forget();
        }

        public void ClearState()
        {
            SendAsync("DELETE", "/v1/lights/state", null).Forget();
        }

        private UniTask SendAsync(string method, string path, string json)
        {
            return transport.SendLightRequestAsync(method, path, json);
        }

        private static string BuildEventJson(string type, int? seatId, int[] seats, string intensity)
        {
            var json = "{\"type\":\"" + Escape(type) + "\"";
            if (seatId.HasValue) json += ",\"seat\":" + seatId.Value;
            if (seats != null && seats.Length > 0)
            {
                json += ",\"seats\":[";
                for (var index = 0; index < seats.Length; index++)
                {
                    if (index > 0) json += ",";
                    json += seats[index];
                }
                json += "]";
            }
            if (!string.IsNullOrWhiteSpace(intensity))
                json += ",\"intensity\":\"" + Escape(intensity) + "\"";
            return json + "}";
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
