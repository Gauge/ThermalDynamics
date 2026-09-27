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
    public class AeroFlightTests
    {
        private static readonly AeroProfile Shuttlecock =
            AeroProfile.Build(AeroFlightLab.Shuttlecock(), AeroProfile.FineDirections);

        [Fact]

        public void S5AShuttlecockKickedTwentyDegreesComesBackAndStays()
        {
            AeroFlightLab.Flight flight = AeroFlightLab.Fly(Shuttlecock, Vector3.Backward, 50f, 1.225f, 20f, 60f);

            Assert.True(flight.LastOutsideFive <= 30f, "still outside 5° at " + flight.LastOutsideFive + " s");
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
    }
}
