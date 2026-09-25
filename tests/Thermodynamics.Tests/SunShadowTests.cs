using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A grid standing in its own light.
    ///
    /// The cheap solar model lights any face pointing at the sun, whatever is built in front of
    /// it. These pin the alternative: the shadow map's own behaviour first, then the thing that
    /// actually matters, which is that a block behind another block stops being heated.
    /// </summary>
    public class SunShadowMapTests
    {

        private static readonly Vector3 SunAlongX = new Vector3(1f, 0f, 0f);


        private static SunShadowMap Build(GridModel grid, Vector3 sun)
        {

            SunShadowMap map = new SunShadowMap();
            map.Restart(grid, sun);
            map.RunToCompletion();
            return map;
        }

        [Fact]

        public void ALoneCellIsLit()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Right));
            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Left));
        }

        [Fact]

        public void TheCellBehindAnotherIsShadowed()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.True(map.IsFaceLit(new Vector3I(1, 0, 0), Face.Right));
            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Right));
        }

        [Fact]

        public void ShadowFollowsTheSunAround()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, -SunAlongX);

            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Left));
            Assert.False(map.IsFaceLit(new Vector3I(1, 0, 0), Face.Left));
        }

        [Fact]

        public void CellsSideBySideAcrossTheSunAreBothLit()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(0, 1, 0));


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Right));
            Assert.True(map.IsFaceLit(new Vector3I(0, 1, 0), Face.Right));
        }

        [Fact]

        public void ACellTheMapNeverSawIsTreatedAsLit()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.True(map.IsLit(new Vector3I(0, 40, 0)));
        }

        [Fact]

        public void AnUnbuiltMapLightsEverything()
        {

            SunShadowMap map = new SunShadowMap();

            Assert.False(map.IsBuilt);
            Assert.True(map.IsLit(Vector3I.Zero));
            Assert.Equal(1f, map.FaceLitFraction(null, Face.Up), 5);
        }


        [Fact]

        public void TheInnerLayerOfAWallIsStillLitOnTheSidesThatFaceOut()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(2, 4, 1));


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            BlockInstance back = builder.Grid.GetAtCell(new Vector3I(0, 1, 0));

            Assert.Equal(0f, map.FaceLitFraction(back, Face.Right), 5);
            Assert.Equal(1f, map.FaceLitFraction(back, Face.Forward), 5);
            Assert.Equal(1f, map.FaceLitFraction(back, Face.Backward), 5);
        }

        [Fact]

        public void AFaceInsideARecessIsShadowedWhileTheWallAroundItIsLit()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 1, 1));
            builder.Place(Catalog.LightArmor(), new Vector3I(3, 1, 0));
            builder.Place(Catalog.LightArmor(), new Vector3I(3, 2, 0));


            SunShadowMap map = Build(builder.Grid, Vector3.Normalize(new Vector3(0.83f, 0.55f, 0f)));

            BlockInstance floor = builder.Grid.GetAtCell(new Vector3I(2, 0, 0));
            BlockInstance tower = builder.Grid.GetAtCell(new Vector3I(3, 2, 0));

            Assert.Equal(0f, map.FaceLitFraction(floor, Face.Up), 5);
            Assert.Equal(1f, map.FaceLitFraction(tower, Face.Up), 5);
        }

        [Fact]

        public void ALongBlockCanHaveOneEndInShadowAndTheOtherInTheOpen()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmorBar(3), Vector3I.Zero);
            BlockInstance bar = builder.Last;
            builder.Place(Catalog.LightArmor(), new Vector3I(2, 1, 0));


            SunShadowMap map = Build(builder.Grid, Vector3.Normalize(new Vector3(0f, 1f, 0f)));

            Assert.Equal(2f / 3f, map.FaceLitFraction(bar, Face.Up), 3);
        }

        [Fact]

        public void ARestartIsOnlyNeededOnceTheSunHasMovedFarEnough()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Vector3 nudged = Vector3.Normalize(new Vector3(1f, 0.005f, 0f));
            Vector3 moved = Vector3.Normalize(new Vector3(1f, 1f, 0f));

            Assert.False(map.NeedsRestart(ref nudged, 0.99939f));
            Assert.True(map.NeedsRestart(ref moved, 0.99939f));
        }



        private static void AssertMatchesReference(GridModel grid, Vector3 sun)
        {

            SunShadowMap map = Build(grid, sun);

            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                Vector3I[] cells = blocks[i].Cells;
                for (int c = 0; c < cells.Length; c++)
                {
                    for (int face = 0; face < Face.Count; face++)
                    {
                        Vector3I outside = cells[c] + Face.Offsets[face];
                        if (grid.IsOccupied(outside)) continue;

                        Assert.Equal(Reference.Lit(grid, outside, sun), map.IsFaceLit(cells[c], face));
                    }
                }
            }
        }


        public static IEnumerable<object[]> ObliqueSuns()
        {
            yield return new object[] { new Vector3(1f, 0f, 0f) };
            yield return new object[] { new Vector3(0.9004f, 0.1619f, -0.4038f) };
            yield return new object[] { new Vector3(0.83f, 0.41f, 0.37f) };
            yield return new object[] { new Vector3(0.97f, 0.31f, 0.19f) };
            yield return new object[] { new Vector3(0.51f, 0.86f, 0.23f) };
            yield return new object[] { new Vector3(0.29f, 0.11f, 0.95f) };
            yield return new object[] { new Vector3(-0.71f, 0.13f, -0.69f) };
            yield return new object[] { new Vector3(-0.19f, -0.96f, 0.11f) };
        }

        [Theory]
        [MemberData(nameof(ObliqueSuns))]

        public void ASolidWallMatchesTheReferenceAtAnySunAngle(Vector3 sun)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), new Vector3I(-3, 0, 0), new Vector3I(1, 7, 4));

            AssertMatchesReference(builder.Grid, sun);
        }

        [Theory]
        [MemberData(nameof(ObliqueSuns))]

        public void AStaircaseMatchesTheReferenceAtAnySunAngle(Vector3 sun)
        {
            GridBuilder builder = GridBuilder.Large();
            for (int i = 0; i < 6; i++)
            {
                builder.Place(Catalog.LightArmor(), new Vector3I(i, i, 0));
            }

            AssertMatchesReference(builder.Grid, sun);
        }

        [Theory]
        [MemberData(nameof(ObliqueSuns))]

        public void ASparseLatticeMatchesTheReferenceAtAnySunAngle(Vector3 sun)
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    builder.Place(Catalog.LightArmor(), new Vector3I(x * 3, y, 0));
                }
            }

            AssertMatchesReference(builder.Grid, sun);
        }

        [Theory]
        [MemberData(nameof(ObliqueSuns))]

        public void AHollowBoxMatchesTheReferenceAtAnySunAngle(Vector3 sun)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(0, 0, 0), new Vector3I(4, 4, 4));

            AssertMatchesReference(builder.Grid, sun);
        }

        [Fact]

        public void AnExactlyDiagonalSunGrazesPastTheCornerRatherThanBeingStoppedByIt()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 1, 0));
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            Vector3 sun = Vector3.Normalize(new Vector3(1f, 1f, 0f));

            SunShadowMap map = Build(builder.Grid, sun);

            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Right));
            Assert.Equal(
                Reference.Lit(builder.Grid, new Vector3I(1, 0, 0), sun),
                map.IsFaceLit(Vector3I.Zero, Face.Right));
        }

        [Fact]

        public void ARayCannotSlipBetweenTwoBlocksThatTouchOnlyAlongAnEdge()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));
            builder.Place(Catalog.LightArmor(), new Vector3I(2, 1, 0));
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, Vector3.Normalize(new Vector3(1f, 1f, 0f)));

            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Right));
        }


        [Fact]

        public void SteppingInSmallBudgetsGivesTheSameAnswerAsRunningItAllAtOnce()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), new Vector3I(-3, 0, 0), new Vector3I(1, 7, 4));


            Vector3 sun = new Vector3(0.9004f, 0.1619f, -0.4038f);


            SunShadowMap whole = Build(builder.Grid, sun);


            SunShadowMap sliced = new SunShadowMap();
            sliced.Restart(builder.Grid, sun);

            int guard = 0;
            while (sliced.IsRunning && guard++ < 1000) sliced.Step(3);

            Assert.False(sliced.IsRunning);
            Assert.Equal(whole.ShadowedCount, sliced.ShadowedCount);

            IList<BlockInstance> blocks = builder.Grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                for (int face = 0; face < Face.Count; face++)
                {
                    Assert.Equal(
                        whole.FaceLitFraction(blocks[i], face),
                        sliced.FaceLitFraction(blocks[i], face));
                }
            }
        }

        [Fact]

        public void ThePreviousAnswerStaysReadableWhileANewPassRuns()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(3, 0, 0));
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.True(map.IsFaceLit(new Vector3I(3, 0, 0), Face.Right));
            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Left));

            map.Restart(builder.Grid, -SunAlongX);
            map.Step(1);

            Assert.True(map.IsRunning);
            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Left));

            map.RunToCompletion();
            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Left));
            Assert.False(map.IsFaceLit(new Vector3I(3, 0, 0), Face.Right));
        }

        [Fact]

        public void StepReportsCompletionExactlyOnce()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 1));


            SunShadowMap map = new SunShadowMap();
            map.Restart(builder.Grid, SunAlongX);

            int completions = 0;
            for (int i = 0; i < 50; i++)
            {
                if (map.Step(2)) completions++;
            }

            Assert.Equal(1, completions);
        }

        [Fact]

        public void TheSkinCellsOnTheBoxsOwnEdgeAreStillTracked()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x <= 3; x++) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            Assert.False(map.IsLit(new Vector3I(-1, 0, 0)));

            Assert.True(map.IsLit(new Vector3I(4, 0, 0)));
        }

        [Fact]

        public void ARestartedMapMatchesAFreshOneExactly()
        {
            GridBuilder a = GridBuilder.Large();
            GridBuilder b = GridBuilder.Large();
            a.PlaceCensus(LoadShapes.Build("ship", 2000));
            b.PlaceCensus(LoadShapes.Build("ship", 2000));


            Vector3 first = new Vector3(1f, 0.7f, 0.3f);

            Vector3 second = new Vector3(-0.4f, 1f, -0.8f);


            SunShadowMap restarted = Build(a.Grid, first);
            restarted.Restart(a.Grid, second);
            restarted.RunToCompletion();


            SunShadowMap fresh = Build(b.Grid, second);

            Assert.True(fresh.ShadowedCount > 100,
                "the fresh map shadows " + fresh.ShadowedCount + " cells, too few for agreement to mean anything");
            Assert.Equal(fresh.ShadowedCount, restarted.ShadowedCount);

            int litSomewhere = 0;
            int shadedSomewhere = 0;
            for (int i = 0; i < a.Placed.Count; i++)
            {
                for (int face = 0; face < Face.Count; face++)
                {
                    float expected = fresh.FaceLitFraction(b.Placed[i], face);
                    float actual = restarted.FaceLitFraction(a.Placed[i], face);
                    Assert.True(expected == actual,
                        "block " + i + " face " + face + " reads " + actual + " restarted and " + expected + " fresh");
                    if (expected > 0f) litSomewhere++;
                    if (expected < 1f) shadedSomewhere++;
                }
            }
            Assert.True(litSomewhere > 0 && shadedSomewhere > 0,

                "the fixture never exercised both classes of answer (`E8`)");
        }

        [Fact]

        public void AReusedMapCoversTheSkinAGrowingGridAdds()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x <= 2; x++) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));


            SunShadowMap map = Build(builder.Grid, SunAlongX);

            for (int x = 3; x <= 6; x++) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));

            map.Restart(builder.Grid, SunAlongX);
            map.RunToCompletion();

            Assert.False(map.IsLit(new Vector3I(-1, 0, 0)));
            Assert.True(map.IsLit(new Vector3I(7, 0, 0)));
            Assert.False(map.IsFaceLit(new Vector3I(3, 0, 0), Face.Right));
        }

        [Fact]

        public void AWarmRebuildAllocatesNothing()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));


            SunShadowMap map = Build(builder.Grid, SunAlongX);
            map.Restart(builder.Grid, SunAlongX);
            map.RunToCompletion();
            Assert.True(map.ShadowedCount > 100,
                "the map shadows " + map.ShadowedCount + " cells, so a rebuild is not doing hull-sized work");

            long before = GC.GetAllocatedBytesForCurrentThread();
            map.Restart(builder.Grid, SunAlongX);
            map.RunToCompletion();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < 16 * 1024,
                "a warm rebuild allocated " + allocated + " B; the sets are being bought again");
        }
    }

    /// <summary>
    /// A grid standing in its own light, at the level of what it does to a temperature.
    ///
    /// <para>
    /// SunShadowMapTests checks the map's geometry; this checks that the map reaches the solver. A
    /// block behind another stops being heated, a lone block is unaffected, the shadow follows a
    /// moving sun, and a block built into an existing shadow is picked up without the sun having to
    /// move.
    /// </para>
    /// </summary>
    public class SolarSelfShadowingTests
    {

        private static EnvironmentSample Sun()
        {
            return Worlds.Space(new Vector3(1f, 0f, 0f));
        }


        private static ThermalSettings Solar(bool selfShadowing)
        {
            ThermalSettings settings = Fixture.EnvironmentOnly();
            settings.EnableSolarHeat = true;
            settings.SolarSelfShadowing = selfShadowing;
            return settings;
        }


        private static ThermalSimulation Pair(ThermalSettings settings, out ThermalNode front, out ThermalNode back)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(3, 0, 0));
            BlockInstance sunward = builder.Last;
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            BlockInstance shaded = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;

            front = simulation.Solver.GetNode(sunward);
            back = simulation.Solver.GetNode(shaded);
            return simulation;
        }

        [Fact]

        public void TheCheapModelHeatsABlockStandingBehindAnother()
        {
            ThermalNode front, back;

            ThermalSimulation simulation = Pair(Solar(false), out front, out back);

            simulation.StepExact(1, Sun());

            Assert.True(front.LastSolarWatts > 0f);
            Assert.True(back.LastSolarWatts > 0f);
        }

        [Fact]

        public void SelfShadowingStopsTheBlockBehindFromBeingHeated()
        {
            ThermalNode front, back;

            ThermalSimulation simulation = Pair(Solar(true), out front, out back);

            simulation.StepExact(1, Sun());

            Assert.True(front.LastSolarWatts > 0f);
            Assert.Equal(0f, back.LastSolarWatts, 5);
        }

        [Fact]

        public void SelfShadowingLeavesALoneBlockAlone()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation shadowed = builder.BuildSimulation(Solar(true), 293.15f);
            shadowed.Solver.CollectDiagnostics = true;
            shadowed.StepExact(1, Sun());

            ThermalSimulation cheap = builder.BuildSimulation(Solar(false), 293.15f);
            cheap.Solver.CollectDiagnostics = true;
            cheap.StepExact(1, Sun());

            Assert.Equal(
                cheap.Solver.Nodes[0].LastSolarWatts,
                shadowed.Solver.Nodes[0].LastSolarWatts,
                4);
        }

        [Fact]

        public void ShadowFollowsTheSunWhenItMoves()
        {
            ThermalNode front, back;

            ThermalSimulation simulation = Pair(Solar(true), out front, out back);

            simulation.StepExact(1, Sun());
            Assert.Equal(0f, back.LastSolarWatts, 5);

            simulation.StepExact(1, Worlds.Space(new Vector3(-1f, 0f, 0f)));

            Assert.True(back.LastSolarWatts > 0f);
            Assert.Equal(0f, front.LastSolarWatts, 5);
        }

        [Fact]

        public void ABlockBuiltIntoTheShadowIsPickedUpWithoutTheSunMoving()
        {

            ThermalSettings settings = Solar(true);

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.StepExact(1, Sun());

            ThermalNode lone = simulation.Solver.Nodes[0];
            Assert.True(lone.LastSolarWatts > 0f);

            simulation.AddBlock(

                new BlockInstance(Catalog.LightArmor(), new Vector3I(3, 0, 0), BlockOrientation.Identity));
            simulation.RebuildAll();

            simulation.StepExact(1, Sun());

            Assert.Equal(0f, lone.LastSolarWatts, 5);
        }

    }
}
