using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Where the air acts on a hull, read from the friction row, against its centre of mass.**
    ///
    /// <para>
    /// The mod applies no aerodynamic force since the `K25` strip, and the force its rebuild applies
    /// acts at the centre of pressure of the hull's surface profile (aerodynamics.md, `AR5`). What
    /// the solver can already say is where the friction watts concentrate, which `AeroOverlay` draws
    /// as the heating centre. These pin that centre's geometry: only its offset *across* the flow
    /// can make a torque, and a fin on one flank moves it a measured distance — the baseline the
    /// profile's centre of pressure will be compared against.
    /// </para>
    /// </summary>
    public class CentreOfPressureTests
    {
        private readonly ITestOutputHelper output;


        public CentreOfPressureTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();
            return settings;
        }


        private static Vector3 CentreOfPressure(ThermalSimulation simulation)
        {
            Vector3 weighted = Vector3.Zero;
            float total = 0f;

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = simulation.Solver.Nodes[i];
                float weight = node.LastFrictionWatts;
                if (weight <= 0f) continue;

                Vector3 centre = (Vector3)node.Block.Min
                    + ((Vector3)(node.Block.MaxExclusive - node.Block.Min)) * 0.5f;

                weighted += centre * weight;
                total += weight;
            }

            return total > 0f ? weighted / total : Vector3.Zero;
        }


        private static Vector3 Centroid(ThermalSimulation simulation)
        {
            Vector3 sum = Vector3.Zero;
            int count = simulation.Solver.Nodes.Count;

            for (int i = 0; i < count; i++)
            {
                BlockInstance block = simulation.Solver.Nodes[i].Block;
                sum += (Vector3)block.Min + ((Vector3)(block.MaxExclusive - block.Min)) * 0.5f;
            }

            return count > 0 ? sum / count : Vector3.Zero;
        }


        private static ThermalSimulation Run(GridBuilder builder)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(1, Worlds.Flight(1f, 120f));
            return simulation;
        }

        [Fact]

        public void OnlyTheOffsetAcrossTheFlowCanMakeATorque()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));


            ThermalSimulation simulation = Run(builder);


            Vector3 pressure = CentreOfPressure(simulation);

            Vector3 mass = Centroid(simulation);
            Vector3 separation = pressure - mass;

            output.WriteLine("cube: pressure {0}, mass {1}, along {2:0.00} cells, across {3:0.000}",
                pressure, mass, Math.Abs(separation.Z), Lateral(separation));

            Assert.True(Math.Abs(separation.Z) > 1f,
                "the cube's pressure is not on its windward face, so the weighting is not windward");

            Assert.True(Lateral(separation) < 0.01f,
                "a hull symmetric about the flow has a lateral offset, so it would weathervane and "
                + "should not");
        }


        private static float Lateral(Vector3 separation)
        {
            return (float)Math.Sqrt(separation.X * separation.X + separation.Y * separation.Y);
        }

        [Fact]

        public void AFinnedHullHasALateralArmAndThisIsHowLong()
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 12));
            builder.Fill(Catalog.HeavyArmor(), new Vector3I(3, 0, 2), new Vector3I(9, 3, 5));


            ThermalSimulation simulation = Run(builder);


            Vector3 pressure = CentreOfPressure(simulation);

            Vector3 mass = Centroid(simulation);
            Vector3 separation = pressure - mass;

            float lateral = Lateral(separation);

            output.WriteLine("finned: pressure {0}, mass {1}, lateral arm {2:0.00} cells ({3:0.0} m)",
                pressure, mass, lateral, lateral * 2.5f);

            Assert.True(lateral > 0.1f,
                "the fin moved the pressure nowhere across the flow, so this shape proves nothing "
                + "and the arm is " + lateral);
        }
    }
}
