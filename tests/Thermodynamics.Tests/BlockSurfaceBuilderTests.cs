using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The bridge a host crosses to describe a block type: airtightness and mount rectangles in,
    /// surface bits out. Everything the game adapter reads from a block definition ends up here,
    /// so these are the tests that pin down what the adapter is allowed to produce.
    /// </summary>
    public class BlockSurfaceBuilderTests
    {

        private static MountRect FullFace(int face)
        {
            Vector3I normal = Face.Offsets[face];
            switch (face)
            {

                case Face.Forward: return new MountRect(normal, new Vector3(0, 0, 0), new Vector3(1, 1, 0));

                case Face.Backward: return new MountRect(normal, new Vector3(0, 0, 1), new Vector3(1, 1, 1));

                case Face.Left: return new MountRect(normal, new Vector3(0, 0, 0), new Vector3(0, 1, 1));

                case Face.Right: return new MountRect(normal, new Vector3(1, 0, 0), new Vector3(1, 1, 1));

                case Face.Down: return new MountRect(normal, new Vector3(0, 0, 0), new Vector3(1, 0, 1));

                default: return new MountRect(normal, new Vector3(0, 1, 0), new Vector3(1, 1, 1));
            }
        }

        [Fact]

        public void FullFaceMountCoversItsOwnFaceOnly()
        {
            for (int face = 0; face < Face.Count; face++)
            {

                MountRect mount = FullFace(face);
                for (int probe = 0; probe < Face.Count; probe++)
                {
                    bool covered = BlockSurfaceBuilder.MountCovers(ref mount, Vector3I.Zero, probe);
                    Assert.Equal(probe == face, covered);
                }
            }
        }

        [Fact]

        public void DisabledMountCoversNothing()
        {

            MountRect mount = FullFace(Face.Up);
            mount.Enabled = false;

            Assert.False(BlockSurfaceBuilder.MountCovers(ref mount, Vector3I.Zero, Face.Up));
        }

        [Fact]

        public void MountOnAFarCellDoesNotCoverTheNearOne()
        {

            MountRect mount = new MountRect(Vector3I.Right, new Vector3(3, 0, 0), new Vector3(3, 1, 1));

            Assert.True(BlockSurfaceBuilder.MountCovers(ref mount, new Vector3I(2, 0, 0), Face.Right));
            Assert.False(BlockSurfaceBuilder.MountCovers(ref mount, new Vector3I(1, 0, 0), Face.Right));
            Assert.False(BlockSurfaceBuilder.MountCovers(ref mount, Vector3I.Zero, Face.Right));
        }

        [Fact]

        public void AirtightBlockSealsEveryFaceOfEveryCell()
        {
            int[] states = BlockSurfaceBuilder.BuildSurfaces(new Vector3I(2, 1, 3), true, null, null);

            Assert.Equal(6, states.Length);
            for (int i = 0; i < states.Length; i++)
            {
                Assert.True(CellSurface.IsFullySealed(states[i]));
            }
        }

        [Fact]

        public void SealTestDrivesSealingPerCellAndFace()
        {

            SealTest seals = delegate (Vector3I cell, int face)
            {
                return cell.Y == 1 && face == Face.Up;
            };

            int[] states = BlockSurfaceBuilder.BuildSurfaces(new Vector3I(1, 2, 1), false, seals, null);

            Assert.False(CellSurface.SelfAirtight(states[0], Face.Up));
            Assert.True(CellSurface.SelfAirtight(states[1], Face.Up));
            Assert.False(CellSurface.SelfAirtight(states[1], Face.Down));
        }

        [Fact]

        public void MountsBecomeSelfMountBits()
        {
            List<MountRect> mounts = new List<MountRect>
            {
                FullFace(Face.Up),
                FullFace(Face.Down),
            };

            int[] states = BlockSurfaceBuilder.BuildSurfaces(Vector3I.One, false, null, mounts);

            Assert.True(CellSurface.SelfMount(states[0], Face.Up));
            Assert.True(CellSurface.SelfMount(states[0], Face.Down));
            Assert.False(CellSurface.SelfMount(states[0], Face.Left));
            Assert.False(CellSurface.SelfMount(states[0], Face.Forward));
        }

        [Fact]

        public void SurfacesAreIndexedTheWayBlockModelIndexesThem()
        {

            Vector3I size = new Vector3I(2, 3, 4);


            SealTest seals = delegate (Vector3I cell, int face)
            {
                return cell == new Vector3I(1, 2, 3) && face == Face.Right;
            };

            int[] states = BlockSurfaceBuilder.BuildSurfaces(size, false, seals, null);


            BlockModel model = new BlockModel();
            model.Size = size;
            model.LocalSurfaces = states;

            Assert.Equal(model.CellCount, states.Length);
            Assert.True(CellSurface.SelfAirtight(
                states[model.LocalCellIndex(new Vector3I(1, 2, 3))], Face.Right));
        }

        [Fact]

        public void FallbackSurfacesMountEverywhereAndSealOnlyWhenAsked()
        {
            int[] open = BlockSurfaceBuilder.BuildFallbackSurfaces(new Vector3I(1, 1, 2), false);
            int[] airtight = BlockSurfaceBuilder.BuildFallbackSurfaces(new Vector3I(1, 1, 2), true);

            Assert.Equal(2, open.Length);
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.True(CellSurface.SelfMount(open[0], face));
                Assert.False(CellSurface.SelfAirtight(open[0], face));
                Assert.True(CellSurface.SelfAirtight(airtight[0], face));
            }
        }

        [Fact]

        public void MountFractionsFollowFromTheBuiltSurfaces()
        {

            List<MountRect> mounts = new List<MountRect> { FullFace(Face.Up) };


            BlockModel model = new BlockModel();
            model.Size = Vector3I.One;
            model.LocalSurfaces = BlockSurfaceBuilder.BuildSurfaces(Vector3I.One, true, null, mounts);

            Assert.Equal(1f, model.LocalFaceMountFraction(Face.Up));
            Assert.Equal(0f, model.LocalFaceMountFraction(Face.Down));
            Assert.Equal(1f, model.LocalFaceSealFraction(Face.Up));
        }
    }
}
