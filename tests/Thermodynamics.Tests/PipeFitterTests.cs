using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The harness itself, because a ring built wrong is a scenario that tests nothing and says it
    /// passed. `PipeFitter` exists to stop that happening by hand, and it had the failure it was
    /// written to prevent: a pump carries no sink ports, every rectangle's first straight run is
    /// index 1, `BuildRing` put the pump there by default, and a sink requested on index 1 was
    /// dropped without a word. Every scenario and test in the repository asked for exactly that.
    /// </summary>
    public class PipeFitterTests
    {

        private static ThermalSettings Isolated()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableDamage = false;
            return settings.Derive();
        }

        [Theory]
        [InlineData(3, 3)]
        [InlineData(4, 3)]
        [InlineData(5, 5)]
        [InlineData(9, 9)]
        [InlineData(20, 20)]

        public void TheFirstStraightRunOfEveryRectangleIsIndexOne(int width, int depth)
        {
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, width, depth);
            Assert.Equal(1, PipeFitter.FirstStraightIndex(cells));
        }

        [Fact]

        public void ASinkAskedForOnThePumpsCellIsHonouredByMovingThePump()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3);
            List<BlockInstance> ring = PipeFitter.BuildRing(builder, cells, -1, sinks);

            Assert.Single(ring[1].CoolantSinkPorts());

            Assert.NotEqual("CoolantPump", ring[1].Model.Name);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            Assert.Single(simulation.Solver.Loops);
            Assert.True(simulation.Solver.Loops[0].HasPump);
        }

        [Fact]

        public void EveryRequestedSinkBecomesALoopLink()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;
            sinks[5] = Vector3I.Up;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5);
            PipeFitter.BuildRing(builder, cells, -1, sinks);

            builder.Place(Catalog.HeavyArmor(), cells[1] + Vector3I.Down);
            builder.Place(Catalog.HeavyArmor(), cells[5] + Vector3I.Up);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];

            Assert.Equal(loop.PipeCount + 2, loop.Links.Count);
        }

        [Fact]

        public void AnExplicitPumpIndexThatCollidesWithASinkIsAnError()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[3] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3);

            ArgumentException error = Assert.Throws<ArgumentException>(
                () => PipeFitter.BuildRing(builder, cells, 3, sinks));

            Assert.Contains("test nothing", error.Message);
        }

        [Fact]

        public void ARingWithNoRoomLeftForAPumpSaysSo()
        {
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            for (int i = 0; i < cells.Count; i++) sinks[i] = Vector3I.Down;

            Assert.Throws<ArgumentException>(
                () => PipeFitter.FirstStraightIndexAvoiding(cells, sinks));
        }
    }
}
