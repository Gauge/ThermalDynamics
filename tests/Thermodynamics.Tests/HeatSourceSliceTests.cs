using System;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Four blocks preserve the frozen source sum while yielding at each work-unit boundary.</summary>
    public class HeatSourceSliceTests
    {
        private static T Field<T>(ThermalSolver solver, string name)
        {
            return (T)typeof(ThermalSolver).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
        }

        private static ThermalSimulation FourBlocks(bool conduction)
        {
            var settings = new ThermalSettings { EnableEnvironment = false, EnableSolarHeat = false,
                EnableWasteHeat = false, EnableDamage = false, EnableConduction = conduction,
                MaxSubsteps = 3, MaxSubstepsPerBlock = 0, HeatTimeScale = 1f };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            for (int i = 0; i < 4; i++)
            {
                var thermal = new BlockThermalProperties { Conductivity = 200f,
                    SpecificHeat = 20f + i, Emissivity = .23f + .17f * i };
                var model = BlockModel.Solid("slice", Vector3I.One, 1f + i, thermal);
                simulation.AddBlock(new BlockInstance(model, new Vector3I(i % 2, i / 2, 0),
                    BlockOrientation.Identity), 250f + i * 40f);
            }
            simulation.RebuildAll();
            simulation.Solver.CollectDiagnostics = true;
            return simulation;
        }

        private static EnvironmentState Environment(ThermalSimulation simulation)
        {
            var sample = EnvironmentSample.DarkVacuum();
            sample.HeatSources = new[] {
                new HeatSourceState(new Vector3(.3f, .4f, -.5f), 123.45f),
                new HeatSourceState(Vector3.Left, 0f),
                new HeatSourceState(new Vector3(-.7f, -.2f, .6f), 789.123f)
            };
            sample.HeatSourceCount = sample.HeatSources.Length;
            return EnvironmentSolver.Solve(simulation.Settings, simulation.Planet, sample);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void SourceSlicesRespectTheirBudgetAndMatchTheFrozenSum(int budget)
        {
            var simulation = FourBlocks(false);
            var solver = simulation.Solver;
            var environment = Environment(simulation);
            Assert.True(solver.BeginStep(.0001f, environment));
            Assert.Equal(1, solver.LastSubsteps);
            Assert.False(solver.AdvanceStep(4)); // Exactly the ordinary environment pass.
            Assert.Equal(4L, solver.LastAdvanceWork);

            float[] expected = (float[])Field<float[]>(solver, "nodeWatts").Clone();
            float[] sourceWatts = new float[4];
            float total = Field<float>(solver, "heatGainAccumulator");
            FrozenSourcePass(solver, environment, expected, sourceWatts, ref total);

            int remaining = 4 * environment.HeatSourceCount;
            while (remaining > 0)
            {
                int slice = Math.Min(budget, remaining);
                Assert.False(solver.AdvanceStep(slice));
                Assert.Equal((long)slice, solver.LastAdvanceWork);
                remaining -= slice;
            }
            Assert.Equal(expected, Field<float[]>(solver, "nodeWatts"));
            Assert.Equal(total, solver.LastHeatGainWatts);
            for (int i = 0; i < 4; i++)
                Assert.Equal(sourceWatts[i], solver.Nodes[i].LastHeatSourceWatts);
            while (!solver.AdvanceStep(1)) Assert.InRange(solver.LastAdvanceWork, 0L, 1L);
        }

        // The pre-slicing source-major arithmetic, kept independent of the production range helper.
        private static void FrozenSourcePass(ThermalSolver solver, EnvironmentState environment,
            float[] watts, float[] sourceWatts, ref float total)
        {
            var exposed = Field<int[]>(solver, "nodeExposedFaces");
            var absorption = Field<float[]>(solver, "nodeAbsorptivity");
            var area = Field<float[]>(solver, "nodeExposedArea");
            var faces = Field<float[]>(solver, "nodeFaceWeights");
            for (int s = 0; s < environment.HeatSourceCount; s++)
            {
                var source = environment.HeatSources[s];
                if (source.Irradiance <= 0f) continue;
                float[] weights = new float[6];
                for (int f = 0; f < 6; f++)
                {
                    float dot = Vector3.Dot(Face.Normals[f], source.DirectionLocal);
                    weights[f] = dot > 0f ? dot : 0f;
                }
                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    if (exposed[i] <= 0) continue;
                    int b = i * 6;
                    float weighted = faces[b] * weights[0] + faces[b + 1] * weights[1]
                        + faces[b + 2] * weights[2] + faces[b + 3] * weights[3]
                        + faces[b + 4] * weights[4] + faces[b + 5] * weights[5];
                    float gain = source.Irradiance * absorption[i] * weighted * area[i];
                    if (gain == 0f) continue;
                    watts[i] += gain;
                    sourceWatts[i] += gain;
                    total += gain;
                }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void SourceSlicesStayIdenticalAcrossSubstepsAndAnAbandonedStep(int budget)
        {
            var whole = FourBlocks(true);
            var spread = FourBlocks(true);
            var environment = Environment(whole);
            spread.Solver.BeginStep(.5f, environment);
            spread.Solver.AdvanceStep(5); // Abandon after one source/block pair.
            spread.Solver.AbandonStep();

            for (int step = 0; step < 3; step++)
            {
                environment.HeatSourceCount = step == 1 ? 0 : 3;
                whole.Solver.Step(.5f, environment);
                spread.Solver.BeginStep(.5f, environment);
                Assert.Equal(3, spread.Solver.LastSubsteps);
                long expectedWork = spread.Solver.StepWorkUnits;
                long spent = 0;
                int slices = 0;
                bool complete;
                do
                {
                    complete = spread.Solver.AdvanceStep(budget);
                    Assert.InRange(spread.Solver.LastAdvanceWork, 0L, (long)budget);
                    spent += spread.Solver.LastAdvanceWork;
                    Assert.True(++slices < 200);
                } while (!complete);
                Assert.Equal(expectedWork, spent);
                Assert.Equal(whole.Solver.LastHeatGainWatts, spread.Solver.LastHeatGainWatts);
                for (int i = 0; i < 4; i++)
                {
                    Assert.Equal(whole.Solver.Nodes[i].Temperature, spread.Solver.Nodes[i].Temperature);
                    Assert.Equal(whole.Solver.Nodes[i].LastDeltaTemperature,
                        spread.Solver.Nodes[i].LastDeltaTemperature);
                    Assert.Equal(whole.Solver.Nodes[i].LastHeatSourceWatts,
                        spread.Solver.Nodes[i].LastHeatSourceWatts);
                }
            }
        }
    }
}
