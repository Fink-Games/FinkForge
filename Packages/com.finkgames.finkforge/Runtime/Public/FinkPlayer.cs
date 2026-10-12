namespace FinkForge
{
    public sealed class FinkPlayer
    {
        public string Nickname { get; }
        public int SeatId { get; }
        public int? TeamId { get; }

        public FinkPlayer(int seatId, string nickname, int? teamId = null)
        {
            Nickname = nickname ?? string.Empty;
            SeatId = seatId;
            TeamId = teamId;
        }
    }
}
