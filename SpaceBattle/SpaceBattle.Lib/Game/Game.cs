using System;
using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class Game : ICommand, ICommandReceiver
    {
        private readonly ConcurrentQueue<ICommand> _queue = new ConcurrentQueue<ICommand>();
        private readonly object _gameScope;

        public Game(object gameScope)
        {
            _gameScope = gameScope ?? throw new ArgumentNullException(nameof(gameScope));
        }

        public void Receive(ICommand command)
        {
            if (command != null) _queue.Enqueue(command);
        }

        public void Execute()
        {
            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _gameScope).Execute();

            try
            {
                while (Ioc.Resolve<bool>("Game.Loop.CanContinue"))
                {
                    if (_queue.TryDequeue(out var command))
                    {
                        try
                        {
                            command.Execute();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Game Loop Error]: {ex.Message}");
                        }
                    }
                    else
                    {
                        Ioc.Resolve<ICommand>("Game.Loop.Idle").Execute();
                    }
                }
            }
            finally
            {
                Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope).Execute();
            }
        }
    }
}