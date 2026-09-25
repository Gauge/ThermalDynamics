using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Six face counts in one field, and what the packing may not lose.**
    ///
    /// <para>
    /// A node kept its exposed faces as an `int[6]`: 48 bytes to hold 24 bytes of payload, once per
    /// node (backlog.md `E2`). Ten bits a face holds 1,023 against a real
    /// worst case of about a hundred — the widest vanilla block is ten cells across — so the
    /// packing is lossless for anything the game has, and these say what happens at the edge rather
    /// than leaving it to be discovered.
    /// </para>
    /// </summary>
    public class FacePackingTests
    {
        [Fact]

        public void EachFaceHoldsItsOwnCount()
        {

            ThermalNode node = Node();

            for (int f = 0; f < Face.Count; f++)
            {
                node.SetExposedFaces(f, (f + 1) * 7);
            }

            for (int f = 0; f < Face.Count; f++)
            {
                Assert.Equal((f + 1) * 7, node.GetExposedFaces(f));
            }

            node.SetExposedFaces(2, 0);
            Assert.Equal(0, node.GetExposedFaces(2));
            Assert.Equal(7, node.GetExposedFaces(0));
            Assert.Equal(42, node.GetExposedFaces(5));
        }

        [Fact]

        public void ACountPastTheLimitIsClampedRatherThanWrapped()
        {

            ThermalNode node = Node();

            node.SetExposedFaces(0, ThermalNode.MaxExposedPerFace + 1);
            Assert.Equal(ThermalNode.MaxExposedPerFace, node.GetExposedFaces(0));

            node.SetExposedFaces(1, int.MaxValue);
            Assert.Equal(ThermalNode.MaxExposedPerFace, node.GetExposedFaces(1));

            node.SetExposedFaces(2, -4);
            Assert.Equal(0, node.GetExposedFaces(2));

            Assert.True(ThermalNode.MaxExposedPerFace > 500,
                "the packing holds only " + ThermalNode.MaxExposedPerFace + " a face");
        }

        [Fact]

        public void AnExposedBlockStillCountsItsOwnFaces()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            ThermalNode node = simulation.Solver.Nodes[0];

            int summed = 0;
            for (int f = 0; f < Face.Count; f++) summed += node.GetExposedFaces(f);

            Assert.Equal(Face.Count, summed);
            Assert.Equal(Face.Count, node.TotalExposedFaces);
            Assert.True(node.ExposedArea > 0f);
        }

        [Fact]

        public void SettingAllSixAtOnceIsTheSixSeparateWritesAndTheRefresh()
        {
            int[][] cases =
            {
                new[] { 0, 0, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 1, 1, 1 },
                new[] { 7, 14, 21, 28, 35, 42 },
                new[] { 100, 0, 3, 0, 0, 1 },
                new[] { ThermalNode.MaxExposedPerFace, 0, 0, 0, 0, 0 },
                new[] { ThermalNode.MaxExposedPerFace + 1, int.MaxValue, -4, 2, 0, 9 },
            };

            foreach (int[] counts in cases)
            {

                ThermalNode reference = Node();
                for (int f = 0; f < Face.Count; f++) reference.SetExposedFaces(f, counts[f]);
                reference.RefreshExposure();


                ThermalNode packed = Node();
                bool expectDirty = false;
                for (int f = 0; f < Face.Count; f++)
                {
                    int clamped = counts[f] < 0 ? 0
                        : counts[f] > ThermalNode.MaxExposedPerFace ? ThermalNode.MaxExposedPerFace
                        : counts[f];
                    if (clamped != packed.GetExposedFaces(f)) expectDirty = true;
                }
                packed.SetExposedFaces(counts);

                string where = "counts " + string.Join(",", counts);
                for (int f = 0; f < Face.Count; f++)
                {
                    Assert.Equal(reference.GetExposedFaces(f), packed.GetExposedFaces(f));
                }

                Assert.Equal(reference.TotalExposedFaces, packed.TotalExposedFaces);
                Assert.True(reference.ExposedArea.Equals(packed.ExposedArea),
                    where + ": area " + reference.ExposedArea + " became " + packed.ExposedArea);
                Assert.True(reference.RadiationCoefficient.Equals(packed.RadiationCoefficient),
                    where + ": radiation coefficient " + reference.RadiationCoefficient
                    + " became " + packed.RadiationCoefficient);
                Assert.True(expectDirty == packed.StateDirty,
                    where + ": the write " + (expectDirty ? "moved a face" : "changed nothing")
                    + " and left the node " + (packed.StateDirty ? "dirty" : "clean"));
            }
        }

        [Fact]

        public void SettingAllSixFromATooShortArrayIsRejected()
        {

            ThermalNode node = Node();

            Assert.Throws<System.ArgumentException>(() => node.SetExposedFaces(new int[Face.Count - 1]));
            Assert.Throws<System.ArgumentException>(() => node.SetExposedFaces((int[])null));
        }

        [Fact]

        public void RepeatingTheSameCountsDoesNotMarkTheNodeDirty()
        {

            ThermalNode node = Node();
            int[] counts = { 3, 0, 5, 1, 0, 2 };

            node.SetExposedFaces(counts);
            Assert.True(node.StateDirty, "the first call changed the node and must say so");

            node.StateDirty = false;
            node.SetExposedFaces(counts);
            Assert.False(node.StateDirty, "the same six counts are not a change");

            node.SetExposedFaces(new[] { 3, 0, 5, 1, 0, 2 });
            Assert.False(node.StateDirty);

            node.SetExposedFaces(new[] { 3, 0, 5, 1, 0, 3 });
            Assert.True(node.StateDirty, "a face that moved must mark the node");

            node.StateDirty = false;
            node.SetExposedFaces(new[] { 3, 0, 5, 1, 0, ThermalNode.MaxExposedPerFace + 9 });
            Assert.True(node.StateDirty);

            node.StateDirty = false;
            node.SetExposedFaces(new[] { 3, 0, 5, 1, 0, int.MaxValue });
            Assert.False(node.StateDirty, "both clamp to the ceiling, so nothing moved");
        }

        [Fact]

        public void APackingWrittenWithoutItsDerivedValuesIsNotSkipped()
        {

            ThermalNode node = Node();

            node.SetExposedFaces(new[] { 4, 4, 4, 4, 4, 4 });
            Assert.Equal(24, node.TotalExposedFaces);
            float areaOfTwentyFour = node.ExposedArea;

            int[] counts = { 7, 7, 7, 7, 7, 7 };
            for (int f = 0; f < Face.Count; f++) node.SetExposedFaces(f, counts[f]);
            Assert.Equal(24, node.TotalExposedFaces);
            Assert.Equal(areaOfTwentyFour, node.ExposedArea);

            node.StateDirty = false;
            node.SetExposedFaces(counts);

            Assert.True(node.StateDirty, "the derived values were stale, so this was a change");
            Assert.Equal(42, node.TotalExposedFaces);
            Assert.True(node.ExposedArea > areaOfTwentyFour);
            Assert.True(node.RadiationCoefficient > 0f);
        }


        private static ThermalNode Node()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();
            return simulation.Solver.Nodes[0];
        }
    }
}
