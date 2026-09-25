using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The transport package shipped, and these are what says so.**
    ///
    /// <para>
    /// <see cref="LoopBefore"/> was `LoopCandidate`, a proposal: forced convection while the ring is
    /// pumped, the old coefficient while it is stopped, and coolant sized from the cell instead of a
    /// flat 50 kg. All three are now the mod's own defaults — the first two at `C42`, the coolant's
    /// density at `C43` — so the class holds the **before** and these tests are the mirror image of
    /// the ones that used to say *none of it ships*.
    /// </para>
    ///
    /// <para>
    /// **The inversion is the point.** A candidate that quietly became the default would leave every
    /// lab with two identical arms, reporting *the package is worth nothing* in exactly the voice it
    /// would use if the package were worth nothing. That is this repository's recurring defect and it
    /// has already cost two findings, so the first test here pins that the two arms still differ.
    /// </para>
    /// </summary>
    public class LoopBeforeTests
    {

        private static ThermalSettings Isolated()
        {
            return Isolation.DeadWorld();
        }

        [Fact]

        public void TheBeforeArmIsNotWhatShips()
        {
            LoopThermalProperties shipped = LoopThermalProperties.Default();
            LoopThermalProperties before = LoopBefore.For(Catalog.LargeGridSize);

            Assert.NotEqual(before.HeatTransferCoefficient, shipped.HeatTransferCoefficient);
            Assert.NotEqual(before.StagnantTransferFraction, shipped.StagnantTransferFraction);

            Assert.NotEqual(
                before.MassPerPipe(Catalog.LargeGridSize),
                shipped.MassPerPipe(Catalog.LargeGridSize));

            Assert.NotEqual(
                before.MassPerPipe(Catalog.SmallGridSize),
                shipped.MassPerPipe(Catalog.SmallGridSize));
        }

        [Fact]

        public void AStoppedRingCarriesWhatEveryRingCarriedBeforeTheRetune()
        {
            LoopThermalProperties shipped = LoopThermalProperties.Default();

            float stopped = shipped.HeatTransferCoefficient * shipped.StagnantTransferFraction;

            Assert.Equal(LoopBefore.Coefficient, stopped, 1);
        }

        [Fact]

        public void TheFlatChargeIsTwoDensitiesTwoOrdersOfMagnitudeApart()
        {
            LoopThermalProperties before = LoopBefore.For(Catalog.LargeGridSize);

            float large = before.MassPerPipe(Catalog.LargeGridSize) / Volume(Catalog.LargeGridSize);
            float small = before.MassPerPipe(Catalog.SmallGridSize) / Volume(Catalog.SmallGridSize);

            Assert.True(small > large * 50f,
                "the flat coolant charge was expected to be far denser on a small grid than a large"
                + " one, which is the defect `C43` corrects; it read " + small.ToString("n1")
                + " against " + large.ToString("n1") + " kg/m3");
        }

        [Fact]

        public void TheRingHoldsMoreCoolantThanItUsedTo()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3), 1);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop shipped = simulation.Solver.Loops[0];

            float now = shipped.CapacityKilograms;
            float sinkNow = shipped.LinkConductance(0);

            simulation.LoopProperties = LoopBefore.For(simulation.Grid.GridSize);
            simulation.RebuildAll();

            CoolantLoop before = simulation.Solver.Loops[0];

            Assert.True(before.CapacityKilograms > 0f,
                "the before ring held nothing, so nothing here is measured");

            Assert.True(now > before.CapacityKilograms,
                "the ring holds " + now.ToString("n0") + " kg as it ships against "
                + before.CapacityKilograms.ToString("n0") + " kg before, so the correction reaches"
                + " no fluid");

            Assert.NotEqual(sinkNow, before.LinkConductance(0), 1);
        }


        private static float Volume(float cell)
        {
            return cell * cell * cell;
        }
    }
}
