using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What windward shielding would cost, measured before it is built — because the one thing
    /// the drag milestone says has no measurement behind it is the thing that decides whether it can work at all.**
    ///
    /// <para>
    /// The machinery exists. `SunShadowMap` is not a sun pass, it is a **direction** pass:
    /// `Restart(grid, direction, occluders)` takes the direction as an argument, walks the grid
    /// against it and produces a per-face lit fraction. Aimed at the relative wind it would give
    /// per-face windward exposure, so a block behind another stops being heated and dragged as
    /// though it were in the open.
    /// </para>
    ///
    /// <para>
    /// **The unmeasured part is the cadence, and the cadence is not about the weather.** The pass
    /// restarts when its direction moves more than `SunRebuildCosine`, which is cos(2°), and the
    /// direction it tracks is in **grid-local** space. The sun crosses two degrees in tens of
    /// seconds. The relative wind crosses two degrees whenever the *ship turns two degrees* — so
    /// the rate is set by the pilot, not the sky.
    /// </para>
    ///
    /// <para>
    /// This measures how long a pass takes on a real hull and states the turn rate at which it can
    /// never finish. It is the number the drag milestone has to be designed against.
    /// </para>
    /// </summary>
    public class WindShieldingCostTests
    {
        private readonly ITestOutputHelper output;


        public WindShieldingCostTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const double RebuildDegrees = 2.0;


        private static ThermalSimulation Hull(int side)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.SolarSelfShadowing = true;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(side, side, side));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(48)]

        public void ADirectionPassIsOutrunByAnOrdinaryTurnRate(int side)
        {

            ThermalSimulation simulation = Hull(side);
            ThermalSolver solver = simulation.Solver;

            simulation.StepExact(1, Worlds.Space(Vector3.Forward));


            SunShadowMap map = new SunShadowMap();
            map.Restart(solver.Grid, Vector3.Forward, null);

            int substeps = 0;
            while (map.IsRunning && substeps < 100000)
            {
                map.Step(solver.SunShadowBudget);
                substeps++;
            }

            Assert.True(map.IsBuilt, "the pass never completed, so its cost is unmeasured");

            double stepSeconds = 1.0 / simulation.Settings.Frequency;
            double secondsToComplete = substeps * stepSeconds;
            double outrunDegreesPerSecond = RebuildDegrees / Math.Max(1e-9, secondsToComplete);

            output.WriteLine(
                "{0,3} cells a side: {1,5} blocks, {2,6} substeps, {3,7:0.00} s to complete, "
                + "outrun above {4,7:0.00} deg/s",
                side, solver.Nodes.Count, substeps, secondsToComplete, outrunDegreesPerSecond);

            Assert.True(substeps > 0, "the pass completed in no substeps, so nothing was walked");
        }

        [Fact]

        public void TheWindDirectionMovesOrdersOfMagnitudeFasterThanTheSun()
        {
            const double dayLengthSeconds = 2 * 60 * 60;
            double sunDegreesPerSecond = 360.0 / dayLengthSeconds;

            double sunSecondsPerRebuild = RebuildDegrees / sunDegreesPerSecond;
            double yawSecondsPerRebuild = RebuildDegrees / 10.0;

            output.WriteLine("sun crosses 2 deg every {0:0.0} s; a 10 deg/s yaw every {1:0.00} s — "
                + "{2:0} times more often", sunSecondsPerRebuild, yawSecondsPerRebuild,
                sunSecondsPerRebuild / yawSecondsPerRebuild);

            Assert.True(sunSecondsPerRebuild / yawSecondsPerRebuild > 100.0,
                "the wind and the sun move at comparable rates, so the solar cadence would carry "
                + "over and this whole concern is misplaced");
        }
        [Theory]
        [InlineData(2.0)]
        [InlineData(5.0)]
        [InlineData(10.0)]
        [InlineData(20.0)]
        [InlineData(45.0)]

        public void AStaleWindwardMapDisagreesWithTheTruthByThisMuch(double degrees)
        {

            ThermalSimulation simulation = Structured();
            ThermalSolver solver = simulation.Solver;
            simulation.StepExact(1, Worlds.Space(Vector3.Forward));

            Vector3 truth = Vector3.Normalize(new Vector3(1f, 0.35f, 0.2f));
            double radians = degrees * Math.PI / 180.0;

            Matrix turn = Matrix.CreateFromAxisAngle(Vector3.Up, (float)radians);
            Vector3 stale = Vector3.Normalize(Vector3.TransformNormal(truth, turn));


            SunShadowMap exact = Built(solver, truth);

            SunShadowMap lagged = Built(solver, stale);

            int faces = 0;
            int differing = 0;
            double error = 0.0;

            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                BlockInstance block = solver.Nodes[i].Block;
                for (int face = 0; face < Face.Count; face++)
                {
                    float a = exact.FaceLitFraction(block, face);
                    float b = lagged.FaceLitFraction(block, face);

                    faces++;
                    error += Math.Abs(a - b);
                    if (Math.Abs(a - b) > 0.01f) differing++;
                }
            }

            output.WriteLine("{0,5:0.0} deg stale: {1,6} faces, {2,6} disagree ({3,5:0.0} %), "
                + "mean error {4:0.0000}",
                degrees, faces, differing, 100.0 * differing / faces, error / faces);

            Assert.True(faces > 0);
        }


        private static ThermalSimulation Structured()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.SolarSelfShadowing = true;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();

            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(16, 2, 16));

            builder.Fill(Catalog.HeavyArmor(), new Vector3I(2, 2, 2), new Vector3I(5, 12, 5));
            builder.Fill(Catalog.HeavyArmor(), new Vector3I(11, 2, 11), new Vector3I(14, 12, 14));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static SunShadowMap Built(ThermalSolver solver, Vector3 direction)
        {

            SunShadowMap map = new SunShadowMap();
            map.Restart(solver.Grid, direction, null);

            int guard = 0;
            while (map.IsRunning && guard++ < 100000) map.Step(solver.SunShadowBudget);

            Assert.True(map.IsBuilt, "the pass did not complete, so this compares nothing");
            return map;
        }

    }
}
