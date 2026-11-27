using App;
using App.Scopes;
using Microsoft.CodeAnalysis;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class ScopeParentHolderAndProxyTests : IDisposable
    {
        private readonly object _rootScope;

        public ScopeParentHolderAndProxyTests()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            _rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
        }

        public void Dispose()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
        }

        [Fact]
        public void Hierarchy_GameScope_HasExactDepthFourAndDoesNotPolluteRootScope()
        {
            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInitializer = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Game.Tag", (Func<object[], object>)(_ => "base-v1")).Execute());

            object gameScope = null!;
            object proxyScope = null!;
            ScopeParentHolder holder = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInitializer).Execute();

                    Assert.Same(_rootScope, Ioc.Resolve<object>("Server.Scope.Root"));
                    Assert.Same(baseInitializer, Ioc.Resolve<ICommand>("Scopes.Base.Initializer"));
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    proxyScope = Ioc.Resolve<object>("Server.Scope.Proxy");
                    gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                })).Execute();

            var chain = new List<object> { gameScope };
            var cursor = gameScope;

            while (!ReferenceEquals(cursor, _rootScope))
            {
                object parent = null!;
                new InSeparateScopeCommand(
                    cursor,
                    new DelegatingCommand(() => parent = Ioc.Resolve<object>("IoC.Scope.Parent"))).Execute();
                chain.Add(parent);
                cursor = parent;
            }

            Assert.Equal(4, chain.Count);
            Assert.Same(gameScope, chain[0]);
            Assert.Same(proxyScope, chain[1]);
            Assert.Same(holder.Current, chain[2]);
            Assert.Same(_rootScope, chain[3]);

            string resolvedTag = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() => resolvedTag = Ioc.Resolve<string>("Game.Tag"))).Execute();
            Assert.Equal("base-v1", resolvedTag);

            Assert.ThrowsAny<Exception>(() => Ioc.Resolve<object>("Server.Scope.Root"));
            Assert.ThrowsAny<Exception>(() => Ioc.Resolve<string>("Game.Tag"));
        }

        [Fact]
        public void TwoIndependentServerScopes_RegisterHierarchyInSameProcess_WithoutCollision()
        {
            var serverScopeA = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var serverScopeB = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);

            var initA = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Game.ServerName", (Func<object[], object>)(_ => "ServerA")).Execute());
            var initB = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Game.ServerName", (Func<object[], object>)(_ => "ServerB")).Execute());

            object gameA = null!;
            object gameB = null!;

            new InSeparateScopeCommand(
                serverScopeA,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, initA).Execute();
                    gameA = Ioc.Resolve<object>("Scopes.Game.Create");
                })).Execute();

            new InSeparateScopeCommand(
                serverScopeB,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, initB).Execute();
                    gameB = Ioc.Resolve<object>("Scopes.Game.Create");
                })).Execute();

            string nameA = null!;
            string nameB = null!;

            new InSeparateScopeCommand(
                gameA,
                new DelegatingCommand(() => nameA = Ioc.Resolve<string>("Game.ServerName"))).Execute();
            new InSeparateScopeCommand(
                gameB,
                new DelegatingCommand(() => nameB = Ioc.Resolve<string>("Game.ServerName"))).Execute();

            Assert.Equal("ServerA", nameA);
            Assert.Equal("ServerB", nameB);
        }

        [Fact]
        public void ProxyScopeFactory_CreateAndSwap_SwitchesBaseScopeAtomicallyWithoutCatch22()
        {
            var baseV1 = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseV2 = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);

            new InSeparateScopeCommand(
                baseV1,
                new DelegatingCommand(() =>
                    Ioc.Resolve<ICommand>("IoC.Register", "Game.Version", (Func<object[], object>)(_ => "v1")).Execute())).Execute();
            new InSeparateScopeCommand(
                baseV2,
                new DelegatingCommand(() =>
                    Ioc.Resolve<ICommand>("IoC.Register", "Game.Version", (Func<object[], object>)(_ => "v2")).Execute())).Execute();

            var holder = new ScopeParentHolder(baseV1);
            var proxyScope = new ProxyScopeFactory().Create(holder);
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", proxyScope);

            string before = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() => before = Ioc.Resolve<string>("Game.Version"))).Execute();

            holder.Swap(baseV2);
            Assert.Same(baseV2, holder.Current);

            string after = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() => after = Ioc.Resolve<string>("Game.Version"))).Execute();

            Assert.Equal("v1", before);
            Assert.Equal("v2", after);
        }

        [Fact]
        public void GameCreateScoped_InitializesGameScopeAndExecutesInnerCommandInsideIt()
        {
            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInitializer = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Base.Value", (Func<object[], object>)(_ => 10)).Execute());

            var observedSum = 0;
            ICommand scopedCommand = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInitializer).Execute();

                    var gameInitializer = new DelegatingCommand(() =>
                        Ioc.Resolve<ICommand>("IoC.Register", "Game.Offset", (Func<object[], object>)(_ => 5)).Execute());
                    var innerCommand = new DelegatingCommand(() =>
                        observedSum = Ioc.Resolve<int>("Base.Value") + Ioc.Resolve<int>("Game.Offset"));

                    scopedCommand = Ioc.Resolve<ICommand>("Game.Create.Scoped", innerCommand, gameInitializer);
                })).Execute();

            scopedCommand.Execute();

            Assert.Equal(15, observedSum);
            Assert.Same(_rootScope, Ioc.Resolve<object>("IoC.Scope.Current"));
        }

        [Fact]
        public void ScopeComponents_WithNullArguments_ThrowArgumentNullException()
        {
            var noop = new DelegatingCommand(() => { });
            var catalog = ScopedAssemblyCatalogFactory.CreateDefault();
            var builder = new GameBaseScopeBuilder();
            var holder = new ScopeParentHolder(_rootScope);

            Assert.Throws<ArgumentNullException>(() => new ScopeParentHolder(null!));
            Assert.Throws<ArgumentNullException>(() => holder.Swap(null!));
            Assert.Throws<ArgumentNullException>(() => new ProxyScopeFactory().Create(null!));
            Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyScopeHierarchy(null!, noop));
            Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyScopeHierarchy(_rootScope, null!));
            Assert.Throws<ArgumentNullException>(() => builder.Build(null!, noop, catalog, Array.Empty<ICommand>()));
            Assert.Throws<ArgumentNullException>(() => builder.Build(_rootScope, null!, catalog, Array.Empty<ICommand>()));
            Assert.Throws<ArgumentNullException>(() => builder.Build(_rootScope, noop, null!, Array.Empty<ICommand>()));
            Assert.Throws<ArgumentNullException>(() => builder.Build(_rootScope, noop, catalog, null!));
            Assert.Throws<ArgumentNullException>(() => new ScopedAssemblyCatalog(null!, Array.Empty<MetadataReference>()));
            Assert.Throws<ArgumentNullException>(() => new ScopedAssemblyCatalog(Array.Empty<System.Reflection.Assembly>(), null!));
            Assert.Throws<ArgumentNullException>(() => ScopedAssemblyCatalogFactory.FromAssemblies(null!));
            Assert.Throws<ArgumentNullException>(() => ScopedAssemblyCatalogFactory.CreateWithPlugins(null!, Array.Empty<byte[]>()));
            Assert.Throws<ArgumentNullException>(() => ScopedAssemblyCatalogFactory.CreateWithPlugins(Array.Empty<System.Reflection.Assembly>(), null!));
            Assert.Throws<ArgumentNullException>(() => new PluginBatch(null!, Array.Empty<ICommand>()));
            Assert.Throws<ArgumentNullException>(() => new PluginBatch(catalog, null!));
        }
    }
}
