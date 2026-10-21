using Cysharp.Threading.Tasks;
using FinkForge.Config;
using FinkForge.Players;

namespace FinkForge.Transport
{
    internal interface IFinkTransport
    {
        void Prepare();
        UniTask LoadBootstrapDataAsync(PlayerService players, ConfigService config);
        UniTask<FinkPauseState> GetPauseStateAsync();
        UniTask SendLightRequestAsync(string method, string path, string json);
        UniTask<bool> SendAsync(PendingDelivery delivery);
    }

    internal enum FinkDeliveryType
    {
        AppStart,
        AppRestart,
        IndividualScores,
        TeamScores,
        AppEnd,
        Incident,
        Heartbeat
    }

    internal sealed class PendingDelivery
    {
        public string DeliveryId { get; }
        public string SessionId { get; }
        public FinkDeliveryType Type { get; }
        public string PayloadJson { get; }
        public int AttemptCount { get; set; }
        public float NextAttemptAt { get; set; }
        public bool Exhausted { get; set; }
        internal System.Action<bool> Completion { get; }

        public PendingDelivery(string sessionId, FinkDeliveryType type, string payloadJson,
            System.Action<bool> completion = null, string stableSuffix = null)
        {
            SessionId = sessionId;
            Type = type;
            DeliveryId = sessionId + ":" + (stableSuffix ?? GetStableSuffix(type));
            PayloadJson = payloadJson;
            Completion = completion;
        }

        private static string GetStableSuffix(FinkDeliveryType type)
        {
            switch (type)
            {
                case FinkDeliveryType.AppStart: return "start";
                case FinkDeliveryType.AppRestart: return "restart";
                case FinkDeliveryType.IndividualScores: return "scores";
                case FinkDeliveryType.TeamScores: return "team-scores";
                case FinkDeliveryType.AppEnd: return "end";
                case FinkDeliveryType.Incident: return "incident";
                case FinkDeliveryType.Heartbeat: return "heartbeat";
                default: return type.ToString();
            }
        }
    }

    internal static class FinkJson
    {
        public static string Serialize(object value) => UnityEngine.JsonUtility.ToJson(value);
    }

    [System.Serializable]
    internal sealed class GameConfigDto
    {
        public int playerCount;
        public string language;
        public float musicVolume = 1f;
        public float SFXVolume = 1f;
        public bool inactivityTimerEnabled;
        public float inactivityTimeoutSeconds;
        public float inactivityQuitCountdownSeconds;
        public bool scoreEnabled;
        // This field is a backend/configuration flag and is intentionally not used to infer team play.
        public bool teamModeEnabled;
        public RuntimePlayerDto[] players;
    }

    internal sealed class FinkPauseState
    {
        public bool Success;
        public bool Paused;
        public string Reason;
        public string Until;
    }

    [System.Serializable]
    internal sealed class RuntimePlayerDto
    {
        public int seatId;
        public string nickname;
        public int teamId;
    }

    [System.Serializable]
    internal sealed class GameLifecyclePayload
    {
        public string status;
        public int playerCount;
        public bool usesTeams;
    }

    [System.Serializable]
    internal sealed class PlayerScorePayload
    {
        public string gameName;
        public int playerCount;
        public PlayerScoreEntry[] players;
    }

    [System.Serializable]
    internal sealed class PlayerScoreEntry
    {
        public int seatId;
        public string playerName;
        public int score;
    }

    [System.Serializable]
    internal sealed class TeamScorePayload
    {
        public string gameName;
        public int playerCount;
        public TeamScoreEntry[] teams;
    }

    [System.Serializable]
    internal sealed class TeamScoreEntry
    {
        public int teamId;
        public string teamName;
        public int score;
    }

    [System.Serializable]
    internal sealed class IncidentPayload
    {
        public string reasonType;
        public string message;
        public IncidentDetails details;
    }

    [System.Serializable]
    internal sealed class IncidentDetails
    {
        public string stackTrace;
        public string scene;
    }

}
