using System;
using App;

namespace SpaceBattle.Lib
{
    public class UpdateSectorCommand : ICommand
    {
        private readonly ICollidable _obj;

        public UpdateSectorCommand(ICollidable obj)
        {
            _obj = obj ?? throw new ArgumentNullException(nameof(obj));
        }

        public void Execute()
        {
            Ioc.Resolve<ICommand>("Game.Grid.UpdateObjectPosition", _obj).Execute();
        }
    }
}
