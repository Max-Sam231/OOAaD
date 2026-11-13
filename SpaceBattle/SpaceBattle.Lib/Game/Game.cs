using System;
using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class Game : ICommand, ICommandReceiver
    {
        private readonly ConcurrentQueue<ICommand> _queue = new ConcurrentQueue<ICommand>();

        public Game()
        {
        }

        public void Receive(ICommand command)
        {
            if (command != null) _queue.Enqueue(command);
        }

        public void Execute()
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
                        Ioc.Resolve<ICommand>("Exception.Handle", command, ex).Execute();
                    }
                }
                else
                {
                    Ioc.Resolve<ICommand>("Game.Loop.Idle").Execute();
                }
            }
        }
    }
}