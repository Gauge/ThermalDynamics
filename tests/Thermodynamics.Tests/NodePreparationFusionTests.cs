using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Four nodes compare fused preparation with frozen separate mirror, floor and demand walks.
    /// Direct temperature/property edits, floor restoration and sliced publication keep their
    /// established behavior; explicit prewarming still uses the separate preparation path.
    /// </summary>
    public class NodePreparationFusionTests
    {
        private static T Field<T>(ThermalSolver solver, string name)
        {
            return (T)typeof(ThermalSolver).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
        }

        private sealed class Expected
        {
            public readonly Dictionary<string, float[]> Rows = new Dictionary<string, float[]>();
            public int[] Faces;
            public int Floored, MassFactorFrom;
            public float Critical, Demand;
        }

        [Fact]
        public void FusedPreparationMatchesFrozenRowsAcrossMutableInputsAndFloorRestoration()
        {
            var settings = new ThermalSettings { HeatTimeScale = 1f, MaxSubstepsPerBlock = 0 };
            var simulation = CoreHotPathTests.FourBlocks(settings);
            var solver = simulation.Solver;
            var environment = EnvironmentState.Vacuum(271.25f);
            environment.AtmosphereFactor = .63f;
            environment.ConvectionCoefficient = 17.25f;

            for (int change = 0; change < 3; change++)
            {
                settings.MaxSubstepsPerBlock = change == 1 ? 1 : 0;
                settings.EnableConduction = change != 2;
                solver.Nodes[0].Temperature += 137.25f; // Public writes need no dirty flag.
                if (change == 1)
                {
                    solver.Nodes[0].Block.Mass = .001f;
                    solver.Nodes[0].RefreshThermalMass();
                    solver.Nodes[1].SetExposedFaces(new int[6]);
                    solver.Nodes[2].SetExposedFaces(new[] { 1, 2, 3, 0, 1, 0 });
                    solver.Nodes[2].Thermal.HeatSourceWatts = 23.25f;
                    solver.Nodes[2].RefreshHeatGeneration();
                    solver.Nodes[3].Thermal.CriticalTemperature = 537.5f;
                    solver.Nodes[3].StateDirty = true;
                }

                Expected expected = FrozenPreparation(solver, environment, .173f);
                long syncs = solver.Work.NodeStateSyncs;
                long estimates = solver.Work.StabilityEstimates;
                long full = solver.Work.FullNodeResyncs + (Field<bool>(solver, "resyncAll") ? 1 : 0);
                Assert.Equal(expected.Demand, solver.RequiredSubsteps(.173f, environment));
                foreach (var row in expected.Rows) Assert.Equal(row.Value, Field<float[]>(solver, row.Key));
                Assert.Equal(expected.Faces, Field<int[]>(solver, "nodeExposedFaces"));
                Assert.Equal(expected.Floored, solver.FlooredNodes);
                Assert.Equal(expected.Critical, Field<float>(solver, "lowestCritical"));
                Assert.Equal(expected.MassFactorFrom, Field<int>(solver, "linkMassFactorFrom"));
                Assert.Equal(syncs + 1, solver.Work.NodeStateSyncs);
                Assert.Equal(estimates + 1, solver.Work.StabilityEstimates);
                Assert.Equal(full, solver.Work.FullNodeResyncs);
                foreach (ThermalNode node in solver.Nodes) Assert.False(node.StateDirty);
                if (change == 1) Assert.True(solver.FlooredNodes > 0);
                if (change == 2)
                    for (int i = 0; i < solver.Nodes.Count; i++)
                        Assert.Equal(solver.Nodes[i].ThermalMass, Field<float[]>(solver, "nodeThermalMass")[i]);

                // Explicit prewarming neither reports a stability estimate nor changes the result.
                solver.PrepareForSteps();
                Assert.Equal(estimates + 1, solver.Work.StabilityEstimates);
                Expected profile = FrozenPreparation(solver, environment, settings.StepSeconds);
                Assert.Equal(profile.Demand, solver.ProfileSubsteps().RequiredSubstepsInForce);
            }
        }

        [Fact]
        public void FusedBeginKeepsSlicedPublicationAndExternalTemperatureWrites()
        {
            var reference = CoreHotPathTests.FourBlocks(new ThermalSettings { HeatTimeScale = 1f,
                MaxSubstepsPerBlock = 1, MaxSubsteps = 3, EnableEnvironment = false,
                EnableSolarHeat = false, EnableWasteHeat = false, EnableDamage = false });
            var sliced = CoreHotPathTests.FourBlocks(new ThermalSettings { HeatTimeScale = 1f,
                MaxSubstepsPerBlock = 1, MaxSubsteps = 3, EnableEnvironment = false,
                EnableSolarHeat = false, EnableWasteHeat = false, EnableDamage = false });
            foreach (var simulation in new[] { reference, sliced })
            {
                simulation.Solver.Nodes[0].Block.Mass = .001f;
                simulation.Solver.Nodes[0].RefreshThermalMass();
            }
            var environment = EnvironmentState.Vacuum(reference.Settings.VacuumTemperature);
            const float seconds = .5f;
            float demand = FrozenPreparation(reference.Solver, environment, seconds).Demand;
            reference.Solver.PrepareForSteps(); // Existing mirror-only preparation path.
            reference.Solver.Step(seconds, environment, demand);

            var solver = sliced.Solver;
            Assert.True(solver.BeginStep(seconds, environment));
            Assert.Equal(reference.Solver.LastSubsteps, solver.LastSubsteps);
            Assert.True(solver.LastSubsteps > 1);
            float[] starting = (float[])Field<float[]>(solver, "nodeStepStart").Clone();
            long spent = 0;
            Assert.False(solver.AdvanceStep(1));
            spent += solver.LastAdvanceWork;
            solver.Nodes[0].Temperature += 17f;
            long syncs = solver.Work.NodeStateSyncs;
            float[] pending = (float[])Field<float[]>(solver, "nodeTemperatures").Clone();
            solver.RequiredSubsteps(.1f);
            Assert.Equal(syncs, solver.Work.NodeStateSyncs);
            Assert.Equal(pending, Field<float[]>(solver, "nodeTemperatures"));

            for (int slices = 0; Field<object>(solver, "stage").ToString() != "Publish"; slices++)
            {
                Assert.True(slices < 128);
                Assert.False(solver.AdvanceStep(1));
                spent += solver.LastAdvanceWork;
                for (int i = 0; i < 4; i++)
                    Assert.Equal(starting[i] + (i == 0 ? 17f : 0f), solver.Nodes[i].Temperature);
            }
            while (!solver.AdvanceStep(1)) spent += solver.LastAdvanceWork;
            spent += solver.LastAdvanceWork;
            Assert.Equal(solver.StepWorkUnits, spent);
            for (int i = 0; i < 4; i++)
            {
                float expected = reference.Solver.Nodes[i].Temperature;
                if (i == 0) expected = Math.Max(ThermalConstants.MinimumTemperature,
                    (starting[i] + 17f) + (expected - starting[i]));
                Assert.Equal(expected, solver.Nodes[i].Temperature);
                Assert.Equal(reference.Solver.Nodes[i].LastDeltaTemperature, solver.Nodes[i].LastDeltaTemperature);
            }
        }

        // Frozen node-only preparation: copy every temperature, refresh dirty properties, then
        // perform separate floor and demand walks. No production preparation/rate helper is used.
        private static Expected FrozenPreparation(ThermalSolver solver, EnvironmentState environment, float seconds)
        {
            Assert.Empty(solver.Loops);
            Assert.Empty(solver.RoomAir);
            var expected = new Expected { Faces = (int[])Field<int[]>(solver, "nodeExposedFaces").Clone(),
                Critical = Field<float>(solver, "lowestCritical"),
                MassFactorFrom = Field<int>(solver, "linkMassFactorFrom") };
            string[] names = { "nodeTemperatures", "nodeStepStart", "nodeThermalMass", "nodeRadiation",
                "nodeGeneration", "nodeExposedArea", "nodeAbsorptivity", "nodeCritical", "nodeFaceWeights",
                "nodeConductanceTotal" };
            foreach (string name in names) expected.Rows.Add(name, (float[])Field<float[]>(solver, name).Clone());
            var rows = expected.Rows;
            bool all = Field<bool>(solver, "resyncAll");
            if (all) expected.Critical = float.PositiveInfinity;
            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                ThermalNode node = solver.Nodes[i];
                rows["nodeTemperatures"][i] = rows["nodeStepStart"][i] = node.Temperature;
                if (!all && !node.StateDirty) continue;
                if (rows["nodeThermalMass"][i] != node.ThermalMass) expected.MassFactorFrom = 0;
                rows["nodeThermalMass"][i] = node.ThermalMass;
                rows["nodeRadiation"][i] = node.RadiationCoefficient;
                rows["nodeGeneration"][i] = node.HeatGenerationWatts;
                rows["nodeExposedArea"][i] = node.ExposedArea;
                rows["nodeAbsorptivity"][i] = node.Thermal.EffectiveSolarAbsorptivity;
                float critical = rows["nodeCritical"][i] = node.Thermal.CriticalTemperature;
                if (critical > 0f && critical < expected.Critical) expected.Critical = critical;
                int total = expected.Faces[i] = node.TotalExposedFaces;
                float inverse = total <= 0 ? 0f : 1f / total;
                for (int f = 0; f < Face.Count; f++)
                    rows["nodeFaceWeights"][i * Face.Count + f] = total <= 0 ? 0f : node.GetExposedFaces(f) * inverse;
            }
            Array.Clear(rows["nodeConductanceTotal"], 0, solver.Nodes.Count);
            if (solver.Settings.EnableConduction)
                foreach (ThermalLink link in solver.Links)
                {
                    rows["nodeConductanceTotal"][link.NodeA] += link.Conductance;
                    rows["nodeConductanceTotal"][link.NodeB] += link.Conductance;
                }
            int cap = solver.EffectiveSubstepFloor;
            float step = solver.Settings.StepSeconds;
            if (cap <= 0 || step <= 0f)
            {
                if (solver.FlooredNodes > 0)
                {
                    for (int i = 0; i < solver.Nodes.Count; i++) rows["nodeThermalMass"][i] = solver.Nodes[i].ThermalMass;
                    expected.MassFactorFrom = 0;
                }
            }
            else
            {
                float perRate = step / (ThermalSolver.StabilitySafetyFactor * cap);
                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    float real = solver.Nodes[i].ThermalMass;
                    float floor = FrozenRate(solver.Settings, environment, expected, i) * perRate;
                    float wanted = real < floor ? floor : real;
                    if (rows["nodeThermalMass"][i] != wanted) expected.MassFactorFrom = 0;
                    rows["nodeThermalMass"][i] = wanted;
                    if (wanted > real) expected.Floored++;
                }
            }
            float worst = 0f;
            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                float rate = FrozenRate(solver.Settings, environment, expected, i) / rows["nodeThermalMass"][i];
                if (rate > worst) worst = rate;
            }
            expected.Demand = worst <= 0f ? 1f : (seconds * worst) / ThermalSolver.StabilitySafetyFactor;
            return expected;
        }

        private static float FrozenRate(ThermalSettings settings, EnvironmentState environment, Expected state, int i)
        {
            bool radiating = settings.EnableEnvironment && settings.EnableRadiation;
            bool convecting = settings.EnableEnvironment && settings.EnableConvection;
            float rate = state.Rows["nodeConductanceTotal"][i];
            if ((!radiating && !convecting) || state.Faces[i] <= 0) return rate;
            if (radiating)
            {
                float t = state.Rows["nodeTemperatures"][i];
                rate += 4f * state.Rows["nodeRadiation"][i] * t * t * t;
            }
            float convection = convecting ? environment.ConvectionCoefficient * environment.AtmosphereFactor : 0f;
            return rate + (convection * state.Rows["nodeExposedArea"][i]);
        }
    }
}
