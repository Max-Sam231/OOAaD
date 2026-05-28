using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class ShootCommandTests
    {
        [Fact]
        public void Execute_ShouldSendProjectileCommandToReceiver()
        {
            var projectileCommand = Substitute.For<ICommand>();
            var receiver = Substitute.For<ICommandReceiver>();
            var shootable = Substitute.For<IShootable>();

            shootable.ProjectileCommand.Returns(projectileCommand);
            shootable.Receiver.Returns(receiver);

            var command = new ShootCommand(shootable);
            command.Execute();

            receiver.Received(1).Receive(projectileCommand);
        }

        [Fact]
        public void Execute_ShouldThrowInvalidOperationException_WhenProjectileCommandIsNull()
        {
            var receiver = Substitute.For<ICommandReceiver>();
            var shootable = Substitute.For<IShootable>();
            shootable.ProjectileCommand.Returns((ICommand)null!);
            shootable.Receiver.Returns(receiver);

            var command = new ShootCommand(shootable);

            Assert.Throws<InvalidOperationException>(() => command.Execute());
        }

        [Fact]
        public void Execute_ShouldThrowInvalidOperationException_WhenReceiverIsNull()
        {
            var projectileCommand = Substitute.For<ICommand>();
            var shootable = Substitute.For<IShootable>();
            shootable.ProjectileCommand.Returns(projectileCommand);
            shootable.Receiver.Returns((ICommandReceiver)null!);

            var command = new ShootCommand(shootable);

            Assert.Throws<InvalidOperationException>(() => command.Execute());
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenShootableIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ShootCommand(null!));
        }

        [Fact]
        public void Execute_ShouldAlsoSendMoveTorpedoCommand_WhenMovementProviderIsAvailable()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            var projectileCommand = Substitute.For<ICommand>();
            var moveTorpedoCommand = Substitute.For<ICommand>();
            var receiver = Substitute.For<ICommandReceiver>();
            var shootable = new ShootableWithTorpedoMovement(projectileCommand, receiver, 42);

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Torpedo.Move", (Func<object[], object>)(args =>
            {
                Assert.Equal(42, (int)args[0]);
                return moveTorpedoCommand;
            })).Execute();

            var command = new ShootCommand(shootable);
            command.Execute();

            receiver.Received(1).Receive(projectileCommand);
            receiver.Received(1).Receive(moveTorpedoCommand);
        }

        private sealed class ShootableWithTorpedoMovement : IShootable, ITorpedoMovementProvider
        {
            public ShootableWithTorpedoMovement(ICommand projectileCommand, ICommandReceiver receiver, int torpedoId)
            {
                ProjectileCommand = projectileCommand;
                Receiver = receiver;
                TorpedoId = torpedoId;
            }

            public ICommand ProjectileCommand { get; }
            public ICommandReceiver Receiver { get; }
            public int TorpedoId { get; }
        }
    }
}
