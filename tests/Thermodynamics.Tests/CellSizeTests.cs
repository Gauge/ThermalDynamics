using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Which cell size is actually the harder one to cool, and the answer is not the one this
    /// repository had written down.**
    ///
    /// <para>
    /// balance.md argued from the arithmetic of a cell face that "the
    /// arithmetic that decides everything else on this page is against small grids by about 2.5×",
    /// and used that to block `C43`'s density correction: half of it is a regression, and it lands
    /// on the size that is already behind. **The first half of that sentence is exact and the
    /// second was never measured.** A small cell face is 0.25 m² against 2.5 m², so the loop pickup
    /// really is a twenty-fifth — but radiation, conduction and the ship's own skin scale by three
    /// different powers of the cell edge, and the game's waste figures scale by none of them.
    /// </para>
    ///
    /// <para>
    /// Measured here, per block, from the definitions alone: **a small grid is 2.1× behind on a
    /// block's own skin, 2.3× behind on the loop pickup, and 2.5× *ahead* on conduction into the
    /// hull.** `tools/corpus/cellsize.py` is the other half, over 8,137 corpus ships, and it says
    /// the same thing louder — a small-grid ship carries 2.47× the exposed skin per kilowatt, buries
    /// a fifth as much of it, and gives its hottest block **19.75×** the path into the hull per watt
    /// that a large-grid ship does.
    /// </para>
    ///
    /// <para>
    /// **So the handicap belongs to the pickup alone, and it does not need a dial.** At the face
    /// count `C42` already measured — three, which is what a routed rectangle gives past a one-cell
    /// source — 79 of the 80 paired blocks have a cooling answer on a small grid exactly where they
    /// have one on a large grid. The exception is the small prototech jump drive, and buying it
    /// costs a **×5.63** small-grid coefficient that would make every other small-grid block
    /// strictly easier to cool than its large counterpart. See balance.md, *What a small cell is
    /// behind on, and what it is not*, and backlog.md `C43`.
    /// </para>
    /// </summary>
    public class CellSizeTests
    {
        private readonly ITestOutputHelper output;


        public CellSizeTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const float WattsFloor = 10000f;

        private const int RoutedFaces = 3;


        private static float Coefficient()
        {
            return LoopThermalProperties.Default().HeatTransferCoefficient;
        }


        private static List<string> WithNoAnswer(IList<CellSizeLab.Pair> pairs, bool large,
            int faces, float coefficient)
        {

            List<string> names = new List<string>();

            for (int i = 0; i < pairs.Count; i++)
            {
                CellSizeLab.Pair pair = pairs[i];
                BlockHeatIndex.Reading reading = large ? pair.Large : pair.Small;
                float cell = large ? Catalog.LargeGridSize : Catalog.SmallGridSize;

                float gradient = CellSizeLab.ForcedGradient(reading.Watts, cell, faces, coefficient);
                if (gradient > reading.CriticalKelvin - BlockHeatIndex.AmbientKelvin)
                {
                    names.Add(pair.Family);
                }
            }

            return names;
        }

        [Fact]

        public void TheGameShipsEnoughPairedBlocksToMeasure()
        {
            List<CellSizeLab.Pair> pairs = CellSizeLab.Pairs();

            int heavy = 0;
            for (int i = 0; i < pairs.Count; i++) if (pairs[i].Large.Watts >= WattsFloor) heavy++;

            output.WriteLine("{0} paired families, {1} of them above {2:n0} W",
                pairs.Count, heavy, WattsFloor);
            output.WriteLine(CellSizeLab.Table(pairs, 12));

            Assert.True(pairs.Count >= 50,
                "only " + pairs.Count + " families paired across the two cell sizes; every figure"
                + " in this class is a statistic over that list, so a list this short means the"
                + " naming convention the pairing relies on has moved rather than that the game has");

            Assert.True(heavy >= 25,
                "only " + heavy + " paired families make more than " + WattsFloor.ToString("n0")
                + " W, and the medians below are quoted over those");

            for (int i = 0; i < pairs.Count; i++)
            {
                Assert.True(pairs[i].Large.Large, pairs[i].Family + " paired a large that is not");
                Assert.False(pairs[i].Small.Large, pairs[i].Family + " paired a small that is not");
                Assert.Equal(pairs[i].Large.TypeId, pairs[i].Small.TypeId);
            }
        }

        [Fact]

        public void TheFourTermsDoNotScaleTogether()
        {
            List<CellSizeLab.Pair> pairs = CellSizeLab.Pairs();

            float radiation = CellSizeLab.MedianHandicap(pairs, p => p.RadiationHandicap, WattsFloor);
            float conduction = CellSizeLab.MedianHandicap(pairs, p => p.ConductionHandicap, WattsFloor);
            float pickup = CellSizeLab.MedianHandicap(pairs, p => p.PickupHandicap, WattsFloor);
            float waste = CellSizeLab.MedianHandicap(pairs, p => 1f / p.WasteRatio, WattsFloor);

            output.WriteLine("median small-grid handicap per watt, over pairs above {0:n0} W:",
                WattsFloor);
            output.WriteLine("  a small block makes 1/{0:n1} of what its large twin does", waste);
            output.WriteLine("  radiation  {0,6:n2}   (1 is parity, above 1 is behind)", radiation);
            output.WriteLine("  conduction {0,6:n2}", conduction);
            output.WriteLine("  pickup     {0,6:n2}", pickup);

            Assert.True(conduction < 1f,
                "the median small-grid block is at " + conduction.ToString("n2")
                + " on conduction into its hull; below 1 means ahead, and balance.md's claim that"
                + " the arithmetic is against small grids rests on this being above it");

            Assert.True(radiation > 1f && pickup > 1f,
                "radiation " + radiation.ToString("n2") + " and pickup " + pickup.ToString("n2")
                + "; both are area terms against a waste figure that is not, so both were expected"
                + " to be behind and a change here means the game's waste figures moved");

            Assert.True(pickup / radiation < 2f && radiation / pickup < 2f,
                "the two area terms parted company: radiation " + radiation.ToString("n2")
                + " against pickup " + pickup.ToString("n2"));
        }

        [Fact]

        public void AtTheFaceCountARoutedRingGivesTheTwoSizesAgree()
        {
            List<CellSizeLab.Pair> pairs = CellSizeLab.Pairs();

            float h = Coefficient();

            output.WriteLine("  faces    small    large");
            for (int faces = 1; faces <= RoutedFaces; faces++)
            {
                output.WriteLine("  {0,5}   {1,6}   {2,6}", faces,
                    WithNoAnswer(pairs, false, faces, h).Count,
                    WithNoAnswer(pairs, true, faces, h).Count);
            }


            List<string> small = WithNoAnswer(pairs, false, RoutedFaces, h);

            List<string> large = WithNoAnswer(pairs, true, RoutedFaces, h);

            output.WriteLine("at {0} faces, small: {1}", RoutedFaces, string.Join(", ", small));
            output.WriteLine("at {0} faces, large: {1}", RoutedFaces, string.Join(", ", large));

            Assert.True(small.Count <= large.Count + 1,
                "at " + RoutedFaces + " sink faces " + small.Count + " small-grid families have no"
                + " cooling answer against " + large.Count + " large-grid ones: "
                + string.Join(", ", small) + ". The decision not to give small grids their own"
                + " pickup coefficient rests on this gap being one block");

            Assert.Contains("PrototechJumpDrive", small);
        }

        [Fact]

        public void ADialLargeEnoughToCloseItWouldOvershootEveryOtherBlock()
        {
            List<CellSizeLab.Pair> pairs = CellSizeLab.Pairs();

            float h = Coefficient();


            int target = WithNoAnswer(pairs, true, RoutedFaces, h).Count;

            float needed = 0f;
            for (float factor = 1f; factor <= 40f; factor += 0.01f)
            {
                if (WithNoAnswer(pairs, false, RoutedFaces, h * factor).Count <= target)
                {
                    needed = factor;
                    break;
                }
            }

            float median = CellSizeLab.MedianHandicap(pairs, p => p.PickupHandicap, WattsFloor);

            float worst = 0f, best = float.MaxValue;
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Large.Watts < WattsFloor) continue;
                if (pairs[i].PickupHandicap > worst) worst = pairs[i].PickupHandicap;
                if (pairs[i].PickupHandicap < best) best = pairs[i].PickupHandicap;
            }

            output.WriteLine("a small-grid coefficient of x{0:n2} closes the gap; the median block"
                + " needs x{1:n2}, and the spread runs x{2:n2} to x{3:n2}", needed, median, best, worst);

            Assert.True(needed > 0f,
                "no factor up to 40 closed the gap, so the block this row turns on is not reachable"
                + " by the coefficient at all and the argument needs rewriting rather than retuning");

            Assert.True(needed > median * 2f,
                "the factor that closes the gap is x" + needed.ToString("n2") + " against a median"
                + " need of x" + median.ToString("n2") + "; the refusal rests on the first being a"
                + " long way past the second");

            Assert.True(best < 1f,
                "the least handicapped paired block is at x" + best.ToString("n2")
                + "; below 1 means a small-grid block that is already easier to cool than its large"
                + " twin, which is what makes a single grid-size dial the wrong instrument");
        }

        [Fact]

        public void TheBoreDerivedCoefficientIsRealPhysicsAndChangesNothing()
        {
            List<CellSizeLab.Pair> pairs = CellSizeLab.Pairs();

            float h = Coefficient();

            double ratio = Catalog.SmallGridSize / (double)Catalog.LargeGridSize;
            float bore = (float)System.Math.Pow(ratio, -0.2);


            int oneFaceShipped = WithNoAnswer(pairs, false, 1, h).Count;

            int oneFaceBore = WithNoAnswer(pairs, false, 1, h * bore).Count;

            int routedShipped = WithNoAnswer(pairs, false, RoutedFaces, h).Count;

            int routedBore = WithNoAnswer(pairs, false, RoutedFaces, h * bore).Count;

            output.WriteLine("bore-derived small-grid coefficient x{0:n3}", bore);
            output.WriteLine("  at 1 face:  {0} -> {1} families with no answer",
                oneFaceShipped, oneFaceBore);
            output.WriteLine("  at {0} faces: {1} -> {2}", RoutedFaces, routedShipped, routedBore);

            Assert.True(bore > 1.3f && bore < 1.45f,
                "the bore ratio gives x" + bore.ToString("n3")
                + "; the argument in balance.md quotes 1.38 and is checked here rather than trusted");

            Assert.Equal(routedShipped, routedBore);

            Assert.True(oneFaceShipped - oneFaceBore <= 1,
                "the bore-derived coefficient moved " + (oneFaceShipped - oneFaceBore)
                + " families off the no-answer list at one sink face; it is refused on being worth"
                + " at most one, and a larger number is a reason to build it");
        }
    }
}
