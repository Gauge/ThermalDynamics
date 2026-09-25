using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Thermodynamics.Core;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The harness measures optimised code, in every configuration.
    ///
    /// <para>
    /// `dotnet run` and `dotnet test` build Debug, and a Debug assembly carries
    /// `DebuggableAttribute(DisableOptimizations)`, which the JIT honours — so until 2026-08-26 every
    /// timing this harness produced was of code the game never runs, at 3.4× a step and up to 7.7×
    /// on the diagnostics surcharge. `tests/Directory.Build.props` sets `Optimize` for every project
    /// now, and this is what says it still does: a line dropped from a props file fails nothing else.
    /// See performance.md, Iteration 1.
    /// </para>
    /// </summary>
    public class OptimisedBuildTests
    {

        private static bool OptimiserDisabled(Assembly assembly)
        {
            DebuggableAttribute debuggable = assembly
                .GetCustomAttributes(typeof(DebuggableAttribute), false)
                .Cast<DebuggableAttribute>()
                .FirstOrDefault();

            return debuggable != null && debuggable.IsJITOptimizerDisabled;
        }

        [Theory]
        [InlineData(typeof(ThermalSolver))]
        [InlineData(typeof(PerformanceReport))]
        [InlineData(typeof(OptimisedBuildTests))]

        public void EveryAssemblyTheHarnessTimesIsJitOptimised(System.Type inside)
        {
            Assembly assembly = inside.Assembly;
            Assert.False(OptimiserDisabled(assembly),
                assembly.GetName().Name + " was built with the JIT optimiser disabled, so every"
                + " millisecond it reports is of code the game never runs");
        }
    }
}
