using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Invariants that must hold whatever shape a grid takes.
    ///
    /// Every other test in this suite builds a solid or hollow box, because those are easy to
    /// reason about. Real grids are ships: irregular, mostly skin, with thin joints between
    /// heavy masses and conduction paths hundreds of blocks long. A solid cube is the best case
    /// for exposure, for bounding-volume density and for conduction distance all at once, so a
    /// bug that only appears on an awkward shape has nowhere to show up.
    ///
    /// These tests run the same assertions across <see cref="GridShapes.Catalogue"/>.
    /// </summary>
    [Trait("speed", "slow")]
    public class ShapeTests
    {

        public static IEnumerable<object[]> AllShapes()
        {
            foreach (KeyValuePair<string, HashSet<Vector3I>> shape in GridShapes.Catalogue())
            {
                yield return new object[] { shape.Key };
            }
        }


        private static HashSet<Vector3I> CellsFor(string name)
        {
            foreach (KeyValuePair<string, HashSet<Vector3I>> shape in GridShapes.Catalogue())
            {
                if (shape.Key == name) return shape.Value;
            }
            throw new ArgumentException("Unknown shape: " + name);
        }


        private static ThermalSettings Pace()
        {
            ThermalSettings settings = new ThermalSettings { Frequency = 4 };
            return settings.Derive();
        }


        private static ThermalSimulation Build(string name, ThermalSettings settings = null)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceAll(Catalog.LightArmor(), CellsFor(name));
            return builder.BuildSimulation(settings ?? Pace());
        }


        [Theory]
        [MemberData(nameof(AllShapes))]

        public void EveryShapeBuildsOneConnectedConductionGraph(string name)
        {

            ThermalSimulation simulation = Build(name);
            GridMetrics metrics = GridMetrics.Measure(simulation);

            Assert.True(metrics.NodeCount > 0, name + " built no nodes");
            Assert.Equal(0, metrics.IsolatedNodeCount);
            Assert.Equal(1, metrics.ComponentCount);
        }

        [Fact]

        public void AShipIsMostlySkinAndMostlyEmptySpaceWhereACubeIsNeither()
        {
            GridMetrics cube = GridMetrics.Measure(Build("solid-cube"));
            GridMetrics ship = GridMetrics.Measure(Build("ship"));

            Assert.True(ship.ExposedFraction > cube.ExposedFraction * 1.2f,
                "ship exposure " + ship.ExposedFraction + " vs cube " + cube.ExposedFraction);
            Assert.True(ship.ExposedFraction > 0.7f,
                "expected a ship to be mostly skin, got " + ship.ExposedFraction);

            Assert.Equal(1f, cube.BoundingFillRatio, 2);
            Assert.True(ship.BoundingFillRatio < 0.35f,
                "expected a ship's bounding box to be mostly empty, got " + ship.BoundingFillRatio);
        }

        [Fact]

        public void AnElongatedGridHasAConductionPathOrdersLongerThanACube()
        {
            GridMetrics cube = GridMetrics.Measure(Build("solid-cube"));
            GridMetrics stick = GridMetrics.Measure(Build("stick"));

            Assert.True(stick.Diameter > cube.Diameter * 5,
                "stick diameter " + stick.Diameter + " vs cube " + cube.Diameter);
        }


        [Theory]
        [MemberData(nameof(AllShapes))]

        public void EnergyIsConservedOnEveryShapeWhenIsolated(string name)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                EnableEnvironment = false,
                EnableSolarHeat = false,
                EnableFriction = false,
                EnableDamage = false,
                EnableCoolantLoops = false,
            }.Derive();


            ThermalSimulation simulation = Build(name, settings);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            nodes[nodes.Count / 3].Temperature = 900f;

            float before = simulation.Solver.TotalEnergy;
            simulation.StepExact(400, Worlds.Shadow());
            float after = simulation.Solver.TotalEnergy;

            Assert.Equal(before, after, Math.Abs(before) * 1e-3f);
        }

        [Theory]
        [MemberData(nameof(AllShapes))]

        public void NoShapeProducesInvalidTemperatures(string name)
        {

            ThermalSimulation simulation = Build(name);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            nodes[0].Temperature = 2000f;
            nodes[nodes.Count - 1].Temperature = ThermalConstants.MinimumTemperature;

            simulation.StepExact(600, Worlds.Shadow());

            for (int i = 0; i < nodes.Count; i++)
            {
                float t = nodes[i].Temperature;
                Assert.False(float.IsNaN(t), name + " produced NaN at node " + i);
                Assert.False(float.IsInfinity(t), name + " produced infinity at node " + i);
                Assert.True(t >= ThermalConstants.MinimumTemperature,
                    name + " produced " + t + "K at node " + i);
            }
        }

        [Theory]
        [MemberData(nameof(AllShapes))]

        public void EveryShapeReachesAUniformTemperatureWhenIsolated(string name)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                EnableEnvironment = false,
                EnableSolarHeat = false,
                EnableFriction = false,
                EnableDamage = false,
                EnableCoolantLoops = false,
            }.Derive();


            ThermalSimulation simulation = Build(name, settings);
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            nodes[0].Temperature = 800f;

            simulation.StepExact(200000, Worlds.Shadow());

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                min = Math.Min(min, nodes[i].Temperature);
                max = Math.Max(max, nodes[i].Temperature);
            }

            Assert.True(max - min < 1f,
                name + " did not equalise: spread " + (max - min) + "K after 200000 steps");
        }


        [Fact]

        public void HeatCrossesAThinNeckSymmetricallyInBothDirections()
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                EnableEnvironment = false,
                EnableSolarHeat = false,
                EnableFriction = false,
                EnableDamage = false,
                EnableCoolantLoops = false,
            }.Derive();


            float forward = TransferAcrossDumbbell(settings, hotEndFirst: true);

            float backward = TransferAcrossDumbbell(settings, hotEndFirst: false);

            Assert.Equal(forward, backward, Math.Abs(forward) * 1e-3f);
        }


        private static float TransferAcrossDumbbell(ThermalSettings settings, bool hotEndFirst)
        {
            GridBuilder builder = GridBuilder.Large();
            HashSet<Vector3I> cells = GridShapes.Dumbbell(5, 8);
            builder.PlaceAll(Catalog.LightArmor(), cells);
            ThermalSimulation simulation = builder.BuildSimulation(settings);


            Vector3I hot = hotEndFirst ? new Vector3I(2, 2, 0) : new Vector3I(2, 2, 17);

            Vector3I cold = hotEndFirst ? new Vector3I(2, 2, 17) : new Vector3I(2, 2, 0);

            simulation.Solver.SetAllTemperatures(300f);
            ThermalNode hotNode = simulation.Solver.GetNodeAt(hot);
            ThermalNode coldNode = simulation.Solver.GetNodeAt(cold);
            Assert.NotNull(hotNode);
            Assert.NotNull(coldNode);

            hotNode.Temperature = 900f;

            float coldBefore = coldNode.Temperature;
            simulation.StepExact(2000, Worlds.Shadow());
            return coldNode.Temperature - coldBefore;
        }


        [Fact]

        public void AnIrregularShapeGivesTheSameResultWhateverOrderItWasBuiltIn()
        {
            HashSet<Vector3I> cells = GridShapes.Ship(20, 7, 7);


            List<Vector3I> forward = new List<Vector3I>(cells);

            List<Vector3I> reversed = new List<Vector3I>(forward);
            reversed.Reverse();


            float a = SettledMeanTemperature(forward);

            float b = SettledMeanTemperature(reversed);

            Assert.Equal(a, b, 3);
        }


        private static float SettledMeanTemperature(List<Vector3I> cells)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceAll(Catalog.LightArmor(), cells);
            ThermalSimulation simulation = builder.BuildSimulation(Pace());

            simulation.Solver.SetAllTemperatures(300f);
            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 900f;
            simulation.StepExact(500, Worlds.Shadow());

            float total = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++) total += nodes[i].Temperature;
            return total / nodes.Count;
        }
    }
}
