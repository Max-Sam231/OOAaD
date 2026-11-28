using NSubstitute;
using App;
using SpaceBattle.Lib;

public class SendCommandTests
{
    [Fact]
    public void Execute_ShouldPassCommandToReceiver()
    {
        var command = Substitute.For<ICommand>();
        var receiver = Substitute.For<ICommandReceiver>();
        var sendCommand = new SendCommand(command, receiver);

        sendCommand.Execute();

        receiver.Received(1).Receive(command);
    }

    [Fact]
    public void Execute_ShouldThrowException_WhenReceiverFails()
    {

        var command = Substitute.For<ICommand>();
        var receiver = Substitute.For<ICommandReceiver>();

        receiver.When(r => r.Receive(Arg.Any<ICommand>()))
                .Do(x => { throw new InvalidOperationException(); });

        var sendCommand = new SendCommand(command, receiver);

        Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());
    }

    [Fact]
    public void Constructor_NullCommand_ThrowsArgumentNullException()
    {
        var receiver = Substitute.For<ICommandReceiver>();

        Assert.Throws<ArgumentNullException>(() => new SendCommand(null!, receiver));
    }

    [Fact]
    public void Constructor_NullReceiver_ThrowsArgumentNullException()
    {
        var command = Substitute.For<ICommand>();

        Assert.Throws<ArgumentNullException>(() => new SendCommand(command, null!));
    }
}
