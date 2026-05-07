using NSubstitute;
using SpaceBattle.Lib;

public class SendCommandTests {
    [Fact]
    public void Execute_ShouldPassCommandToReceiver() {
        var command = Substitute.For<ICommand>();
        var receiver = Substitute.For<ICommandReceiver>();
        var sendCommand = new SendCommand(command, receiver);

        sendCommand.Execute();

        receiver.Received(1).Receive(command);
    }

    [Fact]
    public void Execute_ShouldThrowException_WhenReceiverFails() {
        
        var command = Substitute.For<ICommand>();
        var receiver = Substitute.For<ICommandReceiver>();
        
        receiver.When(r => r.Receive(Arg.Any<ICommand>()))
                .Do(x => { throw new InvalidOperationException(); });

        var sendCommand = new SendCommand(command, receiver);

        Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());
    }
}
