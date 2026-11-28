using System.Text.Json;
using App;
using App.Scopes;
using NSubstitute;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class GameRulesTextBuilderTests : IDisposable
    {
        private readonly GameRulesTextBuilder _textBuilder = new GameRulesTextBuilder();
        private readonly GameRulesParserBuilder _parser = new GameRulesParserBuilder(new TopologicalOrderBuilder());

        public GameRulesTextBuilderTests()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            var rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
            var baseScope = Ioc.Resolve<object>("IoC.Scope.Create", rootScope);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", baseScope).Execute();

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
        public void Translate_BoardExampleWithBlankLinesAndDottedSteps_ProducesValidJsonAndParsesIntoConfig()
        {
            var rulesText = """
            В игре есть Корабль, Ракета, Метеорит

            Корабль может ДвигатьсяПоПрямой, Вращаться, Стрелять
            Ракета может ДвигатьсяПоПрямой
            ДвигатьсяПоПрямой включает Move, BurnFuel
            Стрелять включает Macro.SpawnTorpedo, Commands.PlaySound
            BurnFuel до Move
            """;

            var json = _textBuilder.Translate(rulesText);
            var config = _parser.Parse(json);

            Assert.Equal("1.0.0", config.Version);
            Assert.Empty(config.Entities);
            Assert.Equal(3, config.Templates.Count);
            Assert.Equal(
                "ДвигатьсяПоПрямой,Вращаться,Стрелять",
                config.Templates["Корабль"]["AllowedActions"]["value"].GetString());
            Assert.Equal(
                "System.String",
                config.Templates["Корабль"]["AllowedActions"]["type"].GetString());
            Assert.Equal(
                "ДвигатьсяПоПрямой",
                config.Templates["Ракета"]["AllowedActions"]["value"].GetString());
            Assert.Empty(config.Templates["Метеорит"]);

            var moveAction = Assert.Single(config.Actions, a => a.Name == "ДвигатьсяПоПрямой");
            Assert.Equal(new[] { "Commands.BurnFuel", "Commands.Move" }, moveAction.OrderedSteps);

            var rotateAction = Assert.Single(config.Actions, a => a.Name == "Вращаться");
            Assert.Equal(new[] { "Commands.Вращаться" }, rotateAction.OrderedSteps);

            var shootAction = Assert.Single(config.Actions, a => a.Name == "Стрелять");
            Assert.Equal(new[] { "Macro.SpawnTorpedo", "Commands.PlaySound" }, shootAction.OrderedSteps);

            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("entities").ValueKind);
            Assert.Equal(0, doc.RootElement.GetProperty("entities").GetArrayLength());
        }

        [Fact]
        public void Translate_ExplicitVersionAndIncludesBeforeCan_PreservesStepsAndVersion()
        {
            var rulesText = """
            В игре есть Корабль
            ДвигатьсяПоПрямой включает Move, BurnFuel
            Корабль может ДвигатьсяПоПрямой, Вращаться
            BurnFuel до Move
            """;

            var json = _textBuilder.Translate(rulesText, "2.4.0");
            var config = _parser.Parse(json);

            Assert.Equal("2.4.0", config.Version);
            var moveAction = Assert.Single(config.Actions, a => a.Name == "ДвигатьсяПоПрямой");
            Assert.Equal(new[] { "Commands.BurnFuel", "Commands.Move" }, moveAction.OrderedSteps);
            var rotateAction = Assert.Single(config.Actions, a => a.Name == "Вращаться");
            Assert.Equal(new[] { "Commands.Вращаться" }, rotateAction.OrderedSteps);
        }

        [Theory]
        [InlineData("В игре есть Корабль\nКорабль может Двигаться\nДвигаться включает Move, BurnFuel\nFoo до Bar")]
        [InlineData("В игре есть Корабль\nКорабль может Двигаться\nДвигаться включает Move, BurnFuel\nMove до NonExistent")]
        public void Translate_OrderForMissingPair_ThrowsArgumentException(string rulesText)
        {
            Assert.Throws<ArgumentException>(() => _textBuilder.Translate(rulesText));
        }

        [Fact]
        public void Translate_UnknownEntityInCan_ThrowsArgumentException()
        {
            var rulesText = "Корабль может ДвигатьсяПоПрямой";

            Assert.Throws<ArgumentException>(() => _textBuilder.Translate(rulesText));
        }

        [Theory]
        [InlineData("В игре есть Корабль, ")]
        [InlineData("В игре есть , Корабль")]
        [InlineData("В игре есть Корабль, , Ракета")]
        [InlineData("В игре есть Корабль\nКорабль может ДвигатьсяПоПрямой, ")]
        [InlineData("В игре есть Корабль\nДвигатьсяПоПрямой включает Move, ")]
        public void Translate_EmptyListElement_ThrowsArgumentException(string rulesText)
        {
            Assert.Throws<ArgumentException>(() => _textBuilder.Translate(rulesText));
        }

        [Theory]
        [InlineData("В игре есть")]
        [InlineData("Неизвестная строка")]
        [InlineData("Корабль может")]
        [InlineData("А может Б может В")]
        [InlineData("А включает Б включает В")]
        [InlineData("А до Б до В")]
        public void Translate_UnrecognizedLine_ThrowsArgumentException(string rulesText)
        {
            Assert.Throws<ArgumentException>(() => _textBuilder.Translate(rulesText));
        }

        [Theory]
        [InlineData(null, "1.0.0")]
        [InlineData("", "1.0.0")]
        [InlineData("   ", "1.0.0")]
        [InlineData("В игре есть Корабль", null)]
        [InlineData("В игре есть Корабль", "")]
        [InlineData("В игре есть Корабль", "   ")]
        public void Translate_NullOrWhitespaceInput_ThrowsArgumentException(string? rulesText, string? version)
        {
            Assert.ThrowsAny<ArgumentException>(() => _textBuilder.Translate(rulesText!, version!));
        }

        [Fact]
        public void IoC_TextToJsonAndParseText_ResolveWithDefaultAndExplicitVersion()
        {
            var rulesText = """
            В игре есть Корабль, Ракета
            Корабль может ДвигатьсяПоПрямой, Вращаться, Стрелять
            ДвигатьсяПоПрямой включает Move, BurnFuel
            BurnFuel до Move
            """;

            var defaultJson = Ioc.Resolve<string>("Game.Rules.TextToJson", rulesText);
            var versionedJson = Ioc.Resolve<string>("Game.Rules.TextToJson", rulesText, "2.5.0");
            var defaultConfig = Ioc.Resolve<GameRulesConfig>("Game.Rules.ParseText", rulesText);
            var versionedConfig = Ioc.Resolve<GameRulesConfig>("Game.Rules.ParseText", rulesText, "3.0.0");

            Assert.Equal("1.0.0", _parser.Parse(defaultJson).Version);
            Assert.Equal("2.5.0", _parser.Parse(versionedJson).Version);
            Assert.Equal("1.0.0", defaultConfig.Version);
            Assert.Equal("3.0.0", versionedConfig.Version);

            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", Ioc.Resolve<object>("IoC.Scope.Current"));
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameScope).Execute();
            Ioc.Resolve<ICommand>("Game.Rules.Apply", versionedConfig).Execute();

            Assert.Equal("3.0.0", Ioc.Resolve<string>("Game.Rules.Version"));
            var shipTemplate = Ioc.Resolve<IDictionary<string, object>>("Game.Entity.Template.Корабль");
            Assert.Equal("ДвигатьсяПоПрямой,Вращаться,Стрелять", shipTemplate["AllowedActions"]);
            Assert.Equal(
                new[] { "Commands.BurnFuel", "Commands.Move" },
                Ioc.Resolve<IEnumerable<string>>("Specs.ДвигатьсяПоПрямой"));
        }

        [Fact]
        public void Translate_DuplicateIncludesForSameAction_ThrowsArgumentException()
        {
            var rulesText = """
            В игре есть Корабль
            Двигаться включает Move
            Двигаться включает BurnFuel
            """;

            Assert.Throws<ArgumentException>(() => _textBuilder.Translate(rulesText));
        }

        [Fact]
        public void Translate_MultipleCanLinesAndDuplicateActionsForSameEntity_MergesWithoutDuplicates()
        {
            var rulesText = """
            В игре есть Корабль
            Корабль может ДвигатьсяПоПрямой, Вращаться, ДвигатьсяПоПрямой
            Корабль может Вращаться, Стрелять
            """;

            var json = _textBuilder.Translate(rulesText);
            var config = _parser.Parse(json);

            Assert.Equal(
                "ДвигатьсяПоПрямой,Вращаться,Стрелять",
                config.Templates["Корабль"]["AllowedActions"]["value"].GetString());
            Assert.Equal(3, config.Actions.Count);
        }

        [Fact]
        public void Translate_OrderConstraintBeforeIncludes_SucceedsAndOrdersSteps()
        {
            var rulesText = """
            В игре есть Корабль
            BurnFuel до Move
            Корабль может ДвигатьсяПоПрямой
            ДвигатьсяПоПрямой включает Move, BurnFuel
            """;

            var json = _textBuilder.Translate(rulesText);
            var config = _parser.Parse(json);

            var moveAction = Assert.Single(config.Actions, a => a.Name == "ДвигатьсяПоПрямой");
            Assert.Equal(new[] { "Commands.BurnFuel", "Commands.Move" }, moveAction.OrderedSteps);
        }
    }
}
