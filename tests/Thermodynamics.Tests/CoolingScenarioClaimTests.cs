using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The claims the cooling scenarios make in their summary lines, so a headline conclusion cannot
    /// quietly invert. Same discipline as <see cref="ScenarioClaimTests"/>, kept separate because
    /// these seven were written together against one field telemetry dump.
    ///
    /// Every one of them exists because the dump could not answer the question: it showed a ship
    /// carrying 288 pipe blocks, four pumps, eight radiators and four heat pumps, and could not say
    /// whether any of it moved a watt.
    /// </summary>
    [Trait("speed", "slow")]
    public class CoolingScenarioClaimTests
    {
        [Fact]

        public void TheWholeCoolingPlantBeatsTheSameShipWithoutIt()
        {
            ScenarioResult result = Scenarios.Run("cooling-plant");
            ThermalSimulation simulation = result.Runner.Simulation;

            Assert.Single(simulation.Solver.Loops);
            CoolantLoop loop = simulation.Solver.Loops[0];

            Assert.True(loop.LastWattsAbsorbed > 10000f,
                "the plant's loop draws only " + loop.LastWattsAbsorbed + " W");

            Assert.True(loop.LastWattsRejected > loop.LastWattsAbsorbed * 0.9f,
                "the loop draws " + loop.LastWattsAbsorbed + " W and sheds only " + loop.LastWattsRejected);

            IList<HeatPumpDevice> pumps = simulation.Solver.HeatPumps;
            Assert.NotEmpty(pumps);
            for (int i = 0; i < pumps.Count; i++)
            {
                Assert.True(pumps[i].IsConnected, "pump " + i + " is not bound to two blocks");
                Assert.True(pumps[i].LastLiftedWatts > 0f, "pump " + i + " lifted nothing");
            }

            Assert.Contains("with the plant", result.Summary);
        }

        [Fact]

        public void EveryWayARingCanFailIsNamedRatherThanCountedAsAbsent()
        {
            ScenarioResult result = Scenarios.Run("loop-faults");
            CoolantLoopDiagnostics diagnosis = result.Runner.Simulation.DiagnoseLoops();

            Assert.Equal(2, diagnosis.Loops);
            Assert.True(diagnosis.PipesAdrift > 0, "the scenario is supposed to contain broken plumbing");

            int kinds = 0;
            for (int i = 1; i < diagnosis.Counts.Length; i++)
            {
                if (diagnosis.Counts[i] > 0) kinds++;
            }
            Assert.True(kinds >= 2, "only " + kinds + " kinds of fault were distinguished");

            Assert.True(diagnosis.CountOf(CoolantFault.BlockedByNonCoolant) > 0);
            Assert.True(diagnosis.CountOf(CoolantFault.OpenEnd) > 0);
        }

        [Fact]

        public void ALoopWithNoSinkFaceStillLooksHealthy()
        {
            ScenarioResult result = Scenarios.Run("loop-dry");
            ThermalSimulation simulation = result.Runner.Simulation;

            CoolantLoop loop = simulation.Solver.Loops[0];

            Assert.Single(simulation.Solver.Loops);
            Assert.True(loop.HasPump);
            Assert.True(loop.Temperature > 400f, "the coolant should be hot: " + loop.Temperature);

            Assert.Equal(loop.PipeCount, loop.Links.Count);
        }

        [Fact]

        public void APumpInstalledBackwardsMakesThingsWorse()
        {
            string summary = Scenarios.Run("heatpump-backwards").Summary;

            float correct = ScenarioClaimTests.ExtractCelsius(summary, 0);
            float backwards = ScenarioClaimTests.ExtractCelsius(summary, 1);

            Assert.True(backwards > correct,
                "the pump turned around left the reactor at " + backwards
                + " C against " + correct + " C the right way round");
        }

        [Fact]

        public void TheCoefficientFallsAsTheGapWidensAndTheRatingBindsOnlyWhenItIsNarrow()
        {
            string summary = Scenarios.Run("heatpump-limits").Summary;

            Assert.Contains("gap 10 K", summary);
            Assert.Contains("(rating)", summary);
            Assert.Contains("gap 910 K", summary);

            int narrow = summary.IndexOf("gap 10 K");
            int widest = summary.IndexOf("gap 910 K");
            Assert.Contains("(rating)", summary.Substring(narrow, summary.IndexOf(';', narrow) - narrow));
            Assert.Contains("(Carnot)", summary.Substring(widest));
        }

        [Fact]

        public void PastItsHeadroomThePlantOverheatsRatherThanHolding()
        {
            ScenarioResult result = Scenarios.Run("cooling-runaway");
            string summary = result.Summary;

            float light = ScenarioClaimTests.ExtractCelsius(summary, 0);
            float medium = ScenarioClaimTests.ExtractCelsius(summary, 2);

            Assert.True(medium > light,
                "four times the load should be hotter: " + light + " C then " + medium + " C");

            string[] loads = summary.Split(';');
            Assert.Equal(3, loads.Length);

            Assert.Contains("0 over critical", loads[0]);
            Assert.Contains("0 over critical", loads[1]);
            Assert.DoesNotContain("0 over critical", loads[2]);
            Assert.Contains("over critical", loads[2]);
        }

        [Fact]

        public void ALongRingCostsNoMorePerParcelThanAShortOne()
        {
            ScenarioResult result = Scenarios.Run("loop-stiffness");
            string summary = result.Summary;

            Assert.Contains("8 pipes", summary);
            Assert.Contains("76 pipes", summary);

            Assert.DoesNotContain("energy x0.9", summary);
            Assert.DoesNotContain("energy x1.1", summary);

            ThermalSimulation simulation = result.Runner.Simulation;
            CoolantLoop loop = simulation.Solver.Loops[0];

            float conductance = 0f;
            for (int i = 0; i < loop.Links.Count; i++) conductance += loop.Links[i].Conductance;
            float perParcel = conductance / loop.PipeCount;

            Assert.True(loop.SegmentThermalMass / perParcel > simulation.Settings.StepSeconds,
                "a parcel's time constant " + (loop.SegmentThermalMass / perParcel)
                + " s is shorter than the " + simulation.Settings.StepSeconds
                + " s step, so a long ring is stiff again");

            float parcelsPerSubstep = loop.FlowSegmentsPerSecond
                * (simulation.Settings.StepSeconds / Math.Max(1, simulation.Solver.LastSubsteps));

            Assert.True(parcelsPerSubstep <= 1f,
                "the flow moves " + parcelsPerSubstep + " parcels per substep, past the upwind limit");
        }
    
        [Fact]

        public void SplittingARingBuysNothingButSpreadingTheSourcesDoes()
        {
            string summary = Scenarios.Run("loop-layout").Summary;

            float bunched = ScenarioClaimTests.ExtractCelsius(summary, 0);
            float spread = ScenarioClaimTests.ExtractCelsius(summary, 1);
            float split = ScenarioClaimTests.ExtractCelsius(summary, 2);

            Assert.True(bunched - spread > 20f,
                "spreading the sources should be worth a lot: " + bunched + " C then " + spread + " C");

            Assert.True(Math.Abs(spread - split) < 5f,

                "one ring with spread sources (" + spread + " C) and four rings (" + split
                + " C) should be within a few kelvin; splitting is not what helps");
        }
    
        [Fact]

        public void AHeatPumpOnAWallCoolsTheRoomBehindIt()
        {
            ScenarioResult result = Scenarios.Run("air-conditioning");
            string summary = result.Summary;

            float off = ScenarioClaimTests.ExtractCelsius(summary, 0);
            float on = ScenarioClaimTests.ExtractCelsius(summary, 1);

            Assert.True(off - on > 20f,
                "the pump should make a real difference to the room: " + off + " C then " + on + " C");

            ThermalSimulation simulation = result.Runner.Simulation;
            IList<HeatPumpDevice> pumps = simulation.Solver.HeatPumps;

            Assert.NotEmpty(pumps);
            Assert.True(pumps[0].IsConnected);
            Assert.InRange(pumps[0].ColdNodeIndex, 0, simulation.Solver.Nodes.Count - 1);

            RoomAirNode air = simulation.Solver.GetRoomAir(
                simulation.Rooms.Map, new VRageMath.Vector3I(2, 1, 1));

            Assert.NotNull(air);
            Assert.True(air.HasAir, "an unpressurised room cannot be air-conditioned");
            Assert.NotEmpty(air.Links);
        }
    }
}
