namespace SpaceBattle.Server
{
    public static class Program
    {
        public static Task Main(string[] args) => ServerBootstrap.RunAsync(args, CancellationToken.None);
    }
}
