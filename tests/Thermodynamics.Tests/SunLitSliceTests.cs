using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The lit-fraction sweep is sliced across frames, and a sliced pass has to land on the same
    /// answer the whole pass would have.
    ///
    /// <para>
    /// Two budgeted walks run behind the sun: the shadow map's, which decides which cell faces the
    /// sun reaches, and the solver's own, which turns that map into a lit fraction per node face.
    /// The suite pinned incremental-versus-one-shot equivalence for the conduction graph and for
    /// the room flood fill, and never for this one — it checked only that the *cached environment
    /// rows* were invalidated when the sweep published, which is a different claim: it says a stale
    /// row is not read, not that the row that replaces it is right.
    /// </para>
    ///
    /// <para>
    /// The gap mattered because the sweep writes into a shared array in place. A cursor that
    /// overran, a restart that reset the fill value but not the cursor, or a budget boundary that
    /// skipped a node would leave some faces carrying the previous pass's fraction, and every
    /// symptom of that is a solar figure slightly wrong on a large grid — which nothing else here
    /// would catch.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class SunLitSliceTests
    {
        private const int Blocks = 4000;

        private static readonly Vector3 Sun = Vector3.Normalize(new Vector3(0.71f, 0.42f, -0.56f));


        private static ThermalSimulation Build(bool selfShadow, int shadowBudget, int litBudget)
        {
            ThermalSettings settings = Hulls.Uncapped();
            settings.SolarSelfShadowing = selfShadow;
            settings.Derive();

            ThermalSimulation simulation = Hulls.Driven(settings, Blocks);
            simulation.Solver.SunShadowBudget = shadowBudget;
            simulation.Solver.SunLitBudget = litBudget;
            return simulation;
        }


        private static void Settle(ThermalSimulation simulation, EnvironmentSample sample, int frames)
        {
            for (int i = 0; i < frames; i++) simulation.StepExact(1, sample);
            simulation.Solver.FinishSunLit();
        }


        private static float[] LitFractions(ThermalSimulation simulation)
        {
            int nodes = simulation.Solver.Nodes.Count;
            float[] lit = new float[nodes * Face.Count];

            for (int node = 0; node < nodes; node++)
            {
                for (int face = 0; face < Face.Count; face++)
                {
                    lit[(node * Face.Count) + face] = simulation.Solver.SunLitFraction(node, face);
                }
            }

            return lit;
        }


        private static void RequireAShadow(float[] lit, string what)
        {
            int shaded = 0;
            int open = 0;

            for (int i = 0; i < lit.Length; i++)
            {
                if (lit[i] <= 0.001f) shaded++;
                else if (lit[i] >= 0.999f) open++;
            }

            Assert.True(shaded > 0 && open > 0,
                what + " produced " + shaded + " shaded and " + open + " lit faces of " + lit.Length
                + ", so the hull casts no shadow and the comparison is vacuous");
        }

        [Fact]

        public void ASlicedSweepLandsWhereAWholeOneDoes()
        {

            ThermalSimulation whole = Build(true, int.MaxValue, int.MaxValue);

            ThermalSimulation sliced = Build(true, 97, 13);

            EnvironmentSample sample = Worlds.Space(Sun);

            Settle(whole, sample, 4);
            Settle(sliced, sample, 600);

            Assert.True(whole.Solver.SunShadow.IsBuilt, "the whole pass should have completed");
            Assert.True(sliced.Solver.SunShadow.IsBuilt, "the sliced pass should have completed");
            Assert.False(sliced.Solver.SunLitRefreshPending, "the drain should have finished the sweep");


            float[] expected = LitFractions(whole);

            float[] actual = LitFractions(sliced);

            RequireAShadow(expected, "the whole pass");
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail("face " + i + " of " + expected.Length + ": whole pass wrote "
                        + expected[i] + ", sliced pass wrote " + actual[i]);
                }
            }
        }

        [Fact]

        public void WithSelfShadowingOffEveryFaceEndsFullyLitHoweverThinTheBudget()
        {

            ThermalSimulation sliced = Build(false, 11, 7);

            EnvironmentSample sample = Worlds.Space(Sun);
            Settle(sliced, sample, 600);

            Assert.False(sliced.Solver.SunLitRefreshPending);


            float[] lit = LitFractions(sliced);
            Assert.True(lit.Length > 0);

            for (int i = 0; i < lit.Length; i++)
            {
                if (lit[i] != 1f)
                {
                    Assert.Fail("face " + i + " came back at " + lit[i]
                        + " with self-shadowing off, where every face is unshadowed by definition");
                }
            }
        }

        [Fact]

        public void ASunThatMovesMidSweepLeavesNoTraceOfTheOldOne()
        {

            ThermalSimulation disturbed = Build(true, 97, 13);

            ThermalSimulation clean = Build(true, int.MaxValue, int.MaxValue);

            for (int i = 0; i < 40; i++)
            {
                float angle = i * 0.4f;
                Vector3 moving = Vector3.Normalize(

                    new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0.3f));
                disturbed.StepExact(1, Worlds.Space(moving));
            }

            EnvironmentSample settled = Worlds.Space(Sun);
            Settle(disturbed, settled, 600);
            Settle(clean, settled, 4);


            float[] expected = LitFractions(clean);

            float[] actual = LitFractions(disturbed);

            RequireAShadow(expected, "the undisturbed run");

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail("face " + i + ": a settled sun gives " + expected[i]
                        + ", but the run that saw the sun move gives " + actual[i]);
                }
            }
        }
    }
}
