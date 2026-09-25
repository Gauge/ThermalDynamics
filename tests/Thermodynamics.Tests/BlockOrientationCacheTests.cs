using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// `BlockOrientation` rotates through a table built once from its own matrix, and the table is
    /// held against that matrix exhaustively (`D8`): every one of the thirty-six forward/up pairs —
    /// the twenty-four orientations and the twelve degenerate pairs that keep the matrix path —
    /// over every integer vector in a 7×7×7 cube, forward, back, and per face.
    ///
    /// <para>
    /// The matrix is a signed permutation, so both forms are exact on integers and *identical* is
    /// the right word rather than *close*. What this pins is not the arithmetic but the wiring: a
    /// slot computed wrongly, an axis stored in the wrong order, or a transpose that is not the
    /// inverse would each agree on the identity and disagree somewhere in the cube.
    /// </para>
    /// </summary>
    public class BlockOrientationCacheTests
    {
        private const int Reach = 3;


        private static int Pairs(System.Action<BlockOrientation> body)
        {
            int pairs = 0;
            for (int f = 0; f < 6; f++)
            {
                for (int u = 0; u < 6; u++)
                {
                    body(new BlockOrientation((Base6Directions.Direction)f, (Base6Directions.Direction)u));
                    pairs++;
                }
            }
            return pairs;
        }

        [Fact]

        public void RotateAgreesWithTheMatrixOnEveryVectorOfEveryPair()
        {
            int checkedVectors = 0;

            int pairs = Pairs(delegate (BlockOrientation orientation)
            {
                for (int x = -Reach; x <= Reach; x++)
                for (int y = -Reach; y <= Reach; y++)
                for (int z = -Reach; z <= Reach; z++)
                {

                    Vector3I local = new Vector3I(x, y, z);
                    Vector3I expected = orientation.RotateByMatrix(local);
                    Vector3I actual = orientation.Rotate(local);
                    Assert.True(expected == actual,
                        orientation + " rotates " + local + " to " + actual + " by table and "
                        + expected + " by matrix");
                    checkedVectors++;
                }
            });

            Assert.Equal(36, pairs);
            Assert.Equal(36 * 343, checkedVectors);
        }

        [Fact]

        public void UnrotateAgreesWithTheTransposedMatrixOnEveryVectorOfEveryPair()
        {
            Pairs(delegate (BlockOrientation orientation)
            {
                for (int x = -Reach; x <= Reach; x++)
                for (int y = -Reach; y <= Reach; y++)
                for (int z = -Reach; z <= Reach; z++)
                {

                    Vector3I grid = new Vector3I(x, y, z);
                    Vector3I expected = orientation.UnrotateByMatrix(grid);
                    Vector3I actual = orientation.Unrotate(grid);
                    Assert.True(expected == actual,
                        orientation + " unrotates " + grid + " to " + actual + " by table and "
                        + expected + " by matrix");
                }
            });
        }

        [Fact]

        public void RotateFaceAgreesWithTheMatrixOnEveryFaceOfEveryPair()
        {
            Pairs(delegate (BlockOrientation orientation)
            {
                for (int face = 0; face < Face.Count; face++)
                {
                    int expected = Face.IndexOf(orientation.RotateByMatrix(Face.Offsets[face]));
                    Assert.Equal(expected, orientation.RotateFace(face));
                }

                Assert.Equal(-1, orientation.RotateFace(-1));
                Assert.Equal(-1, orientation.RotateFace(Face.Count));
            });
        }

        [Fact]

        public void EveryLegalOrientationRotatesAndUnrotatesToWhereItStarted()
        {
            int legal = 0;
            Pairs(delegate (BlockOrientation orientation)
            {
                Vector3 f = Base6Directions.GetVector(orientation.Forward);
                Vector3 u = Base6Directions.GetVector(orientation.Up);
                if (System.Math.Abs(Vector3.Dot(f, u)) > 0.001f) return;
                legal++;


                Vector3I probe = new Vector3I(1, 2, 3);
                Vector3I there = orientation.Rotate(probe);
                Assert.True(there != probe || orientation.Equals(BlockOrientation.Identity)
                    || System.Math.Abs(there.X) + System.Math.Abs(there.Y) + System.Math.Abs(there.Z) == 6);
                Assert.Equal(probe, orientation.Unrotate(there));
            });
            Assert.Equal(24, legal);
        }
    }
}
