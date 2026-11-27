using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class TopologicalOrderBuilderTests
    {
        private readonly TopologicalOrderBuilder _builder = new TopologicalOrderBuilder();

        [Fact]
        public void SortActionSteps_WithNoConstraints_PreservesDeclarationOrder()
        {
            var steps = new[] { "Commands.Move", "Commands.BurnFuel", "Commands.CheckFuel" };

            var ordered = _builder.SortActionSteps("Move", steps, Array.Empty<string>());

            Assert.Equal(steps, ordered);
        }

        [Fact]
        public void SortActionSteps_LinearChain_SortsAccordingToConstraints()
        {
            var steps = new[] { "A", "B", "C" };
            var constraints = new[] { "C before B", "B before A" };

            var ordered = _builder.SortActionSteps("Chain", steps, constraints);

            Assert.Equal(new[] { "C", "B", "A" }, ordered);
        }

        [Fact]
        public void SortActionSteps_DiamondDag_BreaksTiesByDeclarationIndexAndDecrementsInDegree()
        {
            var steps = new[] { "D", "B", "C", "A" };
            var constraints = new[] { "A before B", "A before C", "B before D", "C before D" };

            var ordered = _builder.SortActionSteps("Diamond", steps, constraints);

            Assert.Equal(new[] { "A", "B", "C", "D" }, ordered);
        }

        [Fact]
        public void SortActionSteps_DuplicateEdge_IsIdempotent()
        {
            var steps = new[] { "B", "A" };
            var constraints = new[] { "A before B", "A before B" };

            var ordered = _builder.SortActionSteps("Dup", steps, constraints);

            Assert.Equal(new[] { "A", "B" }, ordered);
        }

        [Fact]
        public void SortActionSteps_SelfLoop_ThrowsInvalidOperationException()
        {
            var steps = new[] { "A", "B" };
            var constraints = new[] { "A before A" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => _builder.SortActionSteps("SelfLoopAction", steps, constraints));

            Assert.Contains("SelfLoopAction", ex.Message);
            Assert.Contains("A", ex.Message);
        }

        [Fact]
        public void SortActionSteps_TwoNodeCycle_ThrowsInvalidOperationException()
        {
            var steps = new[] { "A", "B" };
            var constraints = new[] { "A before B", "B before A" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => _builder.SortActionSteps("Cycle2", steps, constraints));

            Assert.Contains("Cycle2", ex.Message);
            Assert.Contains("A", ex.Message);
            Assert.Contains("B", ex.Message);
        }

        [Fact]
        public void SortActionSteps_ThreeNodeCycleWithTail_ThrowsWithOnlyCycledNodes()
        {
            var steps = new[] { "X", "A", "B", "C" };
            var constraints = new[] { "X before A", "A before B", "B before C", "C before A" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => _builder.SortActionSteps("Cycle3", steps, constraints));

            Assert.Contains("Cycle3", ex.Message);
            Assert.Contains("A", ex.Message);
            Assert.Contains("B", ex.Message);
            Assert.Contains("C", ex.Message);
            Assert.DoesNotContain("X", ex.Message);
        }

        [Theory]
        [InlineData("A")]
        [InlineData("A after B")]
        [InlineData("A before B before C")]
        public void SortActionSteps_InvalidConstraintSyntax_ThrowsArgumentException(string constraint)
        {
            Assert.Throws<ArgumentException>(
                () => _builder.SortActionSteps("BadSyntax", new[] { "A", "B", "C" }, new[] { constraint }));
        }

        [Theory]
        [InlineData("Ghost before A")]
        [InlineData("A before Ghost")]
        public void SortActionSteps_UnknownStepInConstraint_ThrowsArgumentException(string constraint)
        {
            Assert.Throws<ArgumentException>(
                () => _builder.SortActionSteps("Unknown", new[] { "A", "B" }, new[] { constraint }));
        }

        [Fact]
        public void Sort_InvalidArguments_ThrowExpectedExceptions()
        {
            Assert.Throws<ArgumentNullException>(() => _builder.Sort(null!, new[] { "A" }, Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentException>(() => _builder.Sort("   ", new[] { "A" }, Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentNullException>(() => _builder.Sort("G", null!, Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentException>(() => _builder.Sort("G", Array.Empty<string>(), Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentNullException>(() => _builder.Sort("G", new[] { "A" }, null!));
            Assert.Throws<ArgumentException>(() => _builder.Sort("G", new[] { "   " }, Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentException>(() => _builder.Sort("G", new[] { "A", "A" }, Array.Empty<(string, string)>()));
            Assert.Throws<ArgumentNullException>(() => _builder.SortActionSteps("G", new[] { "A" }, null!));
            Assert.Throws<ArgumentException>(() => _builder.SortActionSteps("G", new[] { "A" }, new[] { "   " }));
            Assert.Throws<ArgumentException>(() => _builder.Sort("G", new[] { "A" }, new[] { ("", "A") }));
            Assert.Throws<ArgumentException>(() => _builder.Sort("G", new[] { "A" }, new[] { ("A", "") }));
        }
    }
}
