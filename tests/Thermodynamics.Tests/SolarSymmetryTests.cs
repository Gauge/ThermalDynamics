using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A symmetric block heats identically whichever way the sun comes from.
    ///
    /// <para>
    /// Every corpus measurement of sunlight uses one hard-coded direction — the survey holds the
    /// sun at <c>Forward</c> for every ship — so a face-projection or face-index error in any of
    /// the other five directions is invisible to the entire population pass. This is the guard
    /// that makes that affordable: a lone armour cube has six identical faces, so the solar watts
    /// it takes must be identical under all six sun directions, and any asymmetry is a bug in the
    /// per-face path rather than a fact about the block.
    /// </para>
    /// </summary>
    public class SolarSymmetryTests
    {
        [Fact]

        public void ALoneCubeTakesTheSameSunFromAllSixDirections()
        {
            if (!GameBlocks.IsInstalled) return;

            Vector3[] directions =
            {
                Vector3.Forward, Vector3.Backward,
                Vector3.Left, Vector3.Right,
                Vector3.Up, Vector3.Down,
            };

            float first = 0f;

            foreach (Vector3 direction in directions)
            {
                GridBuilder builder = GridBuilder.Large();
                builder.Place(Catalog.LightArmor(), Vector3I.Zero);

                ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
                simulation.Solver.CollectDiagnostics = true;
                simulation.StepExact(1, Worlds.Space(direction));

                float watts = simulation.Solver.Nodes[0].LastSolarWatts;
                Assert.True(watts > 0f, "no solar watts from direction " + direction);

                if (first == 0f) first = watts;
                else
                {
                    Assert.True(System.Math.Abs(watts - first) < first * 0.001f,
                        "direction " + direction + " delivered " + watts + " W against "
                        + first + " W from the first direction — the per-face solar path is "
                        + "asymmetric");
                }
            }
        }
    }
}
