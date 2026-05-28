using App;
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
    }
}
