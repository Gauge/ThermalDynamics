using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The census hull's bolt search — which way each partial-mount block turns — is held against
    /// the search it replaced, orientation for orientation (`D8`).
    ///
    /// <para>
    /// The replacement resolves the 24 orientations' rotated faces once and probes each block's six
    /// neighbours once, where the original built a matrix and probed a dictionary for every face of
    /// every candidate of every block. Same candidates in the same order, same tie-break, same
    /// count — so the two must agree exactly, and a hull where they do not is a different ship
    /// from the one every benchmark describes. The reference below is the original, kept verbatim,
    /// because the old code is the only oracle that answers *did this change anything* (`P4`).
    /// </para>
    /// </summary>
    public class CensusBoltTests
    {
        private static class Reference
        {

            public static BlockOrientation[] Bolt(List<Vector3I> layout, int[] tierOf, BlockModel[] tiers)
            {
                BlockOrientation[] chosen = new BlockOrientation[layout.Count];
                Dictionary<Vector3I, int> at = new Dictionary<Vector3I, int>(layout.Count);
                bool[] placed = new bool[layout.Count];

                for (int i = 0; i < layout.Count; i++) at[layout[i]] = i;

                for (int i = 0; i < layout.Count; i++)
                {
                    if (Census.ProducesHeatAt(i))
                    {
                        chosen[i] = BlockOrientation.Identity;
                        placed[i] = true;
                        continue;
                    }

                    int state = tiers[tierOf[i]].LocalSurfaces[0];
                    int best = -1;

                    foreach (BlockOrientation candidate in Orientations())
                    {
                        int joined = 0;

                        for (int face = 0; face < Face.Count; face++)
                        {
                            if (!CellSurface.SelfMount(state, face)) continue;

                            Vector3I toward = candidate.Rotate(Face.Offsets[face]);

                            int neighbour;
                            if (!at.TryGetValue(layout[i] + toward, out neighbour)) continue;

                            if (!placed[neighbour] || MountsToward(tiers, tierOf, chosen, neighbour, -toward))
                            {
                                joined++;
                            }
                        }

                        if (joined <= best) continue;

                        best = joined;
                        chosen[i] = candidate;
                    }

                    placed[i] = true;
                }

                return chosen;
            }


            private static bool MountsToward(
                BlockModel[] tiers, int[] tierOf, BlockOrientation[] chosen, int index, Vector3I toward)
            {
                int state = tiers[tierOf[index]].LocalSurfaces[0];

                for (int face = 0; face < Face.Count; face++)
                {
                    if (!CellSurface.SelfMount(state, face)) continue;
                    if (chosen[index].Rotate(Face.Offsets[face]) == toward) return true;
                }

                return false;
            }


            private static IEnumerable<BlockOrientation> Orientations()
            {
                Array directions = Enum.GetValues(typeof(Base6Directions.Direction));

                foreach (Base6Directions.Direction forward in directions)
                {
                    foreach (Base6Directions.Direction up in directions)
                    {
                        Vector3 f = Base6Directions.GetVector(forward);
                        Vector3 u = Base6Directions.GetVector(up);
                        if (Math.Abs(Vector3.Dot(f, u)) > 0.001f) continue;

                        yield return new BlockOrientation(forward, up);
                    }
                }
            }
        }

        [Theory]
        [InlineData("ship", 2000)]
        [InlineData("ship", 8000)]
        [InlineData("cube", 2000)]
        [InlineData("truss", 2000)]

        public void TheTableDrivenSearchChoosesWhatTheOriginalChose(string shape, int blocks)
        {

            List<Vector3I> layout = new List<Vector3I>(LoadShapes.Build(shape, blocks));
            int[] tierOf = Census.TiersFor(layout);
            BlockModel[] tiers = Census.Models();

            BlockOrientation[] expected = Reference.Bolt(layout, tierOf, tiers);
            BlockOrientation[] actual = Census.Bolt(layout, tierOf, tiers);

            Assert.Equal(expected.Length, actual.Length);

            int turned = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                if (!expected[i].Equals(BlockOrientation.Identity)) turned++;
            }
            Assert.True(turned > expected.Length / 20,
                "only " + turned + " of " + expected.Length + " blocks were turned, so agreement proves nothing");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]),
                    shape + " block " + i + " at " + layout[i] + " is " + actual[i]
                    + " under the table and " + expected[i] + " under the original");
            }
        }
    }
}
