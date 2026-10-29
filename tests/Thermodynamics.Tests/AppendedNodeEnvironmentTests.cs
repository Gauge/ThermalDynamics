using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// One active node and one appended node verify cached heat totals at the frozen step
    /// boundary. The appended node joins the next step, including its generation, temperature,
    /// and sun/wind publication. A two-node pending sun sweep also exercises the public drain.
    /// </summary>
    public class AppendedNodeEnvironmentTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AppendingAfterTheFirstSubstepPreservesCachedGenerationTotals(bool generalEnvironment)
        {
            var settings = new ThermalSettings { EnableEnvironment = generalEnvironment,
                EnableRadiation = true, EnableConvection = false, EnableSolarHeat = false,
                EnableWasteHeat = true, EnableConduction = false, EnableDamage = false,
                HeatTimeScale = 1f, MaxSubsteps = 2, MaxSubstepsPerBlock = 0 };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var first = simulation.AddBlock(new BlockInstance(Model(100f), Vector3I.Zero,
                BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            var solver = simulation.Solver;
            var environment = EnvironmentState.Vacuum(300f);
            Assert.True(solver.BeginStep(.5f, environment, 2f));
            Assert.Equal(2, solver.LastSubsteps);
            // One environment visit and one apply visit finish the first substep.
            Assert.False(solver.AdvanceStep(2));
            Assert.Equal(100f, solver.LastHeatGainWatts);

            var appended = simulation.AddBlock(new BlockInstance(Model(200f), new Vector3I(3, 0, 0),
                BlockOrientation.Identity), 500f);
            Assert.True(solver.StepInFlight);
            Assert.True(solver.AdvanceStep(long.MaxValue));
            Assert.Equal(100f, solver.LastHeatGainWatts);
            Assert.Equal(300.5f, first.Temperature);
            Assert.Equal(500f, appended.Temperature);

            solver.Step(.5f, environment);
            Assert.Equal(300f, solver.LastHeatGainWatts);
            Assert.Equal(301f, first.Temperature);
            Assert.Equal(501f, appended.Temperature);
        }

        [Fact]
        public void AppendedNodesJoinSunAndWindPublicationOnTheNextPreparedStep()
        {
            var settings = new ThermalSettings { EnableEnvironment = true, EnableRadiation = false,
                EnableConvection = true, EnableSolarHeat = true, SolarSelfShadowing = false,
                EnableWindwardShielding = true, EnableWasteHeat = false, EnableConduction = false,
                EnableDamage = false, HeatTimeScale = 1f, MaxSubsteps = 2, MaxSubstepsPerBlock = 0 };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            simulation.AddBlock(new BlockInstance(Model(0f), Vector3I.Zero, BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            var solver = simulation.Solver;
            var environment = EnvironmentState.Vacuum(300f);
            environment.SolarEnergy = 100f;
            environment.IsSolarOccluded = false;
            environment.SunDirectionLocal = Vector3.UnitX;
            environment.AtmosphereFactor = 1f;
            environment.ConvectionCoefficient = 1f;
            environment.WindSpeed = 1f;
            environment.WindDirectionLocal = Vector3.UnitX;
            Assert.True(solver.BeginStep(.1f, environment, 2f));
            Assert.False(solver.AdvanceStep(2));

            var appended = simulation.AddBlock(new BlockInstance(Model(0f), new Vector3I(0, 3, 0),
                BlockOrientation.Identity), 300f);
            Assert.True(solver.AdvanceStep(long.MaxValue));
            var wind = (float[])typeof(ThermalSolver).GetField("nodeWindLit",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(0f, solver.SunLitFraction(1, face));
                Assert.Equal(0f, wind[Face.Count + face]);
            }

            solver.Step(.1f, environment);
            bool windPublished = false;
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(1f, solver.SunLitFraction(1, face));
                float expected = solver.WindShadow.FaceLitFraction(appended.Block, face);
                Assert.Equal(expected, wind[Face.Count + face]);
                if (expected > 0f) windPublished = true;
            }
            Assert.True(windPublished);

            solver.SunLitBudget = 1;
            solver.MarkSunOccludersChanged();
            Assert.True(solver.BeginStep(.1f, environment, 1f));
            Assert.True(solver.AdvanceStep(long.MaxValue));
            Assert.True(solver.SunLitRefreshPending);
            solver.FinishSunLit();
            Assert.False(solver.SunLitRefreshPending);
            for (int face = 0; face < Face.Count; face++)
                Assert.Equal(1f, solver.SunLitFraction(1, face));
        }

        private static BlockModel Model(float generation)
        {
            return BlockModel.Solid("generation", Vector3I.One, 1f,
                new BlockThermalProperties { Conductivity = 0f, SpecificHeat = 100f,
                    Emissivity = 0f, HeatSourceWatts = generation });
        }
    }
}
