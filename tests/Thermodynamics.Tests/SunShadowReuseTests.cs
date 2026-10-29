using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>At most three blocks verify shadow-skin reuse, invalidation and completed-result ownership against independent rays.</summary>
    public class SunShadowReuseTests
    {
        [Fact]
        public void DirectionRestartsReuseSkinWhileTopologyAndGridIdentityInvalidateIt()
        {
            var model = BlockModel.Solid("skin", Vector3I.One, 100f, BlockThermalProperties.Default());
            var grid = new GridModel(2.5f);
            grid.Add(model, Vector3I.Zero);
            var other = new GridModel(2.5f);
            other.Add(model, new Vector3I(4, 0, 0));
            var occluders = new[] { new SunShadowMap.Occluder {
                Model = other, ToOccluder = MatrixD.Identity } };
            var map = new SunShadowMap();

            map.Restart(grid, Vector3.UnitX);
            Assert.Equal(Skin(grid).Count, map.PendingCells);
            var retainedCells = Field<List<Vector3I>>(map, "pending");
            var retainedEnumerator = retainedCells.GetEnumerator();
            Finish(map, grid, Vector3.UnitX);
            // An enumerator survives only if completion/restart neither clears nor rebuilds
            // the retained skin; this checks reuse without a wall-clock timing assertion.
            Assert.True(retainedEnumerator.MoveNext());

            map.Restart(grid, Vector3.UnitX, occluders);
            Assert.True(retainedEnumerator.MoveNext());
            Assert.Equal(1, map.OccluderCount);
            Assert.False(map.Step(1));
            Assert.Equal(Skin(grid).Count - 1, map.PendingCells);
            AssertPublished(map, grid, Vector3.UnitX);
            Assert.True(map.IsLit(Vector3I.UnitX));
            Finish(map, grid, Vector3.UnitX, other);
            Assert.False(map.IsLit(Vector3I.UnitX));

            map.Restart(grid, -Vector3.UnitX);
            Assert.Equal(0, map.OccluderCount);
            Finish(map, grid, -Vector3.UnitX);

            map.Restart(grid, Vector3.UnitX, occluders);
            map.Step(1);
            map.Restart(null, Vector3.UnitX, occluders);
            AssertInactive(map);
            foreach (Vector3 invalid in new[] { Vector3.Zero,
                new Vector3(float.NaN, 0f, 0f), new Vector3(float.PositiveInfinity, 0f, 0f) })
            {
                map.Restart(grid, invalid, occluders);
                AssertInactive(map);
                AssertPublished(map, grid, -Vector3.UnitX);
            }
            map.Restart(grid, Vector3.UnitX);
            Assert.True(retainedEnumerator.MoveNext());
            Finish(map, grid, Vector3.UnitX);

            Assert.Equal(grid.Version, other.Version);
            map.Restart(other, -Vector3.UnitX);
            Assert.True(Skin(other).SetEquals(retainedCells));
            AssertPublished(map, grid, Vector3.UnitX);
            Finish(map, other, -Vector3.UnitX);

            map.Restart(grid, Vector3.UnitX);
            Finish(map, grid, Vector3.UnitX);
            var added = grid.Add(model, Vector3I.UnitX);
            map.Restart(grid, Vector3.UnitX);
            Assert.Equal(10, map.PendingCells);
            Assert.True(Skin(grid).SetEquals(retainedCells));
            Finish(map, grid, Vector3.UnitX);
            grid.Remove(added);
            map.Restart(grid, -Vector3.UnitX);
            Assert.Equal(6, map.PendingCells);
            Finish(map, grid, -Vector3.UnitX);

            map.Clear();
            AssertInactive(map);
            Assert.False(map.IsBuilt);
            Assert.Empty(retainedCells);
            Assert.Null(Field<GridModel>(map, "skinGrid"));
            Assert.Null(Field<GridModel>(map, "grid"));
            Assert.Null(Field<GridModel>(map, "resultGrid"));
            map.Restart(grid, Vector3.UnitX);
            Assert.Equal(6, map.PendingCells);
            Finish(map, grid, Vector3.UnitX);
        }

        [Fact]
        public void ChangedOccludersReleaseDormantReferencesWithoutLosingPublishedShadows()
        {
            var settings = new ThermalSettings { EnableEnvironment = true, EnableRadiation = false,
                EnableConvection = true, EnableConduction = false, EnableSolarHeat = true,
                SolarSelfShadowing = true, EnableWindwardShielding = true, EnableWasteHeat = false,
                EnableDamage = false, EnableCoolantLoops = false, EnableRoomAir = false,
                EnableHeatPumps = false, HeatTimeScale = 1f, MaxSubsteps = 1, MaxSubstepsPerBlock = 0 };
            settings.Derive();
            var model = BlockModel.Solid("skin", Vector3I.One, 100f,
                new BlockThermalProperties { SpecificHeat = 100f, Conductivity = 0f,
                    Emissivity = 0f, SolarAbsorptivity = 0f });
            var grid = new GridModel(2.5f);
            var simulation = new ThermalSimulation(settings, grid);
            simulation.AddBlock(new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            var other = new GridModel(2.5f);
            other.Add(model, new Vector3I(4, 0, 0));
            var solver = simulation.Solver;
            solver.SunOccluders.Add(new SunShadowMap.Occluder {
                Model = other, ToOccluder = MatrixD.Identity });
            solver.MarkSunOccludersChanged();
            solver.SunShadowBudget = int.MaxValue;
            solver.SunLitBudget = 0;
            var environment = EnvironmentState.Vacuum(300f);
            environment.SolarEnergy = 100f;
            environment.IsSolarOccluded = false;
            environment.SunDirectionLocal = Vector3.UnitX;
            environment.AtmosphereFactor = 1f;
            environment.ConvectionCoefficient = 1f;
            environment.WindSpeed = 1f;
            environment.WindDirectionLocal = -Vector3.UnitX;
            solver.Step(.01f, environment);
            Assert.True(solver.SunLitRefreshPending);

            var sunSkin = Field<List<Vector3I>>(solver.SunShadow, "pending").GetEnumerator();
            var windSkin = Field<List<Vector3I>>(solver.WindShadow, "pending").GetEnumerator();
            foreach (var map in new[] { solver.SunShadow, solver.WindShadow })
            {
                AssertPublished(map, grid, Vector3.UnitX, other);
                Assert.False(map.IsLit(Vector3I.UnitX));
                Assert.Equal(1, map.OccluderCount);
                map.Restart(grid, Vector3.UnitY, solver.SunOccluders);
                Assert.False(map.Step(1));
                Assert.True(map.IsRunning);
            }

            solver.SunOccluders.Clear();
            solver.MarkSunOccludersChanged();
            foreach (var map in new[] { solver.SunShadow, solver.WindShadow })
            {
                AssertInactive(map);
                Assert.Empty(Field<List<Vector3D>>(map, "occluderSun"));
                Assert.Same(grid, Field<GridModel>(map, "resultGrid"));
                Assert.Same(grid, Field<GridModel>(map, "skinGrid"));
                AssertPublished(map, grid, Vector3.UnitX, other);
                map.RunToCompletion();
                AssertPublished(map, grid, Vector3.UnitX, other);
            }
            Assert.True(sunSkin.MoveNext());
            Assert.True(windSkin.MoveNext());

            // The public drain must publish the completed blocked result, never the
            // canceled UnitY pass or an all-lit replacement from clearing the whole map.
            solver.FinishSunLit();
            Assert.False(solver.SunLitRefreshPending);
            Assert.Equal(0f, solver.SunLitFraction(0, Face.Right));

            // While the new unblocked map is still building, wind publication also
            // finishes against the old result. A subsequent pass publishes the new one.
            solver.SunShadowBudget = 1;
            solver.SunLitBudget = 1;
            solver.Step(.01f, environment);
            var wind = (float[])typeof(ThermalSolver).GetField("nodeWindLit",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
            Assert.True(solver.SunShadow.IsRunning);
            Assert.True(solver.WindShadow.IsRunning);
            Assert.Equal(0f, wind[Face.Right]);
            solver.SunShadowBudget = int.MaxValue;
            solver.Step(.01f, environment);
            Assert.Equal(1f, solver.SunLitFraction(0, Face.Right));
            Assert.Equal(1f, wind[Face.Right]);
            AssertPublished(solver.SunShadow, grid, Vector3.UnitX);
            AssertPublished(solver.WindShadow, grid, Vector3.UnitX);
            Assert.True(sunSkin.MoveNext());
            Assert.True(windSkin.MoveNext());
        }

        private static T Field<T>(SunShadowMap map, string name)
        {
            return (T)typeof(SunShadowMap).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);
        }

        private static HashSet<Vector3I> Skin(GridModel grid)
        {
            var skin = new HashSet<Vector3I>();
            foreach (var block in grid.Blocks)
                foreach (var cell in block.Cells)
                    for (int face = 0; face < Face.Count; face++)
                    {
                        Vector3I outside = cell + Face.Offsets[face];
                        if (!grid.IsOccupied(outside)) skin.Add(outside);
                    }
            return skin;
        }

        private static void AssertInactive(SunShadowMap map)
        {
            Assert.False(map.IsRunning);
            Assert.Equal(0, map.PendingCells);
            Assert.Equal(0, map.OccluderCount);
            Assert.False(map.Step(1));
        }

        private static void Finish(SunShadowMap map, GridModel grid, Vector3 sun, GridModel occluder = null)
        {
            while (map.IsRunning) map.Step(2);
            Assert.Equal(0, map.PendingCells);
            Assert.False(map.Step(1));
            AssertPublished(map, grid, sun, occluder);
        }

        private static void AssertPublished(SunShadowMap map, GridModel grid, Vector3 sun,
            GridModel occluder = null)
        {
            Assert.True(map.IsBuilt);
            Assert.Equal(sun, map.SunDirection);
            int shadowed = 0;
            foreach (Vector3I outside in Skin(grid))
            {
                bool lit = Reference.Lit(grid, outside, sun)
                    && (occluder == null || Reference.Lit(occluder, outside, sun));
                Assert.Equal(lit, map.IsLit(outside));
                if (!lit) shadowed++;
            }
            Assert.Equal(shadowed, map.ShadowedCount);
        }
    }
}
