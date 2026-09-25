using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The game had two conduction paces, and the second one is gone.**
    ///
    /// <para>
    /// <see cref="ThermalConstants.ConductionScale"/> sets the pace for solid conduction — block to
    /// block, and block to a bolted panel. The coolant loop's fluid coupling used to have its own,
    /// a 0…1 quality against a reference conductivity of 200 W/(m·K), and nothing in the code made
    /// the two agree: the ratio between them is what decides whether plumbing beats bolting
    /// It is a heat transfer coefficient in W/(m²·K)
    /// now — what the transfer physically is — so there is one pace and one dial rather than two
    /// that had to be moved together.
    /// </para>
    ///
    /// <para>
    /// **Measured, on the retune `C12` asks for.** Raising the solid pace ×4 and leaving the loop's
    /// alone took a coolant sink from buying **195.3 K** against the best surface dial's 41.5 K to
    /// buying **73.3 K** against 135.3 K — the mod's own headline finding, *the radiator is a block
    /// you plumb*, inverted, and nothing said so except four balance tests failing for what read
    /// like unrelated reasons. Moving the loop's pace with it recovered 73.3 K to 108.2 K and did
    /// not restore the ordering, which is a separate finding and is
    /// balance.md's.
    /// </para>
    ///
    /// <para>
    /// So what is left to hold is the solid pace itself and the coefficient the loop shipped at,
    /// both of which every cooling figure in balance.md was measured
    /// against. The failure they prevent is the same one: a balance change nobody chose, in a
    /// mechanism nobody was editing.
    /// </para>
    /// </summary>
    public class ConductionPaceTests
    {
        private readonly ITestOutputHelper output;


        public ConductionPaceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const float ShippedCoefficient = 1000f;

        private const float StoppedCoefficient = 160f;

        [Fact]

        public void AStoppedRingStillCarriesTheOldCoefficient()
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();

            Assert.Equal(StoppedCoefficient,
                properties.HeatTransferCoefficient * properties.StagnantTransferFraction, 1);
        }

        [Fact]

        public void TheFluidCouplingIsStillWhatTheCoolingFiguresWereMeasuredAt()
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();

            Assert.True(
                System.Math.Abs(properties.HeatTransferCoefficient - ShippedCoefficient)
                    < 0.001f * ShippedCoefficient,
                "the fluid couples at " + properties.HeatTransferCoefficient + " W/(m2 K) against"
                + " the " + ShippedCoefficient + " every cooling figure in balance.md was measured"
                + " at. Moving it is a balance decision and wants those figures re-derived with it.");
        }

        [Fact]

        public void TheSameFluidCouplesTheSameWhateverSizeTheGridIs()
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();


            GridModel large = new GridModel(2.5f);

            GridModel small = new GridModel(0.5f);

            float perAreaLarge = CoolantLoopBuilder.PlateConductance(large, properties) / large.CellFaceArea;
            float perAreaSmall = CoolantLoopBuilder.PlateConductance(small, properties) / small.CellFaceArea;

            Assert.Equal(perAreaLarge, perAreaSmall, 3);
            Assert.Equal(ShippedCoefficient, perAreaLarge, 3);
        }

        [Fact]

        public void TheShippedSolidPaceIsFourTimesWhatTheConversionCalibratedTo()
        {
            const float Calibrated = 2.4f;

            Assert.Equal(9.6f, ThermalConstants.ConductionScale, 4);
            Assert.Equal(4f, ThermalConstants.ConductionScale / Calibrated, 4);
        }

        [Fact]

        public void ASmallGridLoopCouplesLessHardThanItUsedTo()
        {

            float now = Settle(160f);

            float before = Settle(800f);

            output.WriteLine("small-grid ring source: {0:n1} K at 160 W/(m2 K), {1:n1} K at the 800"
                + " the old form implied", now, before);

            Assert.True(now > before,
                "the source settled at " + now + " K on the corrected coupling against " + before
                + " K on the old one, so the correction did not reach the rig");

            Assert.True(now - before > 1f,
                "the two couplings are " + (now - before) + " K apart, which is not a measurement");
        }


        private static float Settle(float coefficient)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Small();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 4, 4);
            PipeFitter.BuildRing(builder, cells, -1, sinks);

            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(20000f);
            BlockInstance source = builder.Last;


            ThermalSimulation simulation = new ThermalSimulation(settings, builder.Grid);

            LoopThermalProperties properties = LoopThermalProperties.Default();
            properties.HeatTransferCoefficient = coefficient;
            simulation.LoopProperties = properties;

            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            simulation.RebuildAll();

            Assert.NotEmpty(simulation.Solver.Loops);
            Assert.Equal(coefficient, simulation.Solver.Loops[0].Properties.HeatTransferCoefficient, 3);

            simulation.StepExact(4000, Worlds.Shadow());
            return simulation.Solver.GetNode(source).Temperature;
        }
    }
}
