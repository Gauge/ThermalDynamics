using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What happens when a grid outgrows its buffers with a step part way through.
    ///
    /// <para>
    /// A step is spread over the frames of its window, and the node arrays it integrates over are
    /// grown when the node count passes their capacity. Growing reallocates them — every one, the
    /// temperatures and the mirrored heat capacities included — and refills them at the next
    /// <c>SyncNodeState</c>, which does not run until the next step begins. A step in flight then
    /// carries on over zeroed rows and divides watts by a heat capacity of zero.
    /// </para>
    ///
    /// <para>
    /// It takes a block placed on a frame the step is not finished with, on a grid whose buffers
    /// have no headroom left — welding onto a ship, in other words.
    /// </para>
    /// </summary>
    public class BufferGrowthTests
    {

        private static ThermalSimulation Ship(int side)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                MaxSubsteps = 64,
                MaxSubstepsPerBlock = 0,
                MaxElementVisitsPerStep = 0,
            };
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(side, side, side));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());
            return simulation;
        }


        private static void AssertFinite(ThermalSimulation simulation, string when)
        {
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float t = simulation.Solver.Nodes[i].Temperature;
                Assert.False(float.IsNaN(t) || float.IsInfinity(t),
                    "node " + i + " of " + simulation.Solver.Nodes.Count + " is " + t + " " + when);
            }
        }

        [Fact]

        public void WeldingPastTheBufferCapacityDoesNotProduceNaN()
        {

            ThermalSimulation simulation = Ship(3);

            simulation.Update(1f / 60f, Worlds.Shadow());
            Assert.True(simulation.StepInFlight);

            for (int x = 3; x < 9; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        simulation.AddBlock(new BlockInstance(Catalog.HeavyArmor(),

                            new Vector3I(x, y, z), BlockOrientation.Identity), 293.15f);
                    }
                }
            }

            for (int frame = 0; frame < 120; frame++)
            {
                simulation.Update(1f / 60f, Worlds.Shadow());
                AssertFinite(simulation, "on frame " + frame);
            }
        }

        [Fact]

        public void TheGridKeepsItsTemperaturesAcrossTheGrowth()
        {

            ThermalSimulation simulation = Ship(3);
            simulation.Update(1f / 60f, Worlds.Shadow());

            float before = simulation.Solver.Nodes[0].Temperature;

            for (int x = 3; x < 9; x++)
            {
                simulation.AddBlock(new BlockInstance(Catalog.HeavyArmor(),

                    new Vector3I(x, 0, 0), BlockOrientation.Identity), 293.15f);
            }

            for (int frame = 0; frame < 4; frame++)
            {
                simulation.Update(1f / 60f, Worlds.Shadow());
            }

            float after = simulation.Solver.Nodes[0].Temperature;
            Assert.InRange(after, before - 5f, before + 5f);
        }
    }
}
