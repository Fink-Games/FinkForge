namespace FinkForge.Transport
{
    internal sealed class SerialService
    {
        public string Value { get; private set; }

        public void Resolve()
        {
            // TODO: Use the platform/device integration when its contract is available.
            // Do not invent a production serial provider; an empty value is the safe fallback.
            Value = string.Empty;
        }
    }
}
