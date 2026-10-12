using Thermodynamics.Fixtures;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A blueprint's subgrids are one machine, and heat crosses between them.
    ///
    /// <para>
    /// backlog.md `F12` said the opposite — that "subgrids are read as
    /// separate ships" and "the lab does not reassemble them", so every corpus figure was taken on
    /// a hull the game would not have simulated that way. It is not true, and had not been for as
    /// long as `ShipAssembly` existed: 747 of the first 1,002 ships of the 2026-08-22 sweep hold
    /// more than one grid, 695 of them resolved mechanical joints, and 29,604 joints were built
    /// across them.
    /// </para>
    ///
    /// <para>
    /// What was missing is this: `CorpusSurvey` *counts* bridges and asserts the population resolves
    /// them, and nothing anywhere checked that a bridge actually **moves heat**. A count is not a
    /// mechanism, and a claim about the lab reassembling ships should not rest on one.
    /// </para>
    /// </summary>
    public class SubgridBridgeTests
    {

        private static ShipAssembly Pair(bool bridged, out ThermalNode hot, out ThermalNode cold)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder first = GridBuilder.Large();
            first.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            GridBuilder second = GridBuilder.Large();
            second.Place(Catalog.HeavyArmor(), Vector3I.Zero);


            ShipAssembly assembly = new ShipAssembly();
            ThermalSimulation a = first.BuildSimulation(settings, 800f);
            ThermalSimulation b = second.BuildSimulation(settings, 300f);

            assembly.Simulations.Add(a);
            assembly.Simulations.Add(b);

            hot = a.Solver.Nodes[0];
            cold = b.Solver.Nodes[0];

            if (bridged) assembly.Bridge2(a, hot, b, cold);
            return assembly;
        }

        [Fact]

        public void HeatCrossesAJointBetweenTwoGrids()
        {
            ThermalNode hot, cold;

            ShipAssembly assembly = Pair(true, out hot, out cold);

            Assert.Single(assembly.Bridges);

            float before = (hot.Temperature * hot.ThermalMass) + (cold.Temperature * cold.ThermalMass);

            for (int i = 0; i < 200; i++) assembly.Step(Worlds.Shadow(), 1f / 8f);

            Assert.True(cold.Temperature > 305f,
                "the cold grid should have warmed across the joint: " + cold.Temperature);
            Assert.True(hot.Temperature < 795f,
                "and the hot one cooled: " + hot.Temperature);

            float after = (hot.Temperature * hot.ThermalMass) + (cold.Temperature * cold.ThermalMass);
            Assert.Equal(before, after, before * 0.001f);
        }

        [Fact]

        public void NothingCrossesWithoutOne()
        {
            ThermalNode hot, cold;

            ShipAssembly assembly = Pair(false, out hot, out cold);

            Assert.Empty(assembly.Bridges);

            for (int i = 0; i < 200; i++) assembly.Step(Worlds.Shadow(), 1f / 8f);

            Assert.Equal(800f, hot.Temperature, 1);
            Assert.Equal(300f, cold.Temperature, 1);
        }

        [Fact]

        public void AJointCannotOvershootThePairItConnects()
        {
            ThermalNode hot, cold;

            ShipAssembly assembly = Pair(true, out hot, out cold);

            assembly.Step(Worlds.Shadow(), 60f);

            float low = Math.Min(300f, 800f);
            float high = Math.Max(300f, 800f);

            Assert.InRange(hot.Temperature, low, high);
            Assert.InRange(cold.Temperature, low, high);
        }
    }
}
