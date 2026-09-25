using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The total conductance into a coolant loop and into a room's air is summed once per rebuild
    /// rather than once per caller, and a cache is only worth having if it is invalidated
    /// correctly.
    ///
    /// <para>
    /// The reason it exists: both totals are read by the stability estimate and by the thermal
    /// mass floor, and both of those run twice in a step — once through
    /// <c>AffordableStepSeconds</c> asking what the step can afford, and once inside
    /// <c>BeginStep</c> taking it. So a step walked every room link four times to re-sum a number
    /// that only changes when the graph is rebuilt. On a 126,731-block hull that is 139,120 links
    /// walked four times before the first substep runs, for four identical answers.
    /// </para>
    ///
    /// <para>
    /// The failure mode a cache introduces is silence: a stale total makes the stability estimate
    /// wrong, which makes the step too long, which the overshoot clamps then absorb — so nothing
    /// throws and nothing looks wrong, the grid simply integrates worse. These pin the four paths
    /// that change what the totals are summing.
    /// </para>
    /// </summary>
    public class CoupledConductanceCacheTests
    {

        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static ThermalSimulation Sealed()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        if (x == 1 && y == 1 && z == 1) continue;
                        builder.Place(armour, new Vector3I(x, y, z));
                    }
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 300f);
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void PressurisingARoomIsSeenByTheStabilityEstimate()
        {

            ThermalSimulation simulation = Sealed();

            simulation.StepExact(4, Worlds.Shadow());
            float before = simulation.Solver.ProfileSubsteps().WorstRoomAirDemand;

            Assert.True(simulation.SetRoomPressure(new Vector3I(1, 1, 1), 1f),
                "the middle cell is not a room the mapper found, so nothing was pressurised");

            float after = simulation.Solver.ProfileSubsteps().WorstRoomAirDemand;

            Assert.Equal(0f, before);
            Assert.True(after > 0f,
                "air was added to a sealed room and the room's own demand stayed at zero, so the"
                + " cached room conductance is stale");
        }

        [Fact]

        public void DepressurisingARoomIsSeenToo()
        {

            ThermalSimulation simulation = Sealed();
            simulation.SetRoomPressure(new Vector3I(1, 1, 1), 1f);
            simulation.StepExact(4, Worlds.Shadow());

            float wet = simulation.Solver.ProfileSubsteps().WorstRoomAirDemand;
            Assert.True(wet > 0f, "the room never held air to begin with");

            simulation.SetRoomPressure(new Vector3I(1, 1, 1), 0f);
            float dry = simulation.Solver.ProfileSubsteps().WorstRoomAirDemand;

            Assert.True(dry < wet,
                "the room was emptied and its demand stayed at " + wet
                + ", so the cached room conductance is stale");
        }

        [Fact]

        public void TheProfileAndTheEstimateReadTheSameTotals()
        {

            ThermalSimulation simulation = Sealed();
            simulation.SetRoomPressure(new Vector3I(1, 1, 1), 1f);
            simulation.StepExact(4, Worlds.Shadow());

            ThermalSolver.SubstepProfile profile = simulation.Solver.ProfileSubsteps();

            Assert.True(profile.WorstRoomAirDemand > 0f,
                "the profile sees no room air on a hull that has some");
            Assert.Equal(
                simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds),
                profile.RequiredSubstepsInForce,
                3);
        }

        [Fact]

        public void ReplacingTheLoopsDoesNotLeaveTotalsBehind()
        {

            ThermalSimulation simulation = Sealed();
            simulation.StepExact(2, Worlds.Shadow());

            simulation.Solver.SetLoops(new List<CoolantLoop>());

            float estimate = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);
            Assert.True(estimate > 0f && !float.IsNaN(estimate) && !float.IsInfinity(estimate));
            Assert.Empty(simulation.Solver.Loops);
        }

        [Fact]

        public void GrowingTheGridDoesNotLeaveTotalsBehind()
        {

            ThermalSimulation simulation = Sealed();
            simulation.SetRoomPressure(new Vector3I(1, 1, 1), 1f);
            simulation.StepExact(4, Worlds.Shadow());

            float before = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);

            for (int x = 0; x < 3; x++)
            {
                simulation.AddBlock(new BlockInstance(
                    Catalog.LightArmor(), new Vector3I(x, 0, 3), BlockOrientation.Identity), 300f);
            }

            simulation.StepExact(2, Worlds.Shadow());
            float after = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);

            Assert.True(after > 0f && !float.IsNaN(after));
            Assert.True(Math.Abs(after - before) < before,
                "welding three armour blocks moved the estimate from " + before + " to " + after);
        }
    }
}
