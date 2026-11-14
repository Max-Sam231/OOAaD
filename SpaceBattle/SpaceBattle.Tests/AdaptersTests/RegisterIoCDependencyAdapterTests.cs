using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class RegisterIoCDependencyAdapterTests
    {
        public RegisterIoCDependencyAdapterTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            new RegisterIoCDependencyAdapterFactory().Execute();
        }

        [Fact]
        public void Factory_ForSameInterface_IsCompiledOnce()
        {
            var first = Ioc.Resolve<IAdapterFactory>("Adapters.Factory", typeof(ITestMovingObject));
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameScope).Execute();
            var second = Ioc.Resolve<IAdapterFactory>("Adapters.Factory", typeof(ITestMovingObject));

            Assert.IsAssignableFrom<ITestMovingObject>(first.Create(new Dictionary<string, object>()));
            Assert.Same(first, second);
        }

        [Fact]
        public void Factory_ForNonInterface_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Ioc.Resolve<IAdapterFactory>("Adapters.Factory", typeof(string)));
        }

        [Fact]
        public void Constructor_WithNullInterfaceType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyAdapter(null!));
        }

        [Fact]
        public void Adapter_StandardProperty_ReadsAndWritesGameItem()
        {
            new RegisterIoCDependencyAdapter(typeof(ITestMovingObject)).Execute();
            var gameItem = new Dictionary<string, object> { ["Position"] = new Vector(1, 2) };

            var adapter = Ioc.Resolve<ITestMovingObject>("Adapters.ITestMovingObject", gameItem);
            var before = adapter.Position;
            adapter.Position = new Vector(5, 6);

            Assert.Equal(new Vector(1, 2), before);
            Assert.Equal(new Vector(5, 6), gameItem["Position"]);
        }

        [Fact]
        public void Adapter_PropertyWithDependencyAttribute_UsesDeclaredStrategy()
        {
            new RegisterIoCDependencyAdapter(typeof(ITestMovingObject)).Execute();
            var gameItem = new Dictionary<string, object> { ["Speed"] = 7 };

            var adapter = Ioc.Resolve<ITestMovingObject>("Adapters.ITestMovingObject", gameItem);

            Assert.Equal(new Vector(7, 0), adapter.Velocity);
        }

        [Fact]
        public void Adapter_StandardPropertyMissingInGameItem_ThrowsKeyNotFoundException()
        {
            new RegisterIoCDependencyAdapter(typeof(ITestMovingObject)).Execute();

            var adapter = Ioc.Resolve<ITestMovingObject>("Adapters.ITestMovingObject", new Dictionary<string, object>());

            Assert.Throws<KeyNotFoundException>(() => adapter.Position);
        }

        [Fact]
        public void Adapter_WriteOnlyProperty_WritesGameItem()
        {
            new RegisterIoCDependencyAdapter(typeof(ITestWriteOnly)).Execute();
            var gameItem = new Dictionary<string, object>();

            Ioc.Resolve<ITestWriteOnly>("Adapters.ITestWriteOnly", gameItem).Value = 3;

            Assert.Equal(3, gameItem["Value"]);
        }

        [Fact]
        public void Adapter_GenericPropertyType_IsGeneratedAndRead()
        {
            new RegisterIoCDependencyAdapter(typeof(ITestGenericProperty)).Execute();
            var items = new Dictionary<string, object>();
            var gameItem = new Dictionary<string, object> { ["Items"] = items };

            var adapter = Ioc.Resolve<ITestGenericProperty>("Adapters.ITestGenericProperty", gameItem);

            Assert.Same(items, adapter.Items);
        }

        [Fact]
        public void Execute_WithTwoDependencyAttributesForOneProperty_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => new RegisterIoCDependencyAdapter(typeof(ITestDuplicatedDependency)).Execute());
        }

        [Fact]
        public void Execute_WithDependencyAttributeNamingMissingMethod_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => new RegisterIoCDependencyAdapter(typeof(ITestMissingStrategy)).Execute());
        }

        [Fact]
        public void GeneratedAdapter_WorksWithExistingMoveCommandRegistration()
        {
            new RegisterIoCDependencyAdapter(typeof(IMovingObject)).Execute();
            new RegisterIoCDependencyMoveCommand().Execute();
            var gameItem = new Dictionary<string, object>
            {
                ["Position"] = new Vector(1, 2),
                ["Velocity"] = new Vector(3, 4),
            };

            Ioc.Resolve<ICommand>("Commands.Move", gameItem).Execute();

            Assert.Equal(new Vector(4, 6), gameItem["Position"]);
        }
    }
}
