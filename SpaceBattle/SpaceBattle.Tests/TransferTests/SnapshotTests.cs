using System.Text.Json;
using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class SnapshotTests
    {
        private readonly SnapshotValueCodecs _codecs;

        public SnapshotTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            new RegisterIoCDependencySnapshotCodecs().Execute();

            _codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
        }

        private static CommandDescription Describe(string id)
        {
            return new CommandDescription { Id = id, Type = "Commands.Move", GameItemId = 1 };
        }

        [Fact]
        public void Vector_Dimension_IsTheNumberOfCoordinates()
        {
            Assert.Equal(3, new Vector(1, 2, 3).Dimension);
        }

        [Fact]
        public void Journal_KeepsOrderAndRemovesById()
        {
            var journal = new PendingCommandJournal();
            journal.Add(Describe("a"));
            journal.Add(Describe("b"));
            journal.Add(Describe("c"));

            Assert.True(journal.Remove("b"));
            Assert.False(journal.Remove("b"));
            Assert.Equal(new[] { "a", "c" }, journal.Pending.Select(description => description.Id));

            journal.Clear();

            Assert.Empty(journal.Pending);
        }

        [Fact]
        public void JournaledCommand_ExecutesOnceAndLeavesTheJournal()
        {
            var journal = new PendingCommandJournal();
            var inner = Substitute.For<ICommand>();
            journal.Add(Describe("a"));
            var journaled = new JournaledCommand(inner, journal, "a");

            journaled.Execute();
            journaled.Execute();

            inner.Received(1).Execute();
            Assert.Empty(journal.Pending);
        }

        [Fact]
        public void Repository_ListsIdsAndThrowsForMissingItems()
        {
            var repository = new EnumerableGameObjectRepository();
            var item = new Dictionary<string, object>();
            repository.Set(1, item);
            repository.Set(2, new Dictionary<string, object>());

            repository.Remove(2);

            Assert.Equal(new[] { 1 }, repository.Ids);
            Assert.Same(item, repository.Get(1));
            Assert.Throws<KeyNotFoundException>(() => repository.Get(2));
            Assert.Throws<KeyNotFoundException>(() => repository.Remove(2));
        }

        [Theory]
        [MemberData(nameof(SerializableValues))]
        public void Codecs_RoundTripSupportedValues(object value)
        {
            Assert.True(_codecs.TryWrite(value, out var written));

            Assert.Equal(value, _codecs.Read(written));
        }

        public static TheoryData<object> SerializableValues()
        {
            return new TheoryData<object> { 42, "ship", new Vector(1, -2), new Angle(3) };
        }

        [Fact]
        public void Codecs_ValueWithoutCodecOrNull_IsNotWritten()
        {
            Assert.False(_codecs.TryWrite(Substitute.For<ICommand>(), out _));
            Assert.False(_codecs.TryWrite(null, out _));
        }

        [Theory]
        [InlineData("{\"value\":1}")]
        [InlineData("{\"type\":\"System.Int32\"}")]
        [InlineData("{\"type\":\"Unknown\",\"value\":1}")]
        public void Codecs_ReadingMalformedValue_ThrowsInvalidOperationException(string json)
        {
            var written = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

            Assert.Throws<InvalidOperationException>(() => _codecs.Read(written));
        }

        [Fact]
        public void Snapshot_ContainsOnlySerializableValues_AndSurvivesSerialization()
        {
            var repository = new EnumerableGameObjectRepository();
            repository.Set(7, new Dictionary<string, object>
            {
                ["Position"] = new Vector(1, 2),
                ["Engine"] = Substitute.For<ICommand>(),
            });
            var journal = new PendingCommandJournal();
            journal.Add(Describe("a"));
            var game = new TransferableGame(null!, null!, null!, journal, repository, "Standard", 5);

            var json = GameSnapshotSerializer.Serialize(GameSnapshotBuilder.Build(game, _codecs));
            var snapshot = GameSnapshotSerializer.Deserialize(json);

            Assert.Contains("\"game_item_id\":1", json);
            Assert.Equal("Standard", snapshot.Rules);
            Assert.Equal(5, snapshot.CommandLimit);
            Assert.Equal("Commands.Move", Assert.Single(snapshot.Commands).Type);
            var item = GameSnapshotBuilder.ReadItem(snapshot.Items[7], _codecs);
            Assert.Equal(new Vector(1, 2), item["Position"]);
            Assert.False(item.ContainsKey("Engine"));
        }

        [Theory]
        [InlineData("{")]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("{\"rules\":null,\"command_limit\":1}")]
        [InlineData("{\"rules\":\"\",\"command_limit\":1}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":0}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":1,\"items\":null}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":1,\"commands\":null}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":1,\"items\":{\"1\":null}}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":1,\"items\":{\"1\":{\"Fuel\":null}}}")]
        [InlineData("{\"rules\":\"Standard\",\"command_limit\":1,\"commands\":[null]}")]
        public void Deserialize_InvalidSnapshot_ThrowsInvalidOperationException(string json)
        {
            Assert.Throws<InvalidOperationException>(() => GameSnapshotSerializer.Deserialize(json));
        }

        [Fact]
        public void LocalSending_JournalsTheDescriptionAndDeliversTheCommand()
        {
            var game = Substitute.For<ICommandReceiver>();
            var journal = new PendingCommandJournal();
            var command = Substitute.For<ICommand>();
            var description = Describe("a");

            new LocalGameCommandSending(game, journal, _ => command).Send(description);

            Assert.Same(description, Assert.Single(journal.Pending));
            game.Received(1).Receive(command);
        }

        [Fact]
        public void RedirectingSending_SendsToTheCurrentTarget()
        {
            var first = Substitute.For<IGameCommandSending>();
            var second = Substitute.For<IGameCommandSending>();
            var description = Describe("a");
            var redirecting = new RedirectingGameCommandSending(first);

            redirecting.Send(description);
            redirecting.RedirectTo(second);
            redirecting.Send(description);

            first.Received(1).Send(description);
            second.Received(1).Send(description);
        }

        [Fact]
        public void Constructors_WithNullArguments_ThrowArgumentNullException()
        {
            var command = Substitute.For<ICommand>();
            var receiver = Substitute.For<ICommandReceiver>();
            var sending = Substitute.For<IGameCommandSending>();
            var journal = new PendingCommandJournal();
            var game = new TransferableGame(null!, sending, null!, journal, new EnumerableGameObjectRepository(), "Standard", 1);

            Assert.Throws<ArgumentNullException>(() => new JournaledCommand(null!, journal, "a"));
            Assert.Throws<ArgumentNullException>(() => new JournaledCommand(command, null!, "a"));
            Assert.Throws<ArgumentNullException>(() => new JournaledCommand(command, journal, null!));
            Assert.Throws<ArgumentNullException>(() => new LocalGameCommandSending(null!, journal, _ => command));
            Assert.Throws<ArgumentNullException>(() => new LocalGameCommandSending(receiver, null!, _ => command));
            Assert.Throws<ArgumentNullException>(() => new LocalGameCommandSending(receiver, journal, null!));
            Assert.Throws<ArgumentNullException>(() => new RedirectingGameCommandSending(null!));
            Assert.Throws<ArgumentNullException>(() => new RedirectingGameCommandSending(sending).RedirectTo(null!));
            Assert.Throws<ArgumentNullException>(() => new DelegatingGameCommandSending(null!));
            Assert.Throws<ArgumentNullException>(() => new DelegatingCommand(null!));
            Assert.Throws<ArgumentNullException>(() => new TransferGameCommand(null!, null!));
            Assert.Throws<ArgumentNullException>(() => new TransferGameCommand(game, null!));
        }
    }
}
