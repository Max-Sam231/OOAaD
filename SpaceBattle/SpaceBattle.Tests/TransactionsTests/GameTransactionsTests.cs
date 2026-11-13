using System.Collections;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class GameTransactionsTests
    {
        private readonly GameTransactions _transactions = new GameTransactions();
        private readonly Dictionary<string, object> _stored = new Dictionary<string, object> { ["Fuel"] = 10 };
        private readonly TransactionalGameItem _item;

        public GameTransactionsTests()
        {
            _item = new TransactionalGameItem(_stored, _transactions);
        }

        [Fact]
        public void CommitOrRollback_WithoutOpenTransaction_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(_transactions.Commit);
            Assert.Throws<InvalidOperationException>(_transactions.Rollback);
        }

        [Fact]
        public void Write_WithoutOpenTransaction_GoesStraightToTheStoredItem()
        {
            _item["Fuel"] = 9;

            Assert.False(_transactions.IsActive);
            Assert.Equal(9, _stored["Fuel"]);
        }

        [Fact]
        public void Write_InsideTransaction_IsVisibleThroughTheItemButNotStoredUntilCommit()
        {
            _transactions.Begin();
            _item["Fuel"] = 9;

            Assert.Equal(9, _item["Fuel"]);
            Assert.Equal(10, _stored["Fuel"]);

            _transactions.Commit();

            Assert.Equal(9, _stored["Fuel"]);
            Assert.Equal(9, _item["Fuel"]);
        }

        [Fact]
        public void Rollback_DiscardsWrites()
        {
            _transactions.Begin();
            _item["Fuel"] = 9;
            _item["Position"] = new Vector(1, 2);

            _transactions.Rollback();

            Assert.Equal(10, _item["Fuel"]);
            Assert.False(_stored.ContainsKey("Position"));
        }

        [Fact]
        public void InnerCommit_BecomesPartOfTheOuterTransaction()
        {
            _transactions.Begin();
            _transactions.Begin();
            _item["Fuel"] = 8;
            _transactions.Commit();

            Assert.Equal(8, _item["Fuel"]);
            Assert.Equal(10, _stored["Fuel"]);

            _transactions.Rollback();

            Assert.Equal(10, _item["Fuel"]);
        }

        [Fact]
        public void InnerRollback_KeepsOuterWrites()
        {
            _transactions.Begin();
            _item["Fuel"] = 9;
            _transactions.Begin();
            _item["Fuel"] = 8;

            Assert.Equal(8, _item["Fuel"]);
            Assert.True(_item.TryGetValue("Fuel", out var innermost));
            Assert.Equal(8, innermost);
            Assert.Equal(new object[] { 8 }, _item.Values);

            _transactions.Rollback();

            Assert.Equal(9, _item["Fuel"]);

            _transactions.Commit();

            Assert.Equal(9, _stored["Fuel"]);
        }

        [Fact]
        public void Writes_ToDifferentItems_AreKeptApart()
        {
            var otherStored = new Dictionary<string, object> { ["Fuel"] = 1 };
            var other = new TransactionalGameItem(otherStored, _transactions);
            _transactions.Begin();

            _item["Fuel"] = 9;

            Assert.Equal(1, other["Fuel"]);
            Assert.Equal(new[] { "Fuel" }, other.Keys);
            Assert.Equal(new object[] { 1 }, other.Values);
        }

        [Fact]
        public void DictionaryMembers_ReflectStoredValuesAndOpenTransactionWrites()
        {
            _transactions.Begin();
            _item.Add("Position", new Vector(1, 2));
            _item.Add(new KeyValuePair<string, object>("Name", "ship"));
            _item["Fuel"] = 9;
            var copy = new KeyValuePair<string, object>[3];

            _item.CopyTo(copy, 0);
            var containsName = _item.Contains(new KeyValuePair<string, object>("Name", "ship"));

            Assert.Equal(3, _item.Count);
            Assert.False(_item.IsReadOnly);
            Assert.Equal(new[] { "Fuel", "Name", "Position" }, _item.Keys.Order());
            Assert.Contains(9, _item.Values);
            Assert.True(_item.ContainsKey("Name"));
            Assert.True(_item.TryGetValue("Fuel", out var fuel));
            Assert.Equal(9, fuel);
            Assert.False(_item.TryGetValue("Missing", out _));
            Assert.Contains(new KeyValuePair<string, object>("Fuel", 9), _item);
            Assert.True(containsName);
            Assert.Equal(3, copy.Count(pair => pair.Key != null));
            Assert.Equal(3, ((IEnumerable)_item).Cast<object>().Count());
            Assert.Single(_stored);
        }

        [Fact]
        public void Add_ExistingKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _item.Add("Fuel", 1));
        }

        [Fact]
        public void RemoveAndClear_WithoutTransaction_ChangeTheStoredItem()
        {
            _stored["Name"] = "ship";
            _stored["Shield"] = 1;

            Assert.True(_item.Remove("Name"));
            Assert.True(_item.Remove(new KeyValuePair<string, object>("Shield", 1)));
            Assert.Equal(new[] { "Fuel" }, _stored.Keys);

            _item.Clear();

            Assert.Empty(_stored);
        }

        [Fact]
        public void RemoveAndClear_InsideTransaction_ThrowNotSupportedException()
        {
            _transactions.Begin();

            Assert.Throws<NotSupportedException>(() => _item.Remove("Fuel"));
            Assert.Throws<NotSupportedException>(() => _item.Remove(new KeyValuePair<string, object>("Fuel", 10)));
            Assert.Throws<NotSupportedException>(_item.Clear);
        }

        [Fact]
        public void Constructor_WithNullArguments_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TransactionalGameItem(null!, _transactions));
            Assert.Throws<ArgumentNullException>(() => new TransactionalGameItem(_stored, null!));
        }
    }
}
