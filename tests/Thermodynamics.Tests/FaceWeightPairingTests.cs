using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A node's exposure on face <i>f</i> is weighted against the sun's and the wind's incidence on
    /// **that same face**. A sum pairing face 2's exposure with face 3's incidence compiles, stays
    /// symmetric, conserves energy and reads plausibly on every hull, and a cube — which is what
    /// <see cref="SolarSymmetryTests"/> uses — cannot see a permutation at all.
    ///
    /// <para>
    /// The fixture is a block buried on five sides. The assertion is the weaker and more useful one:
    /// that one direction lights each node, and that it is the same function of the open face for all
    /// six — which a permutation breaks and a handedness convention does not.
    /// </para>
    /// </summary>
    public class FaceWeightPairingTests
    {

        private static ThermalSimulation OpenOn(int face, ThermalSettings settings)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            for (int f = 0; f < Face.Count; f++)
            {
                if (f == face) continue;
                builder.Place(Catalog.LightArmor(), Face.Offsets[f]);
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            return simulation;
        }


        private static ThermalNode Centre(ThermalSimulation simulation, int face)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];
                if (node.TotalExposedFaces != 1) continue;
                if (node.GetExposedFaces(face) <= 0) continue;
                return node;
            }

            Assert.Fail("no node was left open on " + Face.Name(face)
                + " alone — the fixture did not bury the other five faces, so the test would be"
                + " asserting about a block with nothing to distinguish its faces");
            return null;
        }

        [Fact]

        public void EachFaceTakesSunFromItsOwnNormalAndNoOther()
        {
            if (!GameBlocks.IsInstalled) return;

            for (int open = 0; open < Face.Count; open++)
            {
                for (int from = 0; from < Face.Count; from++)
                {

                    ThermalSimulation simulation = OpenOn(open, new ThermalSettings());
                    simulation.StepExact(1, Worlds.Space(Face.Normals[from]));


                    float watts = Centre(simulation, open).LastSolarWatts;
                    string what = "open on " + Face.Name(open) + ", sun from " + Face.Name(from);

                    if (open == from)
                    {
                        Assert.True(watts > 0f, what
                            + ": took no sunlight, so the open face was weighted against some"
                            + " other face's incidence");
                    }
                    else
                    {
                        Assert.True(watts == 0f, what + ": took " + watts
                            + " W with its only open face edge-on or facing away, so the sum"
                            + " paired that face with the wrong incidence");
                    }
                }
            }
        }

        [Fact]

        public void EveryFaceMeetsTheWindOnTheSameConventionAsTheFirst()
        {
            if (!GameBlocks.IsInstalled) return;

            const float Speed = 300f;

            int[] answeredTo = new int[Face.Count];

            for (int open = 0; open < Face.Count; open++)
            {
                int found = -1;

                for (int blowing = 0; blowing < Face.Count; blowing++)
                {

                    ThermalSimulation simulation = OpenOn(open, new ThermalSettings());
                    simulation.StepExact(1, Worlds.WindAndMotion(
                        1f, Speed, Face.Normals[blowing], Vector3.Zero));

                    if (Centre(simulation, open).LastFrictionWatts <= 0f) continue;

                    Assert.True(found < 0, "open on " + Face.Name(open)
                        + ": both " + Face.Name(found) + " and " + Face.Name(blowing)
                        + " drove friction through a single open face, so the sum is reading"
                        + " more than one face's incidence");
                    found = blowing;
                }

                Assert.True(found >= 0, "open on " + Face.Name(open)
                    + ": no wind direction reached it, so its exposure is being weighted against"
                    + " an incidence that is always zero");

                answeredTo[open] = found;
            }

            bool identity = answeredTo[0] == 0;

            for (int open = 0; open < Face.Count; open++)
            {
                int expected = identity ? open : Face.Opposite(open);

                Assert.True(answeredTo[open] == expected, "the face open on "
                    + Face.Name(open) + " answered to wind along " + Face.Name(answeredTo[open])
                    + ", where " + Face.Name(0) + " answering to " + Face.Name(answeredTo[0])
                    + " makes " + Face.Name(expected) + " the only consistent answer — the six"
                    + " terms of the wind sum are not paired face for face");
            }
        }
    }
}
