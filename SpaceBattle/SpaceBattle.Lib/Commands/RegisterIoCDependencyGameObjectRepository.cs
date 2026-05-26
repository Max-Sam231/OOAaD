using App;
using System;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyGameObjectRepository : ICommand
    {
        public void Execute()
        {
            var repository = new GameObjectRepository();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Repository.ReadOnly", (Func<object[], object>)((args) =>
            {
                return (IReadOnlyGameObjectRepository)repository;
            })).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Game.Repository.Writable", (Func<object[], object>)((args) =>
            {
                return (IWritableGameObjectRepository)repository;
            })).Execute();
        }
    }
}
