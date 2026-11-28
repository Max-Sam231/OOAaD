using App;
using SpaceBattle.Lib;
using SpaceBattle.Test;

[assembly: Dependency(typeof(ITestMovingObject), "Velocity", typeof(TestVelocityUtility), "Get")]
[assembly: Dependency(typeof(ITestDuplicatedDependency), "Value", typeof(TestVelocityUtility), "Get")]
[assembly: Dependency(typeof(ITestDuplicatedDependency), "Value", typeof(TestVelocityUtility), "Other")]
[assembly: Dependency(typeof(ITestMissingStrategy), "Value", typeof(TestVelocityUtility), "Missing")]

namespace SpaceBattle.Test
{
    public interface ITestMovingObject
    {
        Vector Position { get; set; }
        Vector Velocity { get; }
    }

    public interface ITestWithMethod
    {
        void Do();
    }

    public interface ITestInheriting : ITestMovingObject
    {
        int Fuel { get; }
    }

    public interface ITestWriteOnly
    {
        int Value { set; }
    }

    public interface ITestGenericProperty
    {
        IDictionary<string, object> Items { get; }
    }

    public interface ITestDuplicatedDependency
    {
        Vector Value { get; }
    }

    public interface ITestMissingStrategy
    {
        Vector Value { get; }
    }

    public static class TestVelocityUtility
    {
        public static object Get(object[] args)
        {
            var gameItem = (IDictionary<string, object>)args[0];

            return new Vector((int)gameItem["Speed"], 0);
        }
    }

    public class TestMoveCommand : ICommand
    {
        public ITestMovingObject Obj { get; }

        public TestMoveCommand(ITestMovingObject obj)
        {
            Obj = obj;
        }

        public void Execute()
        {
        }
    }

    public class TestNotACommand
    {
        public TestNotACommand(ITestMovingObject obj)
        {
        }
    }

    public class TestTwoConstructorsCommand : ICommand
    {
        public TestTwoConstructorsCommand(ITestMovingObject obj)
        {
        }

        public TestTwoConstructorsCommand(ITestWriteOnly obj)
        {
        }

        public void Execute()
        {
        }
    }

    public class TestNoParameterCommand : ICommand
    {
        public void Execute()
        {
        }
    }

    public class TestNonInterfaceParameterCommand : ICommand
    {
        public TestNonInterfaceParameterCommand(string name)
        {
        }

        public void Execute()
        {
        }
    }
}
