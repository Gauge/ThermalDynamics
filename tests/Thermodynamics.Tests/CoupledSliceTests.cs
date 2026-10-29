using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Four blocks cover ordered contact sums and resumable parcel reductions, without a corpus.</summary>
    public class CoupledSliceTests
    {
        private static T Field<T>(ThermalSolver solver, string name)
        {
            return (T)typeof(ThermalSolver).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
        }

        private static string Stage(ThermalSolver solver) { return Field<object>(solver, "stage").ToString(); }

        private static ThermalSimulation Build(bool mixed, bool clamp, bool pumps)
        {
            var settings = new ThermalSettings { EnableConduction = false, EnableEnvironment = false,
                EnableSolarHeat = false, EnableWasteHeat = false, EnableDamage = false,
                EnableHeatPumps = pumps, WellMixedCoolant = mixed, MaxSubsteps = 3,
                MaxSubstepsPerBlock = 0, HeatTimeScale = 1f, ClampConductionOvershoot = clamp };
            var simulation = CoreHotPathTests.FourBlocks(settings);
            var loops = new List<CoolantLoop>();
            for (int l = 0; l < 2; l++)
            {
                var loop = new CoolantLoop(LoopThermalProperties.Default(), 400f);
                foreach (var block in simulation.Grid.Blocks) loop.Pipes.Add(block);
                loops.Add(loop);
            }
            simulation.Solver.SetLoops(loops);
            for (int l = 0; l < loops.Count; l++)
            {
                var loop = loops[l];
                loop.FillFraction = l == 0 ? .37f : 1f;
                loop.FlowSegmentsPerSecond = l == 0 ? 4f : -4f;
                loop.Links.Clear();
                for (int i = 0; i < 4; i++)
                {
                    loop.Links.Add(new LoopLink(i, 20000f + l * 1000f + i * 13f, i));
                    loop.Links.Add(new LoopLink((i + 1) % 4, 750f + i, i));
                    loop.SetSegmentTemperature(i, 311f + i * 79f + l * 11f);
                }
                loop.Advect(.25f);
            }
            for (int r = 0; r < 2; r++)
            {
                var air = new RoomAirNode { RoomIndex = r, Anchor = new Vector3I(r, 0, 0), Temperature = 310f + r * 100f,
                    Volume = 1f, Pressure = 1f, AirDensity = 1.2f };
                air.RefreshThermalMass();
                for (int i = 0; i < 4; i++) air.Links.Add(new RoomLink(i, 5000f + i * 7f));
                simulation.Solver.RoomAir.Add(air);
                simulation.Solver.HeatPumps.Add(new HeatPumpDevice { Enabled = r == 0,
                    ColdNodeIndex = r, HotNodeIndex = r + 2, MaxPowerWatts = 30f, RatedWatts = 75f });
            }
            simulation.Solver.CollectDiagnostics = true;
            simulation.Solver.GateConductionClamp = false;
            return simulation;
        }

        private static EnvironmentState Environment(ThermalSimulation simulation)
        {
            return EnvironmentSolver.Solve(simulation.Settings, simulation.Planet, EnvironmentSample.DarkVacuum());
        }

        private static void ReachCoupled(ThermalSolver solver)
        {
            int advances = 0;
            while (Stage(solver) != "Coupled")
            {
                Assert.False(solver.AdvanceStep(1));
                Assert.InRange(solver.LastAdvanceWork, 0L, 1L);
                Assert.True(++advances < 32);
            }
        }

        [Theory]
        [InlineData(false, false, 1)]
        [InlineData(false, true, 3)]
        [InlineData(true, true, 1)]
        public void CoupledSlicesMatchFrozenContactEquationsAndPreserveTheApplyBoundary(bool mixed,
            bool clamp, int budget)
        {
            var simulation = Build(mixed, clamp, false);
            var solver = simulation.Solver;
            solver.BeginStep(.05f, Environment(simulation));
            ReachCoupled(solver);
            float h = Field<float>(solver, "substepSeconds");
            var expectedWatts = (float[])Field<float[]>(solver, "nodeWatts").Clone();
            var expectedParcels = new float[2][];
            var absorbed = new float[2];
            var rejected = new float[2];
            var startingParcels = new float[2][];
            for (int l = 0; l < 2; l++)
            {
                var loop = solver.Loops[l];
                expectedParcels[l] = new float[loop.ParcelCount];
                startingParcels[l] = new float[loop.ParcelCount];
                for (int p = 0; p < loop.ParcelCount; p++) startingParcels[l][p] = loop.ParcelTemperature(p);
                CoreHotPathTests.LegacyExchange(solver, loop, h, expectedWatts, expectedParcels[l],
                    out absorbed[l], out rejected[l]);
            }
            var roomWatts = new float[2];
            var roomDiagnostics = new float[4];
            FrozenRooms(solver, h, expectedWatts, roomWatts, roomDiagnostics);
            long expectedWork = Field<long>(solver, "stepCoupledWork");
            long spent = 0;
            while (spent < expectedWork)
            {
                int slice = (int)Math.Min(budget, expectedWork - spent);
                Assert.False(solver.AdvanceStep(slice));
                Assert.Equal((long)slice, solver.LastAdvanceWork);
                spent += solver.LastAdvanceWork;
            }
            Assert.Equal("Apply", Stage(solver));
            Assert.Equal(expectedWatts, Field<float[]>(solver, "nodeWatts"));
            for (int l = 0; l < 2; l++)
            {
                Assert.Equal(expectedParcels[l], solver.Loops[l].SegmentWatts);
                Assert.Equal(absorbed[l], solver.Loops[l].AbsorbedEnergy);
                Assert.Equal(rejected[l], solver.Loops[l].RejectedEnergy);
                for (int p = 0; p < startingParcels[l].Length; p++)
                    Assert.Equal(startingParcels[l][p], solver.Loops[l].ParcelTemperature(p));
                Assert.Equal(roomWatts[l], Field<float[]>(solver, "roomWatts")[l]);
                Assert.Equal(310f + l * 100f, solver.RoomAir[l].Temperature);
            }
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(roomDiagnostics[i], solver.Nodes[i].LastRoomWatts);
                Assert.Equal(250f + 317f * i, solver.Nodes[i].Temperature);
            }
        }

        private static void FrozenRooms(ThermalSolver solver, float h, float[] watts,
            float[] roomWatts, float[] diagnostics)
        {
            var temperatures = Field<float[]>(solver, "nodeTemperatures");
            var masses = Field<float[]>(solver, "nodeThermalMass");
            var relaxation = Field<float[]>(solver, "nodeRelaxation");
            for (int r = 0; r < solver.RoomAir.Count; r++)
            {
                var air = solver.RoomAir[r];
                float roomRelaxation = 1f;
                if (solver.ConductionClampLive && h > 0f)
                {
                    float total = 0f;
                    foreach (var link in air.Links) total += link.Conductance;
                    if (air.ThermalMass > 0f && total > 0f)
                    {
                        float stable = air.ThermalMass / (h * total);
                        if (stable < 1f) roomRelaxation = stable;
                    }
                }
                foreach (var link in air.Links)
                {
                    float difference = air.Temperature - temperatures[link.NodeIndex];
                    float exchange = link.Conductance * difference;
                    if (solver.ConductionClampLive)
                    {
                        float combined = air.ThermalMass + masses[link.NodeIndex];
                        if (h > 0f && combined > 0f)
                        {
                            float maxEnergy = difference * (air.ThermalMass * masses[link.NodeIndex] / combined);
                            if (Math.Abs(exchange * h) > Math.Abs(maxEnergy)) exchange = maxEnergy / h;
                        }
                        float scale = roomRelaxation;
                        if (relaxation[link.NodeIndex] < scale) scale = relaxation[link.NodeIndex];
                        if (scale < 1f) exchange *= scale;
                    }
                    watts[link.NodeIndex] += exchange;
                    roomWatts[r] -= exchange;
                    diagnostics[link.NodeIndex] += exchange;
                }
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void EnvironmentSlicesResetConductionDiagnosticsBeforeTheirFrozenContactSum(
            bool generationOnly, bool fuseWattsClear)
        {
            var simulation = CoreHotPathTests.FourBlocks(new ThermalSettings {
                EnableEnvironment = !generationOnly, EnableWasteHeat = false, EnableSolarHeat = false,
                EnableDamage = false, EnableRoomAir = false, EnableCoolantLoops = false,
                ClampConductionOvershoot = false, MaxSubsteps = 1, MaxSubstepsPerBlock = 0 });
            var solver = simulation.Solver;
            solver.FuseWattsClear = fuseWattsClear;
            solver.CollectDiagnostics = true;
            solver.Nodes[0].SetExposedFaces(new int[6]); // Cover the buried branch as well.
            for (int i = 0; i < 4; i++) solver.Nodes[i].LastConductionWatts = 999f;
            solver.BeginStep(.0001f, Environment(simulation));
            for (int i = 0; i < 4; i++)
            {
                Assert.False(solver.AdvanceStep(1));
                Assert.Equal(0f, solver.Nodes[i].LastConductionWatts);
                if (i + 1 < 4) Assert.Equal(999f, solver.Nodes[i + 1].LastConductionWatts);
            }
            Assert.Equal("Conduction", Stage(solver));
            var a = Field<int[]>(solver, "linkA");
            var b = Field<int[]>(solver, "linkB");
            var conductance = Field<float[]>(solver, "linkConductance");
            var temperatures = Field<float[]>(solver, "nodeTemperatures");
            var expected = new float[4];
            Assert.True(solver.LinkCount > 0);
            for (int i = 0; i < solver.LinkCount; i++)
            {
                float exchange = conductance[i] * (temperatures[b[i]] - temperatures[a[i]]);
                expected[a[i]] += exchange;
                expected[b[i]] -= exchange;
            }
            Assert.False(solver.AdvanceStep(solver.LinkCount));
            for (int i = 0; i < 4; i++) Assert.Equal(expected[i], solver.Nodes[i].LastConductionWatts);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void DeviceReplacementAndPressureChangesInvalidateSuspendedContactCursors(int change)
        {
            var simulation = Build(false, true, true);
            var solver = simulation.Solver;
            solver.BeginStep(.1f, Environment(simulation));
            ReachCoupled(solver);
            string target = change == 0 ? "ParcelConductance" : change == 2 ? "Pumps" : "RoomContacts";
            int slices = 0;
            while (Field<object>(solver, "coupledStage").ToString() != target)
            {
                Assert.False(solver.AdvanceStep(1));
                Assert.True(++slices < 100);
            }
            if (change == 0) solver.SetLoops(null);
            else if (change == 1) solver.RebuildRoomAir(RoomMap.AllExternal);
            else if (change == 2) solver.RebuildHeatPumps();
            else
            {
                var map = new RoomMap();
                map.SetSearchBounds(Vector3I.Zero, Vector3I.One);
                map.AddToRoom(map.BeginRoom(), Vector3I.Zero);
                map.DropEmptyRooms();
                Assert.False(solver.SetRoomPressure(map, Vector3I.Zero, float.NaN));
                Assert.False(solver.SetRoomPressure(map, Vector3I.Zero, float.PositiveInfinity));
                Assert.True(solver.StepInFlight);
                Assert.False(float.IsNaN(solver.RoomAir[0].ThermalMass));
                Assert.True(solver.SetRoomPressure(map, Vector3I.Zero, 0f));
            }
            Assert.False(solver.StepInFlight);
            Assert.Equal(0L, solver.StepWorkRemaining);
            Assert.True(solver.AdvanceStep(1));
            Assert.Equal(0L, solver.LastAdvanceWork);
            solver.BeginStep(.1f, Environment(simulation));
            slices = 0;
            while (!solver.AdvanceStep(1)) Assert.True(++slices < 1000);
            Assert.Equal(1L, solver.StepCount);
        }

        [Fact]
        public void AppendedNodesWaitForTheNextStepInBothCoupledPaths()
        {
            foreach (long budget in new[] { 1L, long.MaxValue })
            {
                var simulation = Build(false, true, true);
                var solver = simulation.Solver;
                solver.BeginStep(.1f, Environment(simulation));
                ReachCoupled(solver);
                var added = simulation.AddBlock(new BlockInstance(simulation.Grid.Blocks[0].Model,
                    new Vector3I(10, 0, 0), BlockOrientation.Identity), 500f);
                added.LastRoomWatts = 999f;
                added.LastConductionWatts = 888f;
                Assert.True(solver.StepInFlight);
                Assert.False(float.IsInfinity(solver.RequiredSubsteps(.1f)));
                long remaining = solver.StepWorkRemaining, spent = 0;
                int advances = 0;
                bool done;
                do
                {
                    done = solver.AdvanceStep(budget);
                    spent += solver.LastAdvanceWork;
                    Assert.True(++advances < 1000);
                } while (!done);
                Assert.Equal(remaining, spent);
                Assert.Equal(500f, added.Temperature);
                Assert.Equal(999f, added.LastRoomWatts);
                Assert.Equal(888f, added.LastConductionWatts);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DiagnosticTogglesTakeEffectOnTheNextStepWithoutChangingInFlightWork(bool initial)
        {
            var control = Build(false, true, true);
            var spread = Build(false, true, true);
            var environment = Environment(control);
            spread.Solver.CollectDiagnostics = initial;
            for (int step = 0; step < 2; step++)
            {
                bool expectedDiagnostics = step == 0 ? initial : !initial;
                control.Solver.CollectDiagnostics = expectedDiagnostics;
                for (int i = 0; i < 4; i++)
                {
                    control.Solver.Nodes[i].LastRoomWatts = spread.Solver.Nodes[i].LastRoomWatts = 999f;
                    control.Solver.Nodes[i].LastConductionWatts = spread.Solver.Nodes[i].LastConductionWatts = 888f;
                }
                control.Solver.Step(.1f, environment);
                spread.Solver.BeginStep(.1f, environment);
                long estimated = spread.Solver.StepWorkUnits, spent = 0;
                spread.Solver.CollectDiagnostics = !initial;
                long budget = step == 0 && initial ? long.MaxValue : 1L;
                int advances = 0;
                bool done;
                do
                {
                    done = spread.Solver.AdvanceStep(budget);
                    Assert.InRange(spread.Solver.LastAdvanceWork, 0L, budget);
                    spent += spread.Solver.LastAdvanceWork;
                    Assert.True(++advances < 1000);
                } while (!done);
                Assert.Equal(estimated, spent);
                for (int i = 0; i < 4; i++)
                {
                    Assert.Equal(control.Solver.Nodes[i].Temperature, spread.Solver.Nodes[i].Temperature);
                    Assert.Equal(control.Solver.Nodes[i].LastRoomWatts, spread.Solver.Nodes[i].LastRoomWatts);
                    Assert.Equal(control.Solver.Nodes[i].LastConductionWatts,
                        spread.Solver.Nodes[i].LastConductionWatts);
                    if (!expectedDiagnostics) Assert.Equal(999f, spread.Solver.Nodes[i].LastRoomWatts);
                }
                if (expectedDiagnostics) Assert.NotEqual(999f, spread.Solver.Nodes[0].LastRoomWatts);
            }
        }

        [Fact]
        public void DemandQueriesDuringAPausedSubstepDoNotResyncOrReplaceItsEnvironment()
        {
            var expected = Build(false, true, true);
            var observed = Build(false, true, true);
            var environment = Environment(expected);
            expected.Solver.Step(.1f, environment);
            var solver = observed.Solver;
            solver.BeginStep(.1f, environment);
            Assert.Equal(3, solver.LastSubsteps);
            int advances = 0;
            while (Field<int>(solver, "substepsLeft") != 2 || Stage(solver) != "Begin")
            {
                Assert.False(solver.AdvanceStep(1));
                Assert.True(++advances < 200);
            }
            var scratch = (float[])Field<float[]>(solver, "nodeTemperatures").Clone();
            Assert.NotEqual(solver.Nodes[0].Temperature, scratch[0]);
            var alternate = environment;
            alternate.AtmosphereFactor = 1f;
            alternate.ConvectionCoefficient = 12345f;
            Assert.True(solver.RequiredSubsteps(.1f) > 0f);
            Assert.True(solver.RequiredSubsteps(.1f, alternate) > 0f);
            Assert.Equal(scratch, Field<float[]>(solver, "nodeTemperatures"));
            Assert.Equal(environment, solver.Environment);
            Assert.Equal(environment, Field<EnvironmentState>(solver, "stepEnvironment"));
            advances = 0;
            while (!solver.AdvanceStep(1)) Assert.True(++advances < 400);
            for (int i = 0; i < 4; i++)
                Assert.Equal(expected.Solver.Nodes[i].Temperature, solver.Nodes[i].Temperature);
            for (int l = 0; l < 2; l++)
            {
                for (int p = 0; p < 4; p++)
                    Assert.Equal(expected.Solver.Loops[l].SegmentTemperature(p), solver.Loops[l].SegmentTemperature(p));
                Assert.Equal(expected.Solver.RoomAir[l].Temperature, solver.RoomAir[l].Temperature);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TemperatureResetOrRoomRestoreCancelsPendingContactWork(bool restoreRoom)
        {
            var simulation = Build(false, true, true);
            var solver = simulation.Solver;
            solver.BeginStep(.1f, Environment(simulation));
            ReachCoupled(solver);
            int advances = 0;
            while (Field<object>(solver, "coupledStage").ToString() != "RoomContacts")
            {
                Assert.False(solver.AdvanceStep(1));
                Assert.True(++advances < 100);
            }
            if (restoreRoom)
            {
                Assert.Equal(1, solver.RestoreRoomAir(new[] { new StoredRoom(Vector3I.Zero, 457f) }));
                Assert.Equal(457f, solver.RoomAir[0].Temperature);
            }
            else
            {
                solver.SetAllTemperatures(457f);
                for (int i = 0; i < 4; i++) Assert.Equal(457f, solver.Nodes[i].Temperature);
            }
            Assert.False(solver.StepInFlight);
            Assert.True(solver.AdvanceStep(1));
            Assert.Equal(0L, solver.LastAdvanceWork);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 5)]
        public void TinySlicesStayIdenticalAcrossSubstepsDevicesDisabledAndAbandonment(bool mixed, int budget)
        {
            var whole = Build(mixed, true, true);
            var spread = Build(mixed, true, true);
            var environment = Environment(whole);
            spread.Solver.BeginStep(.1f, environment);
            ReachCoupled(spread.Solver);
            spread.Solver.AdvanceStep(7); // Pause inside the parcel/contact reduction, then discard it.
            spread.Solver.AbandonStep();
            for (int step = 0; step < 3; step++)
            {
                whole.Settings.EnableCoolantLoops = spread.Settings.EnableCoolantLoops = step != 1;
                whole.Settings.EnableRoomAir = spread.Settings.EnableRoomAir = step != 1;
                whole.Settings.EnableHeatPumps = spread.Settings.EnableHeatPumps = step != 1;
                whole.Solver.Step(.1f, environment);
                spread.Solver.BeginStep(.1f, environment);
                long estimate = spread.Solver.StepWorkUnits, spent = 0;
                int advances = 0;
                bool done;
                do
                {
                    done = spread.Solver.AdvanceStep(budget);
                    Assert.InRange(spread.Solver.LastAdvanceWork, 0L, (long)budget);
                    spent += spread.Solver.LastAdvanceWork;
                    Assert.True(++advances < 1000);
                } while (!done);
                Assert.Equal(estimate, spent);
                for (int i = 0; i < 4; i++)
                {
                    Assert.Equal(whole.Solver.Nodes[i].Temperature, spread.Solver.Nodes[i].Temperature);
                    Assert.Equal(whole.Solver.Nodes[i].LastRoomWatts, spread.Solver.Nodes[i].LastRoomWatts);
                }
                for (int l = 0; l < 2; l++)
                {
                    var a = whole.Solver.Loops[l]; var b = spread.Solver.Loops[l];
                    for (int p = 0; p < 4; p++) Assert.Equal(a.SegmentTemperature(p), b.SegmentTemperature(p));
                    Assert.Equal(a.LastWattsAbsorbed, b.LastWattsAbsorbed);
                    Assert.Equal(a.LastWattsRejected, b.LastWattsRejected);
                    Assert.Equal(whole.Solver.RoomAir[l].Temperature, spread.Solver.RoomAir[l].Temperature);
                    var pa = whole.Solver.HeatPumps[l]; var pb = spread.Solver.HeatPumps[l];
                    Assert.Equal(pa.LastLiftedWatts, pb.LastLiftedWatts);
                    Assert.Equal(pa.LastRejectedWatts, pb.LastRejectedWatts);
                    Assert.Equal(pa.LastPowerWatts, pb.LastPowerWatts);
                    Assert.Equal(pa.LastDemandWatts, pb.LastDemandWatts);
                }
            }
        }
    }
}
