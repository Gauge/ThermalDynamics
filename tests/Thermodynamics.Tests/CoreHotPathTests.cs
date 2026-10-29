using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Tiny frozen-reference checks for the merged stability walk and coolant exchange hot path.
    /// Four blocks exercise clamp, fill, flow and mixed-parcel cases without a corpus or large hull.
    /// The reference arithmetic is the pre-optimization implementation, not an alternate caller of it.
    /// </summary>
    public class CoreHotPathTests
    {
        private static T Field<T>(ThermalSolver solver, string name)
        {
            return (T)typeof(ThermalSolver).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
        }

        internal static ThermalSimulation FourBlocks(ThermalSettings settings)
        {
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            for (int i = 0; i < 4; i++)
            {
                var thermal = new BlockThermalProperties { Conductivity = 83.333f,
                    SpecificHeat = 400f + i * 31f, Emissivity = .2f + i * .2f };
                var model = BlockModel.Solid("test", Vector3I.One, 1f + i * 7f, thermal);
                simulation.AddBlock(new BlockInstance(model, new Vector3I(i % 2, i / 2, 0),
                    BlockOrientation.Identity), 250f + 317f * i);
            }
            simulation.RebuildAll();
            return simulation;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(6)]
        public void FusedStabilityMatchesTheFrozenSecondWalk(int floor)
        {
            var settings = new ThermalSettings { MaxSubstepsPerBlock = floor };
            var simulation = FourBlocks(settings);
            var environment = EnvironmentSolver.Solve(settings, simulation.Planet,
                EnvironmentSample.DarkVacuum());
            environment.AtmosphereFactor = .6f;
            environment.ConvectionCoefficient = 25f;
            for (int step = 0; step < 5; step++)
            {
                simulation.Solver.Nodes[0].Temperature += 127f;
                float actual = simulation.Solver.RequiredSubsteps(.173f, environment);
                Assert.Equal(LegacyNodeDemand(simulation.Solver, settings, environment, .173f), actual);
            }
            simulation.RemoveBlock(simulation.Grid.Blocks[3]);
            float afterRemoval = simulation.Solver.RequiredSubsteps(.173f, environment);
            Assert.Equal(LegacyNodeDemand(simulation.Solver, settings, environment, .173f), afterRemoval);
        }

        private static float LegacyNodeDemand(ThermalSolver solver, ThermalSettings settings,
            EnvironmentState environment, float seconds)
        {
            float[] conductance = Field<float[]>(solver, "nodeConductanceTotal");
            float[] temperatures = Field<float[]>(solver, "nodeTemperatures");
            float[] mass = Field<float[]>(solver, "nodeThermalMass");
            float[] radiation = Field<float[]>(solver, "nodeRadiation");
            float[] area = Field<float[]>(solver, "nodeExposedArea");
            int[] faces = Field<int[]>(solver, "nodeExposedFaces");
            bool radiating = settings.EnableEnvironment && settings.EnableRadiation;
            bool convecting = settings.EnableEnvironment && settings.EnableConvection;
            float convection = convecting ? environment.ConvectionCoefficient * environment.AtmosphereFactor : 0f;
            float worst = 0f;
            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                float rate = conductance[i];
                if ((radiating || convecting) && faces[i] > 0)
                {
                    if (radiating)
                    {
                        float t = temperatures[i];
                        rate += 4f * radiation[i] * t * t * t;
                    }
                    rate += convection * area[i];
                }
                float perNode = rate / mass[i];
                if (perNode > worst) worst = perNode;
            }
            return worst <= 0f ? 1f : (seconds * worst) / ThermalSolver.StabilitySafetyFactor;
        }

        [Theory]
        [InlineData(false, 4f, 1f, 1)]
        [InlineData(false, -4f, .37f, 1)]
        [InlineData(false, 0f, .37f, 1)]
        [InlineData(false, 4f, 0f, 1)]
        [InlineData(false, 4f, 1f, 4096)]
        [InlineData(true, 4f, .37f, 1)]
        public void CoolantExchangeMatchesTheFrozenLoop(bool mixed, float flow, float fill, int cap)
        {
            var settings = new ThermalSettings { EnableConduction = false, EnableEnvironment = false,
                EnableSolarHeat = false, EnableWasteHeat = false, EnableDamage = false,
                WellMixedCoolant = mixed, MaxSubsteps = cap };
            var simulation = FourBlocks(settings);
            var solver = simulation.Solver;
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 400f);
            foreach (var block in simulation.Grid.Blocks) loop.Pipes.Add(block);
            solver.SetLoops(new List<CoolantLoop> { loop });
            loop.FillFraction = fill;
            loop.FlowSegmentsPerSecond = flow;
            loop.Links.Clear();
            for (int i = 0; i < 4; i++)
            {
                loop.Links.Add(new LoopLink(i, 800f + i, i));
                loop.Links.Add(new LoopLink((i + 1) % 4, 23f, i));
                loop.SetSegmentTemperature(i, 311f + i * 79f);
            }
            loop.Advect(.25f);
            var environment = EnvironmentSolver.Solve(settings, simulation.Planet,
                EnvironmentSample.DarkVacuum());
            solver.BeginStep(settings.StepSeconds, environment);
            solver.AdvanceStep(4);
            float h = settings.StepSeconds / solver.LastSubsteps;
            float[] expectedNodeWatts = (float[])Field<float[]>(solver, "nodeWatts").Clone();
            float[] expectedParcelWatts = new float[loop.ParcelCount];
            float absorbed, rejected;
            LegacyExchange(solver, loop, h, expectedNodeWatts, expectedParcelWatts, out absorbed, out rejected);
            if (fill > 0f) Assert.Contains(expectedNodeWatts, watts => watts != 0f);
            if (cap == 4096) Assert.False(solver.ConductionClampLive);
            typeof(ThermalSolver).GetMethod("AccumulateLoops", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(solver, new object[] { h });
            Assert.Equal(expectedNodeWatts, Field<float[]>(solver, "nodeWatts"));
            Assert.Equal(expectedParcelWatts, loop.SegmentWatts);
            Assert.Equal(absorbed, loop.AbsorbedEnergy);
            Assert.Equal(rejected, loop.RejectedEnergy);
        }

        internal static void LegacyExchange(ThermalSolver solver, CoolantLoop loop, float h,
            float[] nodeWatts, float[] parcelWatts, out float absorbed, out float rejected)
        {
            float[] temperatures = Field<float[]>(solver, "nodeTemperatures");
            float[] masses = Field<float[]>(solver, "nodeThermalMass");
            float[] nodeRelaxation = Field<float[]>(solver, "nodeRelaxation");
            bool clamp = solver.ConductionClampLive;
            float relaxation = 1f;
            if (clamp && h > 0f)
            {
                float[] total = new float[loop.PipeCount];
                for (int i = 0; i < loop.Links.Count; i++)
                {
                    LoopLink probe = loop.Links[i];
                    if (probe.SegmentIndex < 0 || probe.SegmentIndex >= loop.PipeCount) continue;
                    total[loop.ParcelOf(probe.SegmentIndex)] += loop.LinkConductance(i);
                }
                for (int i = 0; i < loop.PipeCount; i++)
                {
                    if (total[i] <= 0f || loop.SegmentThermalMass <= 0f) continue;
                    float stable = loop.SegmentThermalMass / (h * total[i]);
                    if (stable < relaxation) relaxation = stable;
                }
            }
            absorbed = 0f;
            rejected = 0f;
            for (int i = 0; i < loop.Links.Count; i++)
            {
                LoopLink link = loop.Links[i];
                int parcel = loop.ParcelOf(link.SegmentIndex);
                float difference = loop.SegmentTemperature(link.SegmentIndex) - temperatures[link.NodeIndex];
                float exchange = loop.LinkConductance(i) * difference;
                if (clamp)
                {
                    float combined = loop.SegmentThermalMass + masses[link.NodeIndex];
                    if (h > 0f && combined > 0f)
                    {
                        float maxEnergy = difference * (loop.SegmentThermalMass * masses[link.NodeIndex] / combined);
                        if (Math.Abs(exchange * h) > Math.Abs(maxEnergy)) exchange = maxEnergy / h;
                    }
                    float scale = relaxation;
                    if (nodeRelaxation[link.NodeIndex] < scale) scale = nodeRelaxation[link.NodeIndex];
                    if (scale < 1f) exchange *= scale;
                }
                nodeWatts[link.NodeIndex] += exchange;
                parcelWatts[parcel] -= exchange;
                if (exchange < 0f) absorbed -= exchange * h;
                else rejected += exchange * h;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(7)]
        public void ParcelWrapMatchesTheOriginalModuloForEveryRotationAndExternalIndex(int count)
        {
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 300f);
            for (int i = 0; i < count; i++) loop.Pipes.Add(null);
            loop.RefreshThermalMass();
            loop.FlowSegmentsPerSecond = 1f;
            for (int shift = 0; shift < Math.Max(1, count); shift++)
            {
                int[] indices = { int.MinValue, -17, -1, 0, 1, 6, 7, 15, int.MaxValue };
                foreach (int index in indices)
                {
                    int expected = count == 0 ? 0 : unchecked(index - shift) % count;
                    if (expected < 0) expected += count;
                    Assert.Equal(expected, loop.ParcelOf(index));
                }
                loop.Advect(1f);
            }
        }

        [Fact]
        public void UninitializedPublicLoopDoesNotEnterTheParcelScratchBuffer()
        {
            var simulation = FourBlocks(new ThermalSettings { MaxSubsteps = 1 });
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 300f);
            loop.Pipes.Add(simulation.Grid.Blocks[0]);
            loop.Links.Add(new LoopLink(0, 100f, 0));
            simulation.Solver.Loops.Add(loop);
            simulation.Solver.GateConductionClamp = false;
            var environment = EnvironmentSolver.Solve(simulation.Settings, simulation.Planet,
                EnvironmentSample.DarkVacuum());
            simulation.Solver.Step(simulation.Settings.StepSeconds, environment);
            Assert.Equal(0, loop.ParcelCount);
        }

        [Fact]
        public void ConductanceScansObserveFreshLoopFactorsAndPreserveInvalidContactBounds()
        {
            var settings = new ThermalSettings();
            settings.Derive();
            var solver = new ThermalSimulation(settings, new GridModel(2.5f)).Solver;
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 300f);
            loop.Pipes.Add(null);
            loop.Pipes.Add(null);
            loop.RefreshThermalMass();
            loop.Links.Add(new LoopLink(0, 7.03125f, 0));
            loop.Links.Add(new LoopLink(0, .333333f, 1));
            loop.Links.Add(new LoopLink(0, 10.125f, 0));
            loop.Links.Add(new LoopLink(0, 99f, -1));
            loop.Links.Add(new LoopLink(0, 77f, 2));
            solver.Loops.Add(loop);
            var segment = typeof(ThermalSolver).GetMethod("SegmentConductance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var coupled = typeof(ThermalSolver).GetMethod("RecomputeCoupledConductance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            float[] fills = { 1f, .37f, 0f, .9f, .67f };
            float[] flows = { 0f, 0f, 4f, -4f, 0f };
            float[] stagnant = { .25f, -1f, 2f, .7f, 2f };
            for (int change = 0; change < fills.Length; change++)
            {
                loop.FillFraction = fills[change];
                loop.FlowSegmentsPerSecond = flows[change];
                loop.Properties.StagnantTransferFraction = stagnant[change];
                loop.WellMixed = change == 3;
                loop.Advect(.25f);

                // Frozen scans use the unchanged public per-contact calculation. The full
                // loop total includes invalid segment links; the per-parcel maximum skips them.
                float total = 0f;
                var parcels = new float[loop.ParcelCount];
                for (int i = 0; i < loop.Links.Count; i++)
                {
                    float conductance = loop.LinkConductance(i);
                    total += conductance;
                    int pipe = loop.Links[i].SegmentIndex;
                    if (pipe < 0 || pipe >= loop.PipeCount) continue;
                    parcels[loop.ParcelOf(pipe)] += conductance;
                }
                float worst = 0f;
                foreach (float value in parcels) if (value > worst) worst = value;
                Assert.Equal(worst, (float)segment.Invoke(solver, new object[] { 0 }));
                coupled.Invoke(solver, null);
                Assert.Equal(total, Field<float[]>(solver, "loopConductanceTotal")[0]);
            }
            loop.Links.Clear();
            Assert.Equal(0f, (float)segment.Invoke(solver, new object[] { 0 }));
            coupled.Invoke(solver, null);
            Assert.Equal(0f, Field<float[]>(solver, "loopConductanceTotal")[0]);
        }
    }
}
