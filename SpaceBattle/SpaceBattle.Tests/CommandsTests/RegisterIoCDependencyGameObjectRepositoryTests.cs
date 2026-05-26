using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyGameObjectRepositoryTests
    {
        public RegisterIoCDependencyGameObjectRepositoryTests()
        {
            new InitCommand().Execute();
            var scope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();
        }

        [Fact]
        public void Execute_ShouldRegisterReadOnlyAndWritableRepositories()
        {
            var registerCommand = new RegisterIoCDependencyGameObjectRepository();

            registerCommand.Execute();
            var readOnlyRepo = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
            var writableRepo = Ioc.Resolve<IWritableGameObjectRepository>("Game.Repository.Writable");

            Assert.NotNull(readOnlyRepo);
            Assert.NotNull(writableRepo);
        }

        [Fact]
        public void Resolve_ShouldReturnSameUnderlyingInstance()
        {
            var registerCommand = new RegisterIoCDependencyGameObjectRepository();
            registerCommand.Execute();

            var readOnlyRepo = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
            var writableRepo = Ioc.Resolve<IWritableGameObjectRepository>("Game.Repository.Writable");

            Assert.Same(readOnlyRepo, writableRepo);
        }
    }
}