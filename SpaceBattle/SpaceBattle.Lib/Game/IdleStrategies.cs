using System;
using System.Threading;
using App;

namespace SpaceBattle.Lib
{
    public class ServerIdleCommand : ICommand
    {
        public void Execute()
        {
            Thread.Sleep(1);
        }
    }
}
