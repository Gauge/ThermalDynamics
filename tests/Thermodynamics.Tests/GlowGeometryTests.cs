using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The arithmetic behind a drawn glow: the quad that covers one face of a block, and the single
    /// light a grid's glowing blocks reduce to.
    ///
    /// <para>
    /// Both live in <c>Core</c> rather than beside the draw call precisely so they can be checked
    /// here. What cannot be checked offline is whether the renderer draws what it is handed, which
    /// is a session's job — see known-issues.md, testing gaps.
    /// </para>
    /// </summary>
    public class GlowGeometryTests
    {
        [Fact]

        public void EveryFacesTangentsSpanIt()
        {
            for (int face = 0; face < Face.Count; face++)
            {
                Vector3 left, up;
                FaceQuad.Tangents(face, out left, out up);
                Vector3 normal = Face.Normals[face];

                Assert.Equal(1f, left.Length(), 4);
                Assert.Equal(1f, up.Length(), 4);
                Assert.Equal(0f, Vector3.Dot(left, up), 4);
                Assert.Equal(0f, Vector3.Dot(left, normal), 4);
                Assert.Equal(0f, Vector3.Dot(up, normal), 4);
            }
        }

        [Fact]

        public void AQuadCoversTheFaceItIsDrawnOn()
        {
            Vector3 half = FaceQuad.HalfExtents(Vector3I.Zero, new Vector3I(0, 1, 2), 2.5f);
            Assert.Equal(new Vector3(1.25f, 2.5f, 3.75f), half);

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3 left, up;
                FaceQuad.Tangents(face, out left, out up);
                Vector3 normal = Face.Normals[face];

                float width = FaceQuad.Extent(ref half, ref left);
                float height = FaceQuad.Extent(ref half, ref up);
                float reach = FaceQuad.Extent(ref half, ref normal);

                List<float> got = new List<float> { width, height, reach };
                got.Sort();

                List<float> want = new List<float> { half.X, half.Y, half.Z };
                want.Sort();

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(want[i], got[i], 4);
                }
            }
        }

        [Fact]

        public void AnExtentIsADistanceAndNeverNegative()
        {

            Vector3 half = new Vector3(1.25f, 2.5f, 3.75f);

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3 normal = Face.Normals[face];
                Assert.True(FaceQuad.Extent(ref half, ref normal) > 0f, Face.Name(face));
            }
        }

        [Fact]

        public void NothingGlowingIsNotARegion()
        {
            GlowRegion region;

            Assert.False(GlowRegion.Reduce(new List<LitBlock>(), out region));
            Assert.False(GlowRegion.Reduce(null, out region));

            List<LitBlock> cooled = new List<LitBlock>
            {
                new LitBlock { Position = Vector3I.Zero, Kelvin = 900f, Glow = 0f },
            };
            Assert.False(GlowRegion.Reduce(cooled, out region));
        }

        [Fact]

        public void TheCentreFollowsTheBlocksThatAreFailing()
        {
            List<LitBlock> lit = new List<LitBlock>
            {
                new LitBlock { Position = Vector3I.Zero, Kelvin = 1200f, Glow = 1f },
            };

            for (int i = 0; i < 9; i++)
            {
                lit.Add(new LitBlock
                {

                    Position = new Vector3I(10, 0, 0),
                    Kelvin = 820f,
                    Glow = 0.05f,
                });
            }

            GlowRegion region;
            Assert.True(GlowRegion.Reduce(lit, out region));

            Assert.Equal(3.10, region.Centre.X, 2);
            Assert.Equal(0d, region.Centre.Y, 6);

            Assert.Equal(1200f, region.Kelvin);
            Assert.Equal(1f, region.Glow);
        }

        [Fact]

        public void TheRadiusReachesTheFurthestGlowingBlock()
        {
            List<LitBlock> lit = new List<LitBlock>
            {

                new LitBlock { Position = new Vector3I(-4, 0, 0), Kelvin = 900f, Glow = 0.5f },

                new LitBlock { Position = new Vector3I(4, 0, 0), Kelvin = 900f, Glow = 0.5f },

                new LitBlock { Position = new Vector3I(0, 0, 0), Kelvin = 900f, Glow = 0f },
            };

            GlowRegion region;
            Assert.True(GlowRegion.Reduce(lit, out region));

            Assert.Equal(0d, region.Centre.X, 6);
            Assert.Equal(4f, region.RadiusCells, 4);
        }

        [Fact]

        public void OneGlowingBlockIsARegionOnIt()
        {
            List<LitBlock> lit = new List<LitBlock>
            {

                new LitBlock { Position = new Vector3I(3, -2, 7), Kelvin = 1000f, Glow = 0.4f },
            };

            GlowRegion region;
            Assert.True(GlowRegion.Reduce(lit, out region));

            Assert.Equal(new Vector3D(3, -2, 7), region.Centre);
            Assert.Equal(0f, region.RadiusCells);
            Assert.Equal(0.4f, region.Glow);
        }
    }
}
