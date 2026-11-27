namespace SpaceBattle.Server
{
    public sealed class ServerOptions
    {
        public int WorkerThreadCount { get; set; } = 2;
        public int OutboundChannelCapacity { get; set; } = 64;
        public int MaxWebSocketMessageBytes { get; set; } = 65536;
        public TimeSpan GracefulStopTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
