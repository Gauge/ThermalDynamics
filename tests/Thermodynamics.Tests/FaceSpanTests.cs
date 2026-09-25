using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The per-face frame four places used to compute for themselves.**
    ///
    /// <para>
    /// Counting a block's exposure, auditing it and building a room map each walked the six faces
    /// of a block's bounds, and each worked out the same six numbers to do it: which axis the face
    /// is normal to, whether it is the positive side, which row of cells it is, and the two axes it
    /// spans with their half-open bounds.
    /// </para>
    ///
    /// <para>
    /// **The slab is the part worth a test.** It is the far row on a positive face and the near row
    /// on a negative one, and the `- 1` that makes the far row work is the half-open bound — which
    /// is exactly the kind of thing that is right in three copies and wrong in the fourth, silently,
    /// because a wrong slab still walks a real face of a real block. It reads a row of neighbours
    /// that are not the neighbours.
    /// </para>
    ///
    /// <para>
    /// These tests are written against the arithmetic rather than against the extraction, so they
    /// would have caught a copy that had drifted before it was one function.
    /// </para>
    /// </summary>
    public class FaceSpanTests
    {
        [Theory]
        [InlineData(Face.Right, 0)]
        [InlineData(Face.Up, 1)]
        [InlineData(Face.Backward, 2)]

        public void APositiveFaceIsTheLastRowInsideTheBox(int face, int axis)
        {

            Vector3I min = new Vector3I(2, 3, 5);

            Vector3I maxExclusive = new Vector3I(6, 9, 11);

            BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);

            Assert.Equal(axis, span.Axis);
            Assert.True(span.Positive);
            Assert.Equal(BoxGeometry.Component(maxExclusive, axis) - 1, span.Slab);
        }

        [Theory]
        [InlineData(Face.Left, 0)]
        [InlineData(Face.Down, 1)]
        [InlineData(Face.Forward, 2)]

        public void ANegativeFaceIsTheFirstRowOfTheBox(int face, int axis)
        {

            Vector3I min = new Vector3I(2, 3, 5);

            Vector3I maxExclusive = new Vector3I(6, 9, 11);

            BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);

            Assert.Equal(axis, span.Axis);
            Assert.False(span.Positive);
            Assert.Equal(BoxGeometry.Component(min, axis), span.Slab);
        }

        [Fact]

        public void EverySlabIsInsideTheBox()
        {
            for (int sx = 1; sx <= 3; sx++)
            {
                for (int sy = 1; sy <= 3; sy++)
                {
                    for (int sz = 1; sz <= 3; sz++)
                    {

                        Vector3I min = new Vector3I(-2, 4, 7);

                        Vector3I maxExclusive = min + new Vector3I(sx, sy, sz);

                        for (int face = 0; face < Face.Count; face++)
                        {
                            BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);

                            Assert.InRange(span.Slab,
                                BoxGeometry.Component(min, span.Axis),
                                BoxGeometry.Component(maxExclusive, span.Axis) - 1);
                        }
                    }
                }
            }
        }

        [Fact]

        public void TheSpannedAxesAreTheOtherTwoAxesExactlyOnce()
        {
            Vector3I min = Vector3I.Zero;

            Vector3I maxExclusive = new Vector3I(3, 4, 5);

            for (int face = 0; face < Face.Count; face++)
            {
                BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);

                Assert.NotEqual(span.Axis, span.U);
                Assert.NotEqual(span.Axis, span.V);
                Assert.NotEqual(span.U, span.V);
            }
        }

        [Fact]

        public void TheSpanCoversTheFacesArea()
        {

            Vector3I min = new Vector3I(1, 1, 1);

            Vector3I size = new Vector3I(2, 3, 4);
            Vector3I maxExclusive = min + size;

            for (int face = 0; face < Face.Count; face++)
            {
                BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);

                int cells = (span.MaxExclusiveU - span.MinU) * (span.MaxExclusiveV - span.MinV);
                int expected = BoxGeometry.Component(size, span.U) * BoxGeometry.Component(size, span.V);

                Assert.Equal(expected, cells);
            }
        }
    }
}
