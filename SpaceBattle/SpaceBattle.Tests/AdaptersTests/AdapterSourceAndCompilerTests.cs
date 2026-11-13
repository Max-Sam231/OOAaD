using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class AdapterSourceAndCompilerTests
    {
        [Fact]
        public void Naming_BuildsKeysFromInterfaceAndProperty()
        {
            var type = typeof(ITestMovingObject);

            Assert.Equal("Adapters.ITestMovingObject", AdapterDependencyNaming.Adapter(type));
            Assert.Equal("SpaceBattle.Test.ITestMovingObject.Position", AdapterDependencyNaming.Getter(type, "Position"));
            Assert.Equal("SpaceBattle.Test.ITestMovingObject.Position.Set", AdapterDependencyNaming.Setter(type, "Position"));
            Assert.Equal("ITestMovingObjectAdapter", AdapterDependencyNaming.AdapterTypeName(type));
            Assert.Equal("SpaceBattle.Generated.ITestMovingObjectAdapterFactory", AdapterDependencyNaming.FactoryTypeName(type));
        }

        [Fact]
        public void Build_ForNonInterface_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new AdapterSourceBuilder().Build(typeof(string)));
        }

        [Fact]
        public void Build_ForInterfaceWithMethod_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new AdapterSourceBuilder().Build(typeof(ITestWithMethod)));
        }

        [Fact]
        public void Build_ForInterfaceInheritingAnotherInterface_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new AdapterSourceBuilder().Build(typeof(ITestInheriting)));
        }

        [Fact]
        public void Build_EmitsSetterKeyOnlyForWritableProperties()
        {
            var source = new AdapterSourceBuilder().Build(typeof(ITestMovingObject));

            Assert.Contains("\"SpaceBattle.Test.ITestMovingObject.Position.Set\"", source);
            Assert.DoesNotContain("Velocity.Set", source);
        }

        [Fact]
        public void Compile_ValidSource_ReturnsFactoryCreatingAdapterOfTheInterface()
        {
            var type = typeof(ITestMovingObject);
            var source = new AdapterSourceBuilder().Build(type);
            var compiler = new AdapterFactoryCompiler();

            compiler.Compile(source, AdapterDependencyNaming.FactoryTypeName(type));
            var factory = compiler.Compile(source, AdapterDependencyNaming.FactoryTypeName(type));

            Assert.IsAssignableFrom<ITestMovingObject>(factory.Create(new Dictionary<string, object>()));
        }

        [Fact]
        public void Compile_InvalidSource_ThrowsInvalidOperationExceptionWithDiagnostics()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new AdapterFactoryCompiler().Compile("class Broken {", "Broken"));

            Assert.Contains("error", exception.Message);
        }

        [Fact]
        public void GameItemPropertySetting_Execute_WritesValueUnderKey()
        {
            var gameItem = new Dictionary<string, object>();

            new GameItemPropertySetting(gameItem, "Fuel", 10).Execute();

            Assert.Equal(10, gameItem["Fuel"]);
        }

        [Fact]
        public void GameItemPropertySetting_WithNullGameItem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GameItemPropertySetting(null!, "Fuel", 10));
        }

        [Fact]
        public void GameItemPropertySetting_WithNullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new GameItemPropertySetting(new Dictionary<string, object>(), null!, 10));
        }
    }
}
