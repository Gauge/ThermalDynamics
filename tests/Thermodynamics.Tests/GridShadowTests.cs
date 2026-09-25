using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// One grid's shadow falling on another's faces.
    ///
    /// The interesting part is not that a shadow exists — the whole-grid occlusion ray already knew
    /// that — but where it lands. A station overhead should darken the hull under it and nothing
    /// else, and that means reasoning about two cell lattices that share no axes, no origin and no
    /// scale. The ray is carried into the occluder's frame and walked there, so these check the
    /// transform as much as the walk: offset, rotation, and the shadow moving when the occluder
    /// does.
    /// </summary>
    public class GridShadowTests
    {

        private static readonly Vector3 Up = new Vector3(0f, 1f, 0f);


        private static SunShadowMap.Occluder Above(GridModel model, Vector3D offsetInCells)
        {
            return new SunShadowMap.Occluder
            {
                Model = model,
                Id = 1,

                ToOccluder = MatrixD.CreateTranslation(-offsetInCells),
            };
        }


        private static SunShadowMap Build(GridModel grid, Vector3 sun, params SunShadowMap.Occluder[] others)
        {

            SunShadowMap map = new SunShadowMap();
            map.Restart(grid, sun, others);
            map.RunToCompletion();
            return map;
        }

        [Fact]

        public void APlateOverheadShadesTheFacesUnderItAndNoOthers()
        {
            GridBuilder floor = GridBuilder.Large();
            floor.Fill(Catalog.LightArmor(), new Vector3I(-4, 0, 0), new Vector3I(5, 1, 1));

            GridBuilder plate = GridBuilder.Large();
            plate.Fill(Catalog.LightArmor(), new Vector3I(-1, 0, 0), new Vector3I(2, 1, 1));


            SunShadowMap map = Build(floor.Grid, Up, Above(plate.Grid, new Vector3D(0, 4, 0)));

            for (int x = -1; x <= 1; x++)
            {
                Assert.False(map.IsFaceLit(new Vector3I(x, 0, 0), Face.Up),
                    "cell " + x + " is under the plate and should be shaded");
            }

            for (int x = -4; x <= 4; x++)
            {
                if (x >= -1 && x <= 1) continue;
                Assert.True(map.IsFaceLit(new Vector3I(x, 0, 0), Face.Up),
                    "cell " + x + " is clear of the plate and should be lit");
            }
        }

        [Fact]

        public void TheShadowMovesWithTheOccluder()
        {
            GridBuilder floor = GridBuilder.Large();
            floor.Fill(Catalog.LightArmor(), new Vector3I(-4, 0, 0), new Vector3I(5, 1, 1));

            GridBuilder plate = GridBuilder.Large();
            plate.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap left = Build(floor.Grid, Up, Above(plate.Grid, new Vector3D(-3, 4, 0)));
            Assert.False(left.IsFaceLit(new Vector3I(-3, 0, 0), Face.Up));
            Assert.True(left.IsFaceLit(new Vector3I(3, 0, 0), Face.Up));


            SunShadowMap right = Build(floor.Grid, Up, Above(plate.Grid, new Vector3D(3, 4, 0)));
            Assert.True(right.IsFaceLit(new Vector3I(-3, 0, 0), Face.Up));
            Assert.False(right.IsFaceLit(new Vector3I(3, 0, 0), Face.Up));
        }

        [Fact]

        public void AnOccluderTurnedFortyFiveDegreesStillShadowsWhatItCovers()
        {
            GridBuilder floor = GridBuilder.Large();
            floor.Fill(Catalog.LightArmor(), new Vector3I(-4, 0, 0), new Vector3I(5, 1, 1));

            GridBuilder plate = GridBuilder.Large();
            plate.Fill(Catalog.LightArmor(), new Vector3I(-2, 0, -2), new Vector3I(3, 1, 3));

            MatrixD rotation = MatrixD.CreateRotationY(MathHelper.ToRadians(45));
            MatrixD toOccluder = MatrixD.CreateTranslation(new Vector3D(0, -4, 0)) * rotation;


            SunShadowMap map = Build(floor.Grid, Up, new SunShadowMap.Occluder
            {
                Model = plate.Grid,
                Id = 2,
                ToOccluder = toOccluder,
            });

            Assert.False(map.IsFaceLit(Vector3I.Zero, Face.Up));

            Assert.True(map.IsFaceLit(new Vector3I(4, 0, 0), Face.Up));
        }

        [Fact]

        public void AnOccluderBehindTheGridDoesNotShadowIt()
        {
            GridBuilder floor = GridBuilder.Large();
            floor.Fill(Catalog.LightArmor(), new Vector3I(-2, 0, 0), new Vector3I(3, 1, 1));

            GridBuilder plate = GridBuilder.Large();
            plate.Fill(Catalog.LightArmor(), new Vector3I(-2, 0, 0), new Vector3I(3, 1, 1));


            SunShadowMap map = Build(floor.Grid, Up, Above(plate.Grid, new Vector3D(0, -4, 0)));

            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Up));
        }

        [Fact]

        public void SelfShadowAndGridShadowBothApply()
        {
            GridBuilder ship = GridBuilder.Large();
            ship.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 2, 1));

            GridBuilder plate = GridBuilder.Large();
            plate.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(ship.Grid, Up, Above(plate.Grid, new Vector3D(3, 5, 0)));

            Assert.False(map.IsFaceLit(new Vector3I(0, 0, 0), Face.Up));

            Assert.True(map.IsFaceLit(new Vector3I(0, 1, 0), Face.Up));

            Assert.False(map.IsFaceLit(new Vector3I(3, 1, 0), Face.Up));
        }

        [Fact]

        public void NoOccludersIsTheSameAnswerAsBefore()
        {
            GridBuilder ship = GridBuilder.Large();
            ship.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 2, 1));


            SunShadowMap without = new SunShadowMap();
            without.Restart(ship.Grid, Up);
            without.RunToCompletion();


            SunShadowMap empty = Build(ship.Grid, Up);

            Assert.Equal(0, empty.OccluderCount);

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int face = 0; face < Face.Count; face++)
                    {

                        Vector3I cell = new Vector3I(x, y, 0);
                        Assert.Equal(without.IsFaceLit(cell, face), empty.IsFaceLit(cell, face));
                    }
                }
            }
        }

        [Fact]

        public void AnOccluderWithNoModelIsIgnoredRatherThanCrashing()
        {
            GridBuilder ship = GridBuilder.Large();
            ship.Place(Catalog.LightArmor(), Vector3I.Zero);


            SunShadowMap map = Build(ship.Grid, Up, new SunShadowMap.Occluder { Model = null, Id = 3 });

            Assert.Equal(0, map.OccluderCount);
            Assert.True(map.IsFaceLit(Vector3I.Zero, Face.Up));
        }
    }
}
