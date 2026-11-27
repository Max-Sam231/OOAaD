using App;
using App.Scopes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SpaceBattle.Lib;

namespace SpaceBattle.Test
{
    public class PluginLoaderAndHotReloadTests : IDisposable
    {
        private readonly object _rootScope;
        private readonly List<string> _tempDirs = [];

        public PluginLoaderAndHotReloadTests()
        {
            new InitCommand().Execute();
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();
            _rootScope = Ioc.Resolve<object>("IoC.Scope.Current");
        }

        public void Dispose()
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Clear").Execute();

            foreach (var dir in _tempDirs)
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }

        private string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "SpaceBattle.PluginTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            return dir;
        }

        private static void EmitPluginDll(string assemblyName, string source, string outputPath)
        {
            var references = ScopedAssemblyCatalogFactory.CreateDefault().MetadataReferences;
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var result = compilation.Emit(outputPath);

            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void HotReloadCommand_WithInvalidPluginsDirectory_ThrowsArgumentException(string? pluginsDir)
        {
            Assert.ThrowsAny<ArgumentException>(() => new HotReloadGameBaseScopeCommand(pluginsDir!));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PluginLoaderFactory_WithInvalidDirectory_ThrowsArgumentException(string? pluginsDir)
        {
            Assert.ThrowsAny<ArgumentException>(() => new PluginLoaderFactory().Load(pluginsDir!));
        }

        [Fact]
        public void PluginLoaderFactory_WithMissingDirectory_ThrowsDirectoryNotFoundException()
        {
            var missingDir = Path.Combine(Path.GetTempPath(), "SpaceBattle.Missing." + Guid.NewGuid().ToString("N"));

            Assert.Throws<DirectoryNotFoundException>(() => new PluginLoaderFactory().Load(missingDir));
        }

        [Fact]
        public void HotReload_WithEmptyDirectory_SwapsToNewBaseScope()
        {
            var emptyDir = CreateTempDir();
            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInit = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Base.Ready", (Func<object[], object>)(_ => true)).Execute());

            ScopeParentHolder holder = null!;
            object initialScope = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInit).Execute();
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    initialScope = holder.Current;

                    Ioc.Resolve<ICommand>("Scopes.HotReload", emptyDir).Execute();
                })).Execute();

            Assert.NotSame(initialScope, holder.Current);
        }

        [Fact]
        public void HotReload_WithCorruptedDll_ThrowsBadImageFormatExceptionAndPreservesCurrentScope()
        {
            var dir = CreateTempDir();
            File.WriteAllText(Path.Combine(dir, "corrupted.dll"), "not-a-pe");

            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInit = new DelegatingCommand(() => { });

            ScopeParentHolder holder = null!;
            object initialScope = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInit).Execute();
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    initialScope = holder.Current;

                    Assert.Throws<BadImageFormatException>(() => Ioc.Resolve<ICommand>("Scopes.HotReload", dir).Execute());
                })).Execute();

            Assert.Same(initialScope, holder.Current);
        }

        [Fact]
        public void PluginLoaderFactory_FiltersAllSixTypeCategories_AndCompilesPluginAdapterAndCommand()
        {
            var dir = CreateTempDir();
            var source = """
                using System.Collections.Generic;
                using App;
                using SpaceBattle.Lib;

                [assembly: Dependency(typeof(SpaceBattle.TestPlugin.IPluginObject), "Computed", typeof(SpaceBattle.TestPlugin.PluginHelper), "Compute")]

                namespace SpaceBattle.TestPlugin
                {
                    public interface IPluginObject
                    {
                        int BaseValue { get; set; }
                        int Computed { get; }
                    }

                    public abstract class RegisterAbstractCommand : ICommand
                    {
                        public abstract void Execute();
                    }

                    public class PluginHelper
                    {
                        public static object Compute(object[] args)
                        {
                            return (int)((IDictionary<string, object>)args[0])["BaseValue"] + 10;
                        }
                    }

                    public class PluginActionCommand : ICommand
                    {
                        private readonly IPluginObject _obj;

                        public PluginActionCommand(IPluginObject obj)
                        {
                            _obj = obj;
                        }

                        public void Execute()
                        {
                            _obj.BaseValue = _obj.Computed;
                        }
                    }

                    public class RegisterWithArgsCommand : ICommand
                    {
                        public RegisterWithArgsCommand(int x)
                        {
                        }

                        public void Execute()
                        {
                        }
                    }

                    public class RegisterPluginV1Command : ICommand
                    {
                        public void Execute()
                        {
                            new RegisterIoCDependencyAdapter(typeof(IPluginObject)).Execute();
                            new RegisterIoCDependencyCommandWithAdapter("Commands.PluginAction", typeof(PluginActionCommand)).Execute();
                        }
                    }
                }
                """;
            EmitPluginDll("TestPluginAllSix", source, Path.Combine(dir, "TestPluginAllSix.dll"));

            var batch = new PluginLoaderFactory().Load(dir);
            Assert.Single(batch.RegistrationCommands);

            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInit = new DelegatingCommand(() => { });
            object gameScope = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInit).Execute();
                    gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                    Ioc.Resolve<ICommand>("Scopes.HotReload", dir).Execute();
                })).Execute();

            var item = new Dictionary<string, object> { ["BaseValue"] = 5 };
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                    Ioc.Resolve<ICommand>("Commands.PluginAction", item).Execute())).Execute();

            Assert.Equal(15, item["BaseValue"]);
        }

        [Fact]
        public void HotReload_PluginV1ToPluginV2_TransitionsLiveGameAndOverridesBuiltInKeyWithoutCollisions()
        {
            var dirV1 = CreateTempDir();
            var dirV2 = CreateTempDir();

            string BuildPluginSource(int delta, string versionTag, string formulaOverride) => $$"""
                using System;
                using System.Collections.Generic;
                using App;
                using SpaceBattle.Lib;

                [assembly: Dependency(typeof(SpaceBattle.HotPlugin.IHotObject), "Computed", typeof(SpaceBattle.HotPlugin.HotHelper), "Compute")]

                namespace SpaceBattle.HotPlugin
                {
                    public interface IHotObject
                    {
                        int Value { get; set; }
                        int Computed { get; }
                    }

                    public class HotHelper
                    {
                        public static object Compute(object[] args)
                        {
                            return (int)((IDictionary<string, object>)args[0])["Value"] + {{delta}};
                        }
                    }

                    public class HotActionCommand : ICommand
                    {
                        private readonly IHotObject _obj;

                        public HotActionCommand(IHotObject obj)
                        {
                            _obj = obj;
                        }

                        public void Execute()
                        {
                            _obj.Value = _obj.Computed;
                        }
                    }

                    public class RegisterHotPluginCommand : ICommand
                    {
                        public void Execute()
                        {
                            new RegisterIoCDependencyAdapter(typeof(IHotObject)).Execute();
                            new RegisterIoCDependencyCommandWithAdapter("Commands.HotAction", typeof(HotActionCommand)).Execute();
                            Ioc.Resolve<ICommand>("IoC.Register", "Plugin.Version", (Func<object[], object>)(_ => "{{versionTag}}")).Execute();
                            Ioc.Resolve<ICommand>("IoC.Register", "Game.BuiltIn.Formula", (Func<object[], object>)(_ => "{{formulaOverride}}")).Execute();
                        }
                    }
                }
                """;

            EmitPluginDll("HotPlugin", BuildPluginSource(1, "v1", "overridden-v1"), Path.Combine(dirV1, "HotPlugin.dll"));
            EmitPluginDll("HotPlugin", BuildPluginSource(100, "v2", "overridden-v2"), Path.Combine(dirV2, "HotPlugin.dll"));

            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInit = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Game.BuiltIn.Formula", (Func<object[], object>)(_ => "builtin")).Execute());

            object gameScope = null!;
            ScopeParentHolder holder = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInit).Execute();
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    gameScope = Ioc.Resolve<object>("Scopes.Game.Create");
                })).Execute();

            string initialFormula = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() => initialFormula = Ioc.Resolve<string>("Game.BuiltIn.Formula"))).Execute();
            Assert.Equal("builtin", initialFormula);

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() => Ioc.Resolve<ICommand>("Scopes.HotReload", dirV1).Execute())).Execute();

            var itemV1 = new Dictionary<string, object> { ["Value"] = 5 };
            string versionV1 = null!;
            string formulaV1 = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>("Commands.HotAction", itemV1).Execute();
                    versionV1 = Ioc.Resolve<string>("Plugin.Version");
                    formulaV1 = Ioc.Resolve<string>("Game.BuiltIn.Formula");
                })).Execute();

            Assert.Equal(6, itemV1["Value"]);
            Assert.Equal("v1", versionV1);
            Assert.Equal("overridden-v1", formulaV1);
            Assert.False(((IDictionary<string, Func<object[], object>>)holder.Current).ContainsKey("IoC.Register"));

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() => Ioc.Resolve<ICommand>("Scopes.HotReload", dirV2).Execute())).Execute();

            var itemV2 = new Dictionary<string, object> { ["Value"] = 5 };
            string versionV2 = null!;
            string formulaV2 = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() =>
                {
                    Ioc.Resolve<ICommand>("Commands.HotAction", itemV2).Execute();
                    versionV2 = Ioc.Resolve<string>("Plugin.Version");
                    formulaV2 = Ioc.Resolve<string>("Game.BuiltIn.Formula");
                })).Execute();

            Assert.Equal(105, itemV2["Value"]);
            Assert.Equal("v2", versionV2);
            Assert.Equal("overridden-v2", formulaV2);
            Assert.False(((IDictionary<string, Func<object[], object>>)holder.Current).ContainsKey("IoC.Register"));
        }

        [Fact]
        public void HotReload_WhenPluginRegistrationThrows_PreservesPreviousBaseScope()
        {
            var dir = CreateTempDir();
            var brokenSource = """
                using System;
                using App;

                namespace SpaceBattle.BrokenPlugin
                {
                    public class RegisterBrokenCommand : ICommand
                    {
                        public void Execute()
                        {
                            throw new InvalidOperationException("plugin boom");
                        }
                    }
                }
                """;
            EmitPluginDll("BrokenPlugin", brokenSource, Path.Combine(dir, "BrokenPlugin.dll"));

            var serverScope = Ioc.Resolve<object>("IoC.Scope.Create", _rootScope);
            var baseInit = new DelegatingCommand(() =>
                Ioc.Resolve<ICommand>("IoC.Register", "Base.Marker", (Func<object[], object>)(_ => "still-v1")).Execute());

            ScopeParentHolder holder = null!;
            object initialScope = null!;
            object gameScope = null!;

            new InSeparateScopeCommand(
                serverScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyScopeHierarchy(_rootScope, baseInit).Execute();
                    holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
                    initialScope = holder.Current;
                    gameScope = Ioc.Resolve<object>("Scopes.Game.Create");

                    var ex = Assert.Throws<InvalidOperationException>(
                        () => Ioc.Resolve<ICommand>("Scopes.HotReload", dir).Execute());
                    Assert.Equal("plugin boom", ex.Message);
                })).Execute();

            Assert.Same(initialScope, holder.Current);

            string marker = null!;
            new InSeparateScopeCommand(
                gameScope,
                new DelegatingCommand(() => marker = Ioc.Resolve<string>("Base.Marker"))).Execute();
            Assert.Equal("still-v1", marker);
        }
    }
}
