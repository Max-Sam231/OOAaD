using SpaceBattle.Lib;
using App.Scopes;
using NSubstitute;
using Xunit;
using App;
using ICommand = SpaceBattle.Lib.ICommand;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencySendCommandTests
    {
        public RegisterIoCDependencySendCommandTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void SendCommand_Should_Send_A_Command_To_The_Command_Receiver()
        {
            var mockCommand = Substitute.For<ICommand>();
            var mockReceiver = Substitute.For<ICommandReceiver>();

            Ioc.Resolve<App.ICommand>("IoC.Register", "Adapters.ICommand", (object[] args) => (ICommand)args[0]).Execute();
            Ioc.Resolve<App.ICommand>("IoC.Register", "Adapters.ICommandReceiver", (object[] args) => (ICommandReceiver)args[1]).Execute();

            new RegisterIoCDependencySendCommand().Execute();

            var sendCommand = Ioc.Resolve<ICommand>("Commands.Send", mockCommand, mockReceiver);
            sendCommand.Execute();

            mockReceiver.Received(1).Receive(mockCommand);
        }
    }
}
