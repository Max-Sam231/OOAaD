using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class RegisterGameRulesTests : IDisposable
    {
        private readonly object _rootScope;
        private readonly object _baseScope;

        public RegisterGameRulesTests()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            _rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
            _baseScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _baseScope).Execute();

            new RegisterIoCDependencySnapshotCodecs().Execute();
            new RegisterIoCDependencyMacroCommand().Execute();
            new RegisterIoCDependencyGameRules().Execute();
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Empty",
                (Func<object[], object>)(_ => Substitute.For<ICommand>())).Execute();
        }

        public void Dispose()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
        }

        [Fact]
        public void RegisterGameRulesCommand_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RegisterGameRulesCommand(null!));
        }

        [Fact]
        public void Apply_FullRulesConfig_RegistersActionsTemplatesCollisionsVictoryAndEntities()
        {
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", _baseScope);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameScope).Execute();
            new RegisterIoCDependencyGameObjectRepository().Execute();

            var executedOrder = new List<string>();
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.CheckFuel",
                (Func<object[], object>)(_ => new DelegatingCommand(() => executedOrder.Add("CheckFuel")))).Execute();
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.BurnFuel",
                (Func<object[], object>)(_ => new DelegatingCommand(() => executedOrder.Add("BurnFuel")))).Execute();
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Move",
                (Func<object[], object>)(_ => new DelegatingCommand(() => executedOrder.Add("Move")))).Execute();

            var collisionCalledWith = new List<(int A, int B)>();
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Collision.DestroyIfEnemy",
                (Func<object[], object>)(args => new DelegatingCommand(() => collisionCalledWith.Add(((int)args[0], (int)args[1]))))).Execute();

            var reindexNextId = 0;
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Game.State.Reindex",
                (Func<object[], object>)(args => new DelegatingCommand(() => reindexNextId = (int)args[0]))).Execute();

            var json = """
            {
              "version": "2.0.0",
              "victory_target": "Flagship",
              "templates": {
                "Torpedo": {
                  "TickAction": { "type": "System.String", "value": "Macro.ProjectileTick" },
                  "Speed": { "type": "System.Int32", "value": 4 }
                }
              },
              "collisions": {
                "Torpedo.Ship": "Commands.Collision.DestroyIfEnemy"
              },
              "entities": [
                {
                  "id": 10,
                  "properties": {
                    "Position": { "type": "SpaceBattle.Lib.Vector", "value": [10, 20] },
                    "Angle": { "type": "SpaceBattle.Lib.Angle", "value": [2, 8] },
                    "Speed": { "type": "System.Int32", "value": 3 },
                    "OwnerId": { "type": "System.String", "value": "player-1" }
                  }
                },
                {
                  "id": 2,
                  "properties": {
                    "Position": { "type": "SpaceBattle.Lib.Vector", "value": [1, 1] }
                  }
                }
              ],
              "actions": {
                "Move": {
                  "steps": ["Commands.Move", "Commands.BurnFuel", "Commands.CheckFuel"],
                  "order": [
                    "Commands.CheckFuel before Commands.BurnFuel",
                    "Commands.BurnFuel before Commands.Move"
                  ]
                }
              }
            }
            """;

            var config = Ioc.Resolve<GameRulesConfig>("Game.Rules.Parse", json);
            Ioc.Resolve<ICommand>("Game.Rules.Apply", config).Execute();

            Assert.Equal("2.0.0", Ioc.Resolve<string>("Game.Rules.Version"));
            Assert.Equal(json, Ioc.Resolve<string>("Game.Rules.Config"));
            Assert.Equal("Flagship", Ioc.Resolve<string>("Game.Victory.TargetType"));

            var templateDirect = Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Torpedo");
            var templateResolved = Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Resolve", "Torpedo");
            var templateMissing = Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Resolve", "Unknown");
            Assert.Equal("Macro.ProjectileTick", templateDirect["TickAction"]);
            Assert.Equal(4, templateResolved["Speed"]);
            Assert.NotSame(templateDirect, templateResolved);
            Assert.Empty(templateMissing);

            Ioc.Resolve<ICommand>("Game.Collision.Handle.Ship.Torpedo", 10, 2).Execute();
            Assert.Equal(new[] { (10, 2) }, collisionCalledWith);

            var specs = Ioc.Resolve<IEnumerable<string>>("Specs.Move");
            Assert.Equal(new[] { "Commands.CheckFuel", "Commands.BurnFuel", "Commands.Move" }, specs);

            Ioc.Resolve<ICommand>("Macro.Move", new Dictionary<string, object>()).Execute();
            Assert.Equal(new[] { "CheckFuel", "BurnFuel", "Move" }, executedOrder);

            var repo = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
            var ship = repo.Get(10);
            Assert.Equal(new Vector(10, 20), ship["Position"]);
            Assert.Equal(new Angle(2, 8), ship["Angle"]);
            Assert.Equal(3, ship["Speed"]);
            Assert.Equal("player-1", ship["OwnerId"]);
            Assert.Equal(new Vector(1, 1), repo.Get(2)["Position"]);
            Assert.Equal(11, reindexNextId);
        }

        [Fact]
        public void Apply_MinimalRulesWithoutOptionalSectionsOrEntities_UsesBaseDefaults()
        {
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", _baseScope);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameScope).Execute();

            var json = """
            {
              "version": "1.0.0",
              "entities": [],
              "actions": {}
            }
            """;

            var config = Ioc.Resolve<GameRulesConfig>("Game.Rules.Parse", json);
            Ioc.Resolve<ICommand>("Game.Rules.Apply", config).Execute();

            Assert.Equal("1.0.0", Ioc.Resolve<string>("Game.Rules.Version"));
            Assert.Equal("Ship", Ioc.Resolve<string>("Game.Victory.TargetType"));
            Assert.Empty(Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Resolve", "Any"));
            Ioc.Resolve<ICommand>("Game.State.Reindex", 1).Execute();
        }

        [Fact]
        public void MultipleGames_WithDifferentRulesUnderSharedBase_ExecuteIndependently()
        {
            var holder = new ScopeParentHolder(_baseScope);
            var proxyScope = new ProxyScopeFactory().Create(holder);
            var gameScopeA = Ioc.Resolve<object>("IoC.Scope.Create", proxyScope);
            var gameScopeB = Ioc.Resolve<object>("IoC.Scope.Create", proxyScope);

            var logA = new List<string>();
            var logB = new List<string>();

            var rulesA = """
            {
              "version": "v-fuel",
              "entities": [],
              "actions": {
                "Move": {
                  "steps": ["Commands.Move", "Commands.BurnFuel", "Commands.CheckFuel"],
                  "order": [
                    "Commands.CheckFuel before Commands.BurnFuel",
                    "Commands.BurnFuel before Commands.Move"
                  ]
                }
              }
            }
            """;

            var rulesB = """
            {
              "version": "v-classic",
              "entities": [],
              "actions": {
                "Move": {
                  "steps": ["Commands.Move"]
                }
              }
            }
            """;

            new InSeparateScopeCommand(
                gameScopeA,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.CheckFuel",
                        (Func<object[], object>)(_ => new DelegatingCommand(() => logA.Add("CheckFuel")))).Execute();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.BurnFuel",
                        (Func<object[], object>)(_ => new DelegatingCommand(() => logA.Add("BurnFuel")))).Execute();
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.Move",
                        (Func<object[], object>)(_ => new DelegatingCommand(() => logA.Add("Move")))).Execute();
                    var parsed = Ioc.Resolve<GameRulesConfig>("Game.Rules.Parse", rulesA);
                    Ioc.Resolve<ICommand>("Game.Rules.Apply", parsed).Execute();
                })).Execute();

            new InSeparateScopeCommand(
                gameScopeB,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Commands.Move",
                        (Func<object[], object>)(_ => new DelegatingCommand(() => logB.Add("Move")))).Execute();
                    var parsed = Ioc.Resolve<GameRulesConfig>("Game.Rules.Parse", rulesB);
                    Ioc.Resolve<ICommand>("Game.Rules.Apply", parsed).Execute();
                })).Execute();

            var versionA = string.Empty;
            var versionB = string.Empty;

            new InSeparateScopeCommand(
                gameScopeA,
                new DelegatingCommand(() =>
                {
                    versionA = Ioc.Resolve<string>("Game.Rules.Version");
                    Ioc.Resolve<ICommand>("Macro.Move", new Dictionary<string, object>()).Execute();
                })).Execute();

            new InSeparateScopeCommand(
                gameScopeB,
                new DelegatingCommand(() =>
                {
                    versionB = Ioc.Resolve<string>("Game.Rules.Version");
                    Ioc.Resolve<ICommand>("Macro.Move", new Dictionary<string, object>()).Execute();
                })).Execute();

            Assert.Equal("v-fuel", versionA);
            Assert.Equal("v-classic", versionB);
            Assert.Equal(new[] { "CheckFuel", "BurnFuel", "Move" }, logA);
            Assert.Equal(new[] { "Move" }, logB);
        }

        [Fact]
        public void RegisterNamed_RegistersReusableRuleCommandInCurrentScope()
        {
            var json = """
            {
              "version": "named-1",
              "entities": [],
              "actions": {
                "Rotate": { "steps": ["Commands.Rotate"] }
              }
            }
            """;

            Ioc.Resolve<ICommand>("Game.Rules.RegisterNamed", "standard", json).Execute();

            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", _baseScope);
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>("Game.Rules.standard").Execute();
                    Assert.Equal("named-1", Ioc.Resolve<string>("Game.Rules.Version"));
                    Assert.Equal(new[] { "Commands.Rotate" }, Ioc.Resolve<IEnumerable<string>>("Specs.Rotate"));
                })).Execute();
        }

        [Fact]
        public void Templates_ResolveSnapshotCodecsLazily_AcrossHotReload()
        {
            var baseV2 = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            new InSeparateScopeCommand(
                baseV2,
                new DelegatingCommand(() =>
                {
                    var v2Codecs = new SnapshotValueCodecs();
                    v2Codecs.Add(
                        typeof(int),
                        value => System.Text.Json.JsonSerializer.SerializeToElement((int)value),
                        element => element.GetInt32() * 10);
                    Ioc.Resolve<ICommand>(
                        "IoC.Register",
                        "Snapshot.Codecs",
                        (Func<object[], object>)(_ => v2Codecs)).Execute();
                })).Execute();

            var holder = new ScopeParentHolder(_baseScope);
            var proxyScope = new ProxyScopeFactory().Create(holder);
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", proxyScope);

            var json = """
            {
              "version": "1.0.0",
              "templates": {
                "Torpedo": {
                  "Speed": { "type": "System.Int32", "value": 4 }
                }
              },
              "entities": [],
              "actions": {}
            }
            """;

            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    var config = Ioc.Resolve<GameRulesConfig>("Game.Rules.Parse", json);
                    Ioc.Resolve<ICommand>("Game.Rules.Apply", config).Execute();
                    Assert.Equal(4, Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Torpedo")["Speed"]);
                    Assert.Equal(4, Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Resolve", "Torpedo")["Speed"]);
                })).Execute();

            holder.Swap(baseV2);

            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    Assert.Equal(40, Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Torpedo")["Speed"]);
                    Assert.Equal(40, Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Resolve", "Torpedo")["Speed"]);
                })).Execute();
        }
    }
}
