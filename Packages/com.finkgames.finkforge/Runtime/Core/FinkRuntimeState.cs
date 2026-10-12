using System;

namespace FinkForge.Core
{
    internal enum FinkInitializationState { None, Initializing, Ready, Failed }
    internal enum FinkSessionState { None, Active, Finishing, Finished }

    internal sealed class FinkSession
    {
        public string Id { get; }
        public DateTime StartedAtUtc { get; }
        public FinkSessionState State { get; set; }
        public bool ScoreDeliveryRequested { get; set; }
        public bool EndRequested { get; set; }

        public FinkSession()
        {
            Id = Guid.NewGuid().ToString("N");
            StartedAtUtc = DateTime.UtcNow;
            State = FinkSessionState.Active;
        }
    }

    internal sealed class FinkRuntimeState
    {
        public FinkInitializationState Initialization { get; set; }
        public FinkSession CurrentSession { get; set; }
        public bool PendingStart { get; set; }
    }
}
