using Aerodynamics.Core;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The aerodynamic moments, flown**: step 1's registered rigs S5 to S8 on a rigid body at 60 Hz
    /// (aerodynamics.md, Step 1's criteria). A shuttlecock flown boom-first — its frontal area behind
    /// its centre of mass — comes back to its heading after a 20° kick and stays; flown plate-first it
    /// swings round; a spun hull's spin halves in finite time, sooner in thicker air; and the numerical
    /// guard binds on none of them, because a resolved case never needs it.
    /// </summary>
    [Trait("scope", "integration")]
    public class AeroFlightTests
    {
        private static readonly AeroProfile Shuttlecock =
            AeroProfile.Build(AeroFlightLab.Shuttlecock(), AeroProfile.FineDirections);

        [Fact]

        public void S5AShuttlecockKickedTwentyDegreesComesBackAndStays()
        {
            AeroFlightLab.Flight flight = AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1.225f, 20f, 60f);

            Assert.True(flight.LastOutsideFive <= 30f, "still outside 5° at " + flight.LastOutsideFive + " s");
            Assert.Equal(3600, flight.Steps);
            Assert.InRange(flight.FinalError, 0f, 5f);
            Assert.Equal(0, flight.GuardBound);
        }

        [Fact]

        public void S6TheSameBodyFlownTheOtherWayRoundDiverges()
        {
            AeroFlightLab.Flight flight = AeroFlightLab.Fly(Shuttlecock, Vector3.Forward, 50f, 1.225f, 20f, 60f);

            Assert.True(flight.MaxError > 45f, "stayed within " + flight.MaxError + "°");
            Assert.Equal(0, flight.GuardBound);
        }

        [Fact]

        public void S7ASpinHalvesAndHalvesSoonerInThickerAir()
        {
            AeroProfile cube = AeroProfile.Build(AeroCalibrationLab.Box(8, 8, 8), AeroProfile.FineDirections);
            int thickGuard, thinGuard;

            float thick = AeroFlightLab.SpinHalfLife(cube, 1f, 1f, 3600f, out thickGuard);
            float thin = AeroFlightLab.SpinHalfLife(cube, 0.5f, 1f, 3600f, out thinGuard);

            Assert.True(thick < 3600f, "the spin did not halve");
            Assert.True(thick < thin, "density 1: " + thick + " s, density 0.5: " + thin + " s");
            Assert.Equal(0, thickGuard + thinGuard);
        }

        [Fact]

        public void TheGuardCapsDampingAndLeavesARestoringMomentAlone()
        {
            Vector3 force = Vector3.Zero;
            Vector3 damping = new Vector3(0f, -1e9f, 0f);

            bool bound = AeroProfile.Guard(ref force, ref damping, Vector3.Zero, new Vector3(0f, 0.01f, 0f),
                1000f, new Vector3(100f), AeroFlightLab.Tick);

            Assert.True(bound);
            Assert.Equal(-100f * 0.01f / AeroFlightLab.Tick, damping.Y, 2);
        }

        [Fact]
        public void EmptyFlightCannotLookLikeAStableHeading()
        {
            AeroProfile empty = AeroProfile.Build(new GridModel(2.5f), AeroProfile.CoarseDirections);
            Assert.Throws<ArgumentException>(() => AeroFlightLab.Fly(empty, Vector3.Backward, 50f, 1f, 20f, 1f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void AnInvalidFlightDurationIsRejected(float seconds)
        {
            Assert.Throws<ArgumentException>(() => AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1f, 20f, seconds));
        }

        [Fact]
        public void AnOverflowingForceCannotPassAsZeroHeadingError()
        {
            Assert.Throws<InvalidOperationException>(() =>
                AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, float.MaxValue, 20f, 1f));
        }

        [Fact]
        public void VacuumIsTheControlThatDoesNotRestoreAHeading()
        {
            AeroFlightLab.Flight flight = AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 0f, 20f, 60f);
            Assert.Equal(3600, flight.Steps);
            Assert.Equal(20f, flight.FinalError, 3);
            Assert.Equal(60f, flight.LastOutsideFive, 3);
        }

        [Fact]
        public void ASpinHalvesTheSameWayInEitherDirection()
        {
            AeroProfile cube = AeroProfile.Build(AeroCalibrationLab.Box(4, 4, 4), AeroProfile.CoarseDirections);
            int positiveGuard, negativeGuard;
            float positive = AeroFlightLab.SpinHalfLife(cube, 1f, 1f, 3600f, out positiveGuard);
            float negative = AeroFlightLab.SpinHalfLife(cube, 1f, -1f, 3600f, out negativeGuard);
            Assert.True(positive > 0f && positive < 3600f);
            Assert.Equal(positive, negative);
            Assert.Equal(0, positiveGuard + negativeGuard);
        }

        [Fact]
        public void AFirstTickCrossingIsTimedAfterThatTick()
        {
            AeroProfile cube = AeroProfile.Build(AeroCalibrationLab.Box(4, 4, 4), AeroProfile.CoarseDirections);
            int guard;
            float time = AeroFlightLab.SpinHalfLife(cube, 1e8f, 1f, 1f, out guard);
            Assert.Equal(1, guard);
            Assert.Equal(AeroFlightLab.Tick, time);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void AnInvalidIntegrationTickIsRejected(float tick)
        {
            Assert.Throws<ArgumentException>(() => AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1f, 20f, 1f, tick));
        }

        [Fact]
        public void ASpinThatHasNotHalvedIsReportedAsCensored()
        {
            int guard;
            Assert.Equal(float.PositiveInfinity, AeroFlightLab.SpinHalfLife(Shuttlecock, 0f, 1f, 1f, out guard));
            Assert.Equal(0, guard);
        }

        [Theory]
        [InlineData(120)]
        [InlineData(240)]
        public void TheRestoringFlightConvergesAsTheTickIsRefined(int frequency)
        {
            AeroFlightLab.Flight baseline = AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1.225f, 20f, 60f);
            AeroFlightLab.Flight fine = AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1.225f, 20f, 60f, 1f / frequency);
            Assert.Equal(60 * frequency, fine.Steps);
            Assert.True(fine.LastOutsideFive <= 30f);
            Assert.InRange(Math.Abs(fine.FinalError - baseline.FinalError), 0f, 0.1f);
            Assert.InRange(Math.Abs(fine.LastOutsideFive - baseline.LastOutsideFive), 0f, 0.1f);
            Assert.Equal(0, fine.GuardBound);
        }

        [Fact]
        public void TheSpinClockAgreesWithTheQuadraticDragSolution()
        {
            AeroProfile cube = AeroProfile.Build(AeroCalibrationLab.Box(4, 4, 4), AeroProfile.CoarseDirections);
            // The four-cell cube's face-centre quadrature is K = 2*n*A*sum(|z|*(a²+z²)).
            // I*dω/dt = -ρ*C*K*ω²/2 has half-life I/(ρ*C*K*ω₀/2); see aerodynamics.md.
            float inertia = 64f * Catalog.HeavyArmor().Mass * 100f / 6f;
            float expected = inertia / (0.5f * 1.17f * 17968.75f);
            int guard;
            float actual = AeroFlightLab.SpinHalfLife(cube, 1f, 1f, 3600f, out guard);
            Assert.InRange(Math.Abs(actual - expected), 0f, 2f * AeroFlightLab.Tick);
            Assert.Equal(0, guard);
        }
    }
}
