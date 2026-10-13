using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using FinkForge.Config;
using FinkForge.Environment;
using FinkForge.Players;

namespace FinkForge.Transport
{
    internal sealed class FinkHttpTransport : IFinkTransport
    {
        private readonly FinkEnvironment environment;

        public FinkHttpTransport(FinkEnvironment environment)
        {
            this.environment = environment;
        }

        public void Prepare() { }

        public async UniTask LoadBootstrapDataAsync(PlayerService players, ConfigService config)
        {
            FinkResponse response = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                response = await SendRequestAsync(UnityWebRequest.kHttpVerbGET, "/v1/context", null);
                if (response.Success) break;
                if (response.StatusCode != 503 || attempt == 2) break;
                await UniTask.Delay(TimeSpan.FromSeconds(1 + attempt));
            }

            EnsureSuccess(response);
            var dto = JsonUtility.FromJson<GameConfigDto>(response.Body);
            if (dto == null)
                throw new FinkTransportException("Context response was malformed.", false);

            var hasTeamIds = response.Body != null && response.Body.Contains("\"teamId\"");
            config.ApplyRemoteConfig(dto.playerCount, dto.language, dto.musicVolume, dto.SFXVolume,
                dto.inactivityTimerEnabled, dto.inactivityTimeoutSeconds, dto.inactivityQuitCountdownSeconds,
                dto.scoreEnabled);

            var mappedPlayers = new System.Collections.Generic.List<FinkPlayer>();
            if (dto.players != null)
            {
                foreach (var player in dto.players)
                {
                    if (player == null) continue;
                    var teamId = hasTeamIds && player.teamId >= 0 ? player.teamId : (int?)null;
                    mappedPlayers.Add(new FinkPlayer(player.seatId, player.nickname, teamId));
                }
            }
            players.ReplacePlayers(mappedPlayers);
            // TODO: Confirm authoritative player roster source in v1 runtime contract.
        }

        public async UniTask<bool> SendAsync(PendingDelivery delivery)
        {
            var path = GetPath(delivery.Type);
            var response = await SendRequestAsync(
                UnityWebRequest.kHttpVerbPOST,
                path,
                delivery.PayloadJson);
            EnsureSuccess(response);
            return true;
        }

        private string GetPath(FinkDeliveryType type)
        {
            switch (type)
            {
                case FinkDeliveryType.AppStart: return "/v1/app/start";
                case FinkDeliveryType.AppRestart: return "/v1/app/restart";
                case FinkDeliveryType.IndividualScores: return "/v1/game/scores";
                case FinkDeliveryType.TeamScores: return "/v1/game/team-scores";
                case FinkDeliveryType.AppEnd: return "/v1/app/end";
                case FinkDeliveryType.Incident: return "/v1/health/incident";
                case FinkDeliveryType.Heartbeat: return "/v1/health/heartbeat";
                default: throw new FinkTransportException("Unknown Table Runtime delivery type.", false);
            }
        }

        private async UniTask<FinkResponse> SendRequestAsync(string method, string path, string json)
        {
            if (!environment.IsConfigured)
            {
                environment.TryGetConfigurationError(out var configurationError);
                throw new FinkTransportException("Fink Forge launch environment is not configured: " + configurationError, false);
            }

            var url = environment.BaseUrl.TrimEnd('/') + path;
            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 3
            };

            if (!string.IsNullOrEmpty(json))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            ApplyLaunchHeaders(request);

            try
            {
                await request.SendWebRequest().ToUniTask();
            }
            catch (Exception exception)
            {
                throw new FinkTransportException("Table Runtime request failed.", true, exception);
            }

            var status = (int)request.responseCode;
            var body = request.downloadHandler?.text ?? string.Empty;
            var success = request.result == UnityWebRequest.Result.Success && status >= 200 && status < 300 && !HasExplicitFailure(body);
            return new FinkResponse(status, success, body, request.error);
        }

        private void ApplyLaunchHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("X-API-Key", environment.ApiKey);
            request.SetRequestHeader("Authorization", "Bearer " + environment.AccessToken);
            request.SetRequestHeader("X-Fink-Installation-Id", environment.InstallationId);
            request.SetRequestHeader("X-Fink-Device-Id", environment.DeviceId);
            request.SetRequestHeader("X-Fink-Launch-Nonce", environment.LaunchNonce);
        }

        private static bool HasExplicitFailure(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || !body.Contains("\"success\"")) return false;
            var response = JsonUtility.FromJson<SuccessResponse>(body);
            return response != null && !response.success;
        }

        private static void EnsureSuccess(FinkResponse response)
        {
            if (response != null && response.Success) return;
            var transient = response == null || response.StatusCode == 0 || response.StatusCode >= 500 || response.StatusCode == 503;
            throw new FinkTransportException("Table Runtime request returned an unsuccessful response.", transient, response?.StatusCode ?? 0);
        }
    }

    internal sealed class FinkResponse
    {
        public int StatusCode { get; }
        public bool Success { get; }
        public string Body { get; }
        public string Message { get; }
        public FinkResponse(int statusCode, bool success, string body, string message)
        {
            StatusCode = statusCode; Success = success; Body = body; Message = message;
        }
    }

    [Serializable]
    internal sealed class SuccessResponse
    {
        public bool success;
        public string message;
    }

    internal sealed class FinkTransportException : Exception
    {
        public bool IsTransient { get; }
        public int StatusCode { get; }
        public FinkTransportException(string message, bool isTransient, Exception inner = null, int statusCode = 0)
            : base(message, inner)
        {
            IsTransient = isTransient;
            StatusCode = statusCode;
        }
        public FinkTransportException(string message, bool isTransient, int statusCode)
            : this(message, isTransient, null, statusCode) { }
    }
}
