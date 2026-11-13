using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class InTransactionCommandTests
    {
        private readonly Dictionary<string, object> _stored = new Dictionary<string, object>
        {
            ["Fuel"] = 10,
            ["Position"] = new Vector(1, 2),
            ["Velocity"] = new Vector(3, 4),
        };

        private readonly IDictionary<string, object> _item;

        public InTransactionCommandTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            new RegisterIoCDependencyTransactions().Execute();

            _item = Ioc.Resolve<IDictionary<string, object>>("Game.Item.Transactional", _stored);
        }

        private static ICommand InTransaction(Action action)
        {
            return Ioc.Resolve<ICommand>("Commands.InTransaction", new TestActionCommand(action));
        }

        private static void Fail()
        {
            throw new InvalidOperationException("step failed");
        }

        [Fact]
        public void Execute_WhenAllStepsSucceed_AppliesEveryWrite()
        {
            InTransaction(() =>
            {
                _item["Position"] = new Vector(4, 6);
                _item["Fuel"] = (int)_item["Fuel"] - 1;
            }).Execute();

            Assert.Equal(new Vector(4, 6), _stored["Position"]);
            Assert.Equal(9, _stored["Fuel"]);
            Assert.False(Ioc.Resolve<GameTransactions>("Game.Transactions").IsActive);
        }

        [Fact]
        public void Execute_WhenALaterStepFails_AppliesNothingAndRethrows()
        {
            var transaction = InTransaction(() =>
            {
                _item["Fuel"] = (int)_item["Fuel"] - 1;
                Fail();
            });

            var exception = Assert.Throws<InvalidOperationException>(transaction.Execute);

            Assert.Equal("step failed", exception.Message);
            Assert.Equal(10, _stored["Fuel"]);
            Assert.False(Ioc.Resolve<GameTransactions>("Game.Transactions").IsActive);
        }

        [Fact]
        public void InnerFailureCaughtByOuterCommand_UndoesOnlyTheInnerWrites()
        {
            var inner = InTransaction(() =>
            {
                _item["B"] = 2;
                Fail();
            });

            InTransaction(() =>
            {
                _item["A"] = 1;
                Assert.Throws<InvalidOperationException>(inner.Execute);
                _item["C"] = 3;
            }).Execute();

            Assert.Equal(1, _stored["A"]);
            Assert.Equal(3, _stored["C"]);
            Assert.False(_stored.ContainsKey("B"));
        }

        [Fact]
        public void OuterFailure_UndoesWritesOfAnInnerTransactionThatCommitted()
        {
            var inner = InTransaction(() => _item["B"] = 2);
            var outer = InTransaction(() =>
            {
                inner.Execute();
                Fail();
            });

            Assert.Throws<InvalidOperationException>(outer.Execute);

            Assert.False(_stored.ContainsKey("B"));
        }

        [Fact]
        public void MacroCommandWithGeneratedAdapter_InTransaction_LeavesPositionUnchangedWhenALaterCommandFails()
        {
            new RegisterIoCDependencyAdapterFactory().Execute();
            new RegisterIoCDependencyAdapter(typeof(IMovingObject)).Execute();
            new RegisterIoCDependencyMoveCommand().Execute();
            var macro = new MacroCommand(new[]
            {
                Ioc.Resolve<ICommand>("Commands.Move", _item),
                new TestActionCommand(Fail),
            });

            Assert.Throws<InvalidOperationException>(Ioc.Resolve<ICommand>("Commands.InTransaction", macro).Execute);
            Assert.Equal(new Vector(1, 2), _stored["Position"]);

            Assert.Throws<InvalidOperationException>(macro.Execute);
            Assert.Equal(new Vector(4, 6), _stored["Position"]);
        }

        [Fact]
        public void Constructor_WithNullArguments_ThrowsArgumentNullException()
        {
            var transactions = new GameTransactions();

            Assert.Throws<ArgumentNullException>(() => new InTransactionCommand(null!, transactions));
            Assert.Throws<ArgumentNullException>(() => new InTransactionCommand(Substitute.For<ICommand>(), null!));
        }
    }
}
