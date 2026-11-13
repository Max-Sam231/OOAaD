using App;

namespace SpaceBattle.Lib
{
    public class TransferGameCommand : ICommand
    {
        private readonly TransferableGame _game;
        private readonly ServerThread _target;

        public TransferGameCommand(TransferableGame game, ServerThread target)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void Execute()
        {
            if (_game.Successor != null)
            {
                Ioc.Resolve<ICommand>("Game.Transfer", _game.Successor, _target).Execute();

                return;
            }

            var codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
            var json = GameSnapshotSerializer.Serialize(GameSnapshotBuilder.Build(_game, codecs));
            var successor = Ioc.Resolve<TransferableGame>("Game.Restore", json, _target);

            _game.Journal.Clear();
            _game.Successor = successor;
            _game.Location.RedirectTo(successor.Sending);
        }
    }
}
