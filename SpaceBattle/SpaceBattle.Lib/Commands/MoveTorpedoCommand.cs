using App;

namespace SpaceBattle.Lib
{
    public class MoveTorpedoCommand : ICommand
    {
        private readonly int _torpedoId;
        private readonly IReadOnlyGameObjectRepository _repository;

        public MoveTorpedoCommand(int torpedoId, IReadOnlyGameObjectRepository repository)
        {
            _torpedoId = torpedoId;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void Execute()
        {
            var torpedo = _repository.Get(_torpedoId);
            var moveCommand = Ioc.Resolve<ICommand>("Commands.Move", torpedo);
            moveCommand.Execute();
        }
    }
}
