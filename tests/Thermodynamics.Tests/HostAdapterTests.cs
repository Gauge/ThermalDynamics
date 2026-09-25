using Thermodynamics;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The parts of the game adapter that can be checked without the game: the gate that decides
    /// whether a tick is worth sampling the world for, and the coolant plumbing table.
    /// </summary>
    public class HostAdapterTests
    {


        private static ThermalSimulation Paced(int frequency, float speed)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Frequency = frequency;
            settings.SimulationSpeed = speed;
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            return builder.BuildSimulation(settings, 300f);
        }


        private static long Frames(ThermalSimulation simulation, int frames, float frameSeconds)
        {
            long before = simulation.StepsCompleted;
            for (int i = 0; i < frames; i++) simulation.Update(frameSeconds, Worlds.Shadow());
            return simulation.StepsCompleted - before;
        }

        [Fact]

        public void FrequencyIsStepsPerSimulatedSecond()
        {
            Assert.Equal(4, Frames(Paced(4, 1f), 60, 1f / 60f));
        }

        [Fact]

        public void SimulationSpeedMultipliesTheStepRate()
        {
            Assert.Equal(12, Frames(Paced(4, 3f), 60, 1f / 60f));
        }

        [Fact]

        public void ALongFrameDoesNotProduceABurstOfSteps()
        {
            Assert.Equal(1, Frames(Paced(4, 1f), 1, 60f));
        }

        [Fact]

        public void AZeroLengthFrameStepsNothing()
        {
            Assert.Equal(0, Frames(Paced(4, 1f), 10, 0f));
        }


        [Fact]

        public void StraightPipeLinksForwardAndBackward()
        {
            CoolantShape shape = ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Straight", Vector3I.One);

            Assert.NotNull(shape);
            Assert.False(shape.IsPump);
            Assert.Equal(2, shape.LinkPorts.Length);
            Assert.Equal(Vector3I.Forward, shape.LinkPorts[0].LocalDirection);
            Assert.Equal(Vector3I.Backward, shape.LinkPorts[1].LocalDirection);
            Assert.Empty(shape.SinkPorts);
        }

        [Fact]

        public void CornerPipeTurns()
        {
            CoolantShape shape = ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Corner", Vector3I.One);

            Assert.Equal(Vector3I.Forward, shape.LinkPorts[0].LocalDirection);
            Assert.Equal(Vector3I.Left, shape.LinkPorts[1].LocalDirection);
        }

        [Fact]

        public void SinkVariantsCarryTheirPlates()
        {
            Assert.Single(ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Straight_SingleSink", Vector3I.One).SinkPorts);
            Assert.Equal(2, ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Straight_DoubleSink", Vector3I.One).SinkPorts.Length);
            Assert.Single(ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Corner_SingleSink", Vector3I.One).SinkPorts);
            Assert.Equal(2, ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Corner_DoubleSink", Vector3I.One).SinkPorts.Length);
        }

        [Fact]

        public void BothGridSizesResolveToTheSamePlumbing()
        {
            CoolantShape large = ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Corner_DoubleSink", Vector3I.One);
            CoolantShape small = ThermalCoolantShapes.Get("Gauge_SG_CoolantPipe_Corner_DoubleSink", Vector3I.One);

            Assert.Equal(large.LinkPorts.Length, small.LinkPorts.Length);
            Assert.Equal(large.LinkPorts[1].LocalDirection, small.LinkPorts[1].LocalDirection);
            Assert.Equal(large.SinkPorts.Length, small.SinkPorts.Length);
        }

        [Fact]

        public void PumpPortsSitAtTheEndsOfTheBlockWhateverItsLength()
        {
            CoolantShape shortPump = ThermalCoolantShapes.Get("Gauge_LG_CoolantPump", Vector3I.One);
            CoolantShape longPump = ThermalCoolantShapes.Get("Gauge_SG_CoolantPump", new Vector3I(1, 1, 3));

            Assert.True(shortPump.IsPump);
            Assert.True(longPump.IsPump);

            Assert.Equal(Vector3I.Zero, shortPump.LinkPorts[0].LocalCell);
            Assert.Equal(Vector3I.Zero, shortPump.LinkPorts[1].LocalCell);

            Assert.Equal(Vector3I.Zero, longPump.LinkPorts[0].LocalCell);
            Assert.Equal(new Vector3I(0, 0, 2), longPump.LinkPorts[1].LocalCell);
        }

        [Fact]

        public void ABlockWithNoPlumbingHasNoShape()
        {
            Assert.Null(ThermalCoolantShapes.Get("LargeBlockArmorBlock", Vector3I.One));
            Assert.Null(ThermalCoolantShapes.Get("", Vector3I.One));
            Assert.Null(ThermalCoolantShapes.Get(null, Vector3I.One));
        }

        [Fact]

        public void PrefixStrippingLeavesUnprefixedNamesAlone()
        {
            Assert.Equal("CoolantPump", ThermalCoolantShapes.StripGridPrefix("Gauge_LG_CoolantPump"));
            Assert.Equal("CoolantPump", ThermalCoolantShapes.StripGridPrefix("Gauge_SG_CoolantPump"));
            Assert.Equal("SomeOtherMod_Pipe", ThermalCoolantShapes.StripGridPrefix("SomeOtherMod_Pipe"));
        }

        [Fact]

        public void ShapesFromTheTableFormAWorkingRing()
        {

            GridModel grid = new GridModel(2.5f);

            BlockModel straight = BlockModel.Solid("CoolantPipe_Straight", Vector3I.One, 220f, new BlockThermalProperties());
            straight.WithCoolant(ThermalCoolantShapes.Get("Gauge_LG_CoolantPipe_Straight", Vector3I.One));

            BlockModel pump = BlockModel.Solid("CoolantPump", Vector3I.One, 600f, new BlockThermalProperties());
            pump.WithCoolant(ThermalCoolantShapes.Get("Gauge_LG_CoolantPump", Vector3I.One));

            grid.Add(pump, Vector3I.Zero);
            grid.Add(straight, new Vector3I(0, 0, 1));

            Assert.Empty(CoolantLoopBuilder.FindLoops(grid, LoopThermalProperties.Default(), 293.15f));
        }
    }
}
