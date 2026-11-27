using App;

namespace SpaceBattle.Lib
{
    public sealed class BroadcastGameStateCommand : ICommand
    {
        private readonly string _gameId;

        public BroadcastGameStateCommand(string gameId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

            _gameId = gameId;
        }

        public void Execute()
        {
            var connections = Ioc.Resolve<IGameConnectionRegistry>("Server.Connections");
            if (connections.GetConnectionCount(_gameId) == 0)
            {
                return;
            }

            var step = Ioc.Resolve<long>("Game.Step.Current");
            var payload = Ioc.Resolve<ReadOnlyMemory<byte>>("Game.State.Serialize", _gameId, step);
            connections.Publish(_gameId, payload);
        }
    }
}
