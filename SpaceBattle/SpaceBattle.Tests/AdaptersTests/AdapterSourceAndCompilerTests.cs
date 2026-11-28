using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class AdapterSourceAndCompilerTests
    {
        public AdapterSourceAndCompilerTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            new RegisterIoCDependencyAdapterTemplates().Execute();
        }

        [Fact]
        public void Naming_BuildsKeysFromInterfaceAndProperty()
        {
            var type = typeof(ITestMovingObject);

            Assert.Equal("Adapters.ITestMovingObject", AdapterDependencyNaming.Adapter(type));
            Assert.Equal("SpaceBattle.Test.ITestMovingObject.Position", AdapterDependencyNaming.Getter(type, "Position"));
            Assert.Equal("SpaceBattle.Test.ITestMovingObject.Position.Set", AdapterDependencyNaming.Setter(type, "Position"));
            Assert.Equal("ITestMovingObjectAdapter", AdapterDependencyNaming.AdapterTypeName(type));
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
            var compiler = new GeneratedCodeCompiler();

            compiler.Compile<IAdapterFactory>(source);
            var factory = compiler.Compile<IAdapterFactory>(source);

            Assert.IsAssignableFrom<ITestMovingObject>(factory.Create(new Dictionary<string, object>()));
        }

        [Fact]
        public void Compile_InvalidSource_ThrowsInvalidOperationExceptionWithDiagnostics()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new GeneratedCodeCompiler().Compile<IAdapterFactory>("class Broken {"));

            Assert.Contains("error", exception.Message);
        }

        [Theory]
        [InlineData("public class NotAFactory { }")]
        [InlineData("public abstract class AbstractFactory : SpaceBattle.Lib.IAdapterFactory { public abstract object Create(System.Collections.Generic.IDictionary<string, object> gameItem); }")]
        [InlineData("public class First : SpaceBattle.Lib.IAdapterFactory { public object Create(System.Collections.Generic.IDictionary<string, object> gameItem) { return null; } } public class Second : First { }")]
        public void Compile_SourceWithoutExactlyOneConcreteFactory_ThrowsInvalidOperationException(string source)
        {
            Assert.Throws<InvalidOperationException>(() => new GeneratedCodeCompiler().Compile<IAdapterFactory>(source));
        }

        [Fact]
        public void Templates_AreRegisteredFromTheTextFilesNextToTheAssembly()
        {
            Assert.Contains("{{AdapterName}}", Ioc.Resolve<string>("Adapters.Template.Class"));
            Assert.Contains("{{Accessors}}", Ioc.Resolve<string>("Adapters.Template.Property"));
            Assert.Contains("{{GetterKey}}", Ioc.Resolve<string>("Adapters.Template.Getter"));
            Assert.Contains("{{SetterKey}}", Ioc.Resolve<string>("Adapters.Template.Setter"));
            Assert.Contains("{{CommandType}}", Ioc.Resolve<string>("Adapters.Template.CommandFactory"));
        }

        [Fact]
        public void Templates_FromDirectoryMissingAFile_ThrowAndRegisterNothing()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
            var partial = Path.Combine(AppContext.BaseDirectory, "PartialTemplates");
            Directory.CreateDirectory(partial);
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Adapters", "Templates", "AdapterClass.txt"),
                Path.Combine(partial, "AdapterClass.txt"),
                true);

            Assert.ThrowsAny<IOException>(() => new RegisterIoCDependencyAdapterTemplates(partial).Execute());
            Assert.ThrowsAny<Exception>(() => Ioc.Resolve<string>("Adapters.Template.Class"));
        }

        [Fact]
        public void Templates_WithNullDirectory_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyAdapterTemplates(null!));
        }

        [Fact]
        public void Build_UsesTheClassTemplateRegisteredInTheCurrentScope()
        {
            var custom = Ioc.Resolve<string>("Adapters.Template.Class")
                .Replace("namespace SpaceBattle.Generated", "namespace Team.Custom");
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", gameScope).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "Adapters.Template.Class", (Func<object[], object>)(args => custom)).Execute();

            var source = new AdapterSourceBuilder().Build(typeof(ITestMovingObject));
            var factory = new GeneratedCodeCompiler().Compile<IAdapterFactory>(source);

            Assert.Contains("namespace Team.Custom", source);
            Assert.IsAssignableFrom<ITestMovingObject>(factory.Create(new Dictionary<string, object>()));
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
