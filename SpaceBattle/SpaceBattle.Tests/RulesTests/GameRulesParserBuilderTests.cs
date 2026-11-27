using System.Text.Json;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class GameRulesParserBuilderTests
    {
        private readonly GameRulesParserBuilder _parser = new GameRulesParserBuilder(new TopologicalOrderBuilder());

        [Fact]
        public void Parse_ValidFullRulesJson_ReturnsClonedConfigWithNormalizedCollisionsAndOrderedSteps()
        {
            var json = """
            {
              "version": "1.0.0",
              "victory_target": "Cruiser",
              "templates": {
                "Torpedo": {
                  "TickAction": { "type": "System.String", "value": "Macro.ProjectileTick" },
                  "Speed": { "type": "System.Int32", "value": 5 }
                }
              },
              "collisions": {
                "Torpedo.Ship": "Commands.Collision.DestroyIfEnemy",
                "Asteroid.Ship": "Commands.Collision.DestroyBoth"
              },
              "entities": [
                {
                  "id": 1,
                  "properties": {
                    "Position": { "type": "SpaceBattle.Lib.Vector", "value": [10, 20] },
                    "Speed": { "type": "System.Int32", "value": 1 },
                    "Angle": { "type": "SpaceBattle.Lib.Angle", "value": [0, 8] },
                    "OwnerId": { "type": "System.String", "value": "player-1" }
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
                },
                "Rotate": {
                  "steps": ["Commands.Rotate"]
                },
                "Combo": {
                  "steps": ["Macro.Move", "Macro.External"]
                }
              }
            }
            """;

            var config = _parser.Parse(json);

            Assert.Equal("1.0.0", config.Version);
            Assert.Equal(json, config.RawJson);
            Assert.Equal("Cruiser", config.VictoryTarget);
            Assert.Single(config.Templates);
            Assert.Equal("System.String", config.Templates["Torpedo"]["TickAction"]["type"].GetString());
            Assert.Equal(5, config.Templates["Torpedo"]["Speed"]["value"].GetInt32());
            Assert.Equal("Commands.Collision.DestroyIfEnemy", config.Collisions["Ship.Torpedo"]);
            Assert.Equal("Commands.Collision.DestroyBoth", config.Collisions["Asteroid.Ship"]);
            Assert.Single(config.Entities);
            Assert.Equal(1, config.Entities[0].Id);
            Assert.Equal("player-1", config.Entities[0].Properties["OwnerId"]["value"].GetString());
            Assert.Equal(3, config.Actions.Count);
            Assert.Equal(
                new[] { "Commands.CheckFuel", "Commands.BurnFuel", "Commands.Move" },
                config.Actions[0].OrderedSteps);
            Assert.Equal(new[] { "Commands.Rotate" }, config.Actions[1].OrderedSteps);
        }

        [Fact]
        public void Parse_MinimalValidRulesJsonWithEmptyEntitiesAndActions_Succeeds()
        {
            var json = """
            {
              "version": "0.1.0",
              "entities": [],
              "actions": {}
            }
            """;

            var config = _parser.Parse(json);

            Assert.Equal("0.1.0", config.Version);
            Assert.Null(config.VictoryTarget);
            Assert.Empty(config.Templates);
            Assert.Empty(config.Collisions);
            Assert.Empty(config.Entities);
            Assert.Empty(config.Actions);
        }

        [Fact]
        public void Parse_NullOrWhitespace_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
            Assert.Throws<ArgumentException>(() => _parser.Parse(""));
            Assert.Throws<ArgumentException>(() => _parser.Parse("   "));
        }

        [Fact]
        public void Parse_MalformedJson_ThrowsJsonException()
        {
            Assert.ThrowsAny<JsonException>(() => _parser.Parse("{bad"));
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("42")]
        [InlineData("\"text\"")]
        public void Parse_NonObjectRoot_ThrowsArgumentException(string json)
        {
            Assert.Throws<ArgumentException>(() => _parser.Parse(json));
        }

        [Theory]
        [InlineData("{\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":123,\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"   \",\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"victory_target\":123,\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"victory_target\":\"  \",\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"templates\":[],\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"templates\":{\"\":{}},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"templates\":{\"T\":{},\"T\":{}},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"templates\":{\"T\":123},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"templates\":{\"T\":{\"P\":123}},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":[],\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\"Ship\":\"Commands.X\"},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\".Ship\":\"Commands.X\"},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\"Ship.\":\"Commands.X\"},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\"Ship.Torpedo\":123},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\"Ship.Torpedo\":\"  \"},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"collisions\":{\"Ship.Torpedo\":\"C1\",\"Torpedo.Ship\":\"C2\"},\"entities\":[],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":{},\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[]}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":[]}")]
        public void Parse_InvalidRootOrOptionalFields_ThrowsArgumentException(string json)
        {
            Assert.Throws<ArgumentException>(() => _parser.Parse(json));
        }

        [Theory]
        [InlineData("{\"version\":\"1\",\"entities\":[123],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"properties\":{}}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":\"1\",\"properties\":{}}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":1.5,\"properties\":{}}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":1,\"properties\":{}},{\"id\":1,\"properties\":{}}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":1}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":1,\"properties\":[]}],\"actions\":{}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[{\"id\":1,\"properties\":{\"X\":42}}],\"actions\":{}}")]
        public void Parse_InvalidEntities_ThrowsArgumentException(string json)
        {
            Assert.Throws<ArgumentException>(() => _parser.Parse(json));
        }

        [Theory]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"A\"]},\"Move\":{\"steps\":[\"B\"]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"   \":{\"steps\":[\"A\"]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":123}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":{}}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[123]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"  \"]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"A\",\"A\"]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"A\"],\"order\":{}}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"A\"],\"order\":[123]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"Move\":{\"steps\":[\"A\"],\"order\":[\"  \"]}}}")]
        public void Parse_InvalidActions_ThrowsArgumentException(string json)
        {
            Assert.Throws<ArgumentException>(() => _parser.Parse(json));
        }

        [Theory]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"A\":{\"steps\":[\"Macro.B\"]},\"B\":{\"steps\":[\"Macro.A\"]}}}")]
        [InlineData("{\"version\":\"1\",\"entities\":[],\"actions\":{\"A\":{\"steps\":[\"Macro.A\"]}}}")]
        public void Parse_CrossActionMacroCycle_ThrowsInvalidOperationException(string json)
        {
            Assert.Throws<InvalidOperationException>(() => _parser.Parse(json));
        }

        [Fact]
        public void Constructors_GuardClausesAndDefaults_WorkAsSpecified()
        {
            Assert.Throws<ArgumentNullException>(() => new GameRuleEntity(1, null!));
            Assert.Throws<ArgumentNullException>(() => new GameRuleAction(null!, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new GameRuleAction("  ", Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>(() => new GameRuleAction("Move", null!));
            Assert.Throws<ArgumentNullException>(
                () => new GameRulesConfig(null!, "{}", Array.Empty<GameRuleEntity>(), Array.Empty<GameRuleAction>()));
            Assert.Throws<ArgumentException>(
                () => new GameRulesConfig(" ", "{}", Array.Empty<GameRuleEntity>(), Array.Empty<GameRuleAction>()));
            Assert.Throws<ArgumentNullException>(
                () => new GameRulesConfig("1", null!, Array.Empty<GameRuleEntity>(), Array.Empty<GameRuleAction>()));
            Assert.Throws<ArgumentException>(
                () => new GameRulesConfig("1", " ", Array.Empty<GameRuleEntity>(), Array.Empty<GameRuleAction>()));
            Assert.Throws<ArgumentNullException>(
                () => new GameRulesConfig("1", "{}", null!, Array.Empty<GameRuleAction>()));
            Assert.Throws<ArgumentNullException>(
                () => new GameRulesConfig("1", "{}", Array.Empty<GameRuleEntity>(), null!));
            Assert.Throws<ArgumentNullException>(() => new GameRulesParserBuilder(null!));

            var config = new GameRulesConfig("1", "{}", Array.Empty<GameRuleEntity>(), Array.Empty<GameRuleAction>());
            Assert.Empty(config.Templates);
            Assert.Empty(config.Collisions);
            Assert.Null(config.VictoryTarget);
        }
    }
}
