using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A ring of pipe becoming a loop, and the loop moving heat.
    ///
    /// <para>
    /// Two halves, and the first decides whether the second means anything: topology — what counts as
    /// a closed ring, that a ring is found once wherever the search starts, and that the loop's
    /// identity survives a rebuild so a saved temperature comes back to the right fluid — and
    /// transport, where the assertions are on energy rather than on temperature, because a loop that
    /// leaks energy still produces plausible-looking numbers.
    /// </para>
    /// </summary>
    public class CoolantLoopTests
    {

        private static HistoricalThermalSettings Isolated()
        {
            return Isolation.DeadWorld();
        }

        [Fact]

        public void AClosedPumpedRingIsDetected()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            Assert.Single(simulation.Solver.Loops);
            Assert.Equal(8, simulation.Solver.Loops[0].PipeCount);
            Assert.True(simulation.Solver.Loops[0].HasPump);
        }

        [Fact]

        public void ARingWithoutAPumpIsStillALoop()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildPumplessRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            Assert.Single(simulation.Solver.Loops);
            CoolantLoop loop = simulation.Solver.Loops[0];

            Assert.Equal(8, loop.PipeCount);
            Assert.False(loop.HasPump);
            Assert.Equal(0f, loop.FlowSegmentsPerSecond);
        }

        [Fact]

        public void AnOpenRunIsNotALoop()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 4; x++)
            {
                BlockModel model = x == 0 ? Catalog.CoolantPump() : Catalog.CoolantPipeStraight();
                builder.Place(model, new Vector3I(x, 0, 0),
                    PipeFitter.Orient(model, Vector3I.Left, Vector3I.Right));
            }

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            Assert.Empty(simulation.Solver.Loops);
        }

        [Fact]

        public void BreakingTheRingDestroysTheLoop()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring = PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            Assert.Single(simulation.Solver.Loops);

            simulation.RemoveBlock(ring[3]);
            simulation.RebuildAll();

            Assert.Empty(simulation.Solver.Loops);
        }

        [Fact]

        public void ClosingTheRingCreatesTheLoop()
        {
            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            List<BlockInstance> ring = PipeFitter.BuildRing(builder, cells);

            BlockInstance last = ring[ring.Count - 1];
            builder.Grid.Remove(last);
            builder.Placed.Remove(last);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            Assert.Empty(simulation.Solver.Loops);

            simulation.AddBlock(new BlockInstance(last.Model, last.Min, last.Orientation));
            simulation.RebuildAll();

            Assert.Single(simulation.Solver.Loops);
        }

        [Fact]

        public void ARingIsFoundOnlyOnceNoMatterWhereTheSearchStarts()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            Assert.Single(simulation.Solver.Loops);
            Assert.Equal(10, simulation.Solver.Loops[0].PipeCount);
        }

        [Fact]

        public void TwoSeparateRingsAreBothFound()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(new Vector3I(0, 5, 0), 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            Assert.Equal(2, simulation.Solver.Loops.Count);
        }

        [Fact]

        public void AMultiCellPumpIsWalkedEndToEnd()
        {
            GridBuilder builder = GridBuilder.Large();


            List<Vector3I> path = new List<Vector3I>();
            for (int z = 0; z < 5; z++) path.Add(new Vector3I(0, 0, z));
            for (int z = 4; z >= 0; z--) path.Add(new Vector3I(1, 0, z));

            BlockModel pump = Catalog.CoolantPumpLong();
            builder.Place(pump, new Vector3I(0, 0, 1), BlockOrientation.Identity);

            for (int i = 0; i < path.Count; i++)
            {
                if (i >= 1 && i <= 3) continue;

                Vector3I cell = path[i];
                Vector3I toPrevious = path[(i - 1 + path.Count) % path.Count] - cell;
                Vector3I toNext = path[(i + 1) % path.Count] - cell;

                BlockModel model = toPrevious == -toNext
                    ? Catalog.CoolantPipeStraight()
                    : Catalog.CoolantPipeCorner();

                builder.Place(model, cell, PipeFitter.Orient(model, toPrevious, toNext));
            }

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            Assert.Single(simulation.Solver.Loops);
            Assert.True(simulation.Solver.Loops[0].HasPump);

            Assert.Equal(8, simulation.Solver.Loops[0].PipeCount);
        }

        [Fact]

        public void LoopSignatureIsStableAcrossRebuilds()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            long first = simulation.Solver.Loops[0].Signature;

            simulation.RebuildAll();
            long second = simulation.Solver.Loops[0].Signature;

            Assert.Equal(first, second);
            Assert.NotEqual(0L, first);
        }

        [Fact]

        public void ALoopKeepsItsIdentityWhateverOrderItsPipesArrivedIn()
        {
            GridBuilder ordered = GridBuilder.Large();
            PipeFitter.BuildRing(ordered, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            GridBuilder shuffled = GridBuilder.Large();
            PipeFitter.BuildRing(shuffled, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));
            shuffled.ReorderPlacement(20260824);

            CoolantLoop first = ordered.BuildSimulation(Isolated()).Solver.Loops[0];
            CoolantLoop second = shuffled.BuildSimulation(Isolated()).Solver.Loops[0];

            Assert.NotEqual(0L, first.Signature);
            Assert.Equal(first.Signature, second.Signature);
            Assert.Equal(first.PipeCount, second.PipeCount);
        }

        [Fact]

        public void ARingOnePipeLongerIsADifferentLoopAndDoesNotTakeTheOldOnesTemperature()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];

            long before = loop.Signature;
            loop.Temperature = 400f;

            string saved = simulation.Save();

            GridBuilder grown = GridBuilder.Large();
            PipeFitter.BuildRing(grown, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3));

            ThermalSimulation after = grown.BuildSimulation(Isolated());
            CoolantLoop wider = after.Solver.Loops[0];

            Assert.NotEqual(before, wider.Signature);

            float untouched = wider.Temperature;
            after.Load(saved);

            Assert.Equal(untouched, after.Solver.Loops[0].Temperature, 3);
            Assert.NotEqual(400f, after.Solver.Loops[0].Temperature);
        }

        [Fact]

        public void ARebuildKeepsTheCoolantTemperature()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            simulation.Solver.Loops[0].Temperature = 777f;

            simulation.RebuildAll();

            Assert.Equal(777f, simulation.Solver.Loops[0].Temperature, 3);
        }

        [Fact]

        public void ASinkFaceCarriesMoreThanThePipesAlone()
        {

            float withSink = HeatDrawnFromABlockUnderTheRing(true);

            float withoutSink = HeatDrawnFromABlockUnderTheRing(false);

            Assert.True(withSink > withoutSink * 1.2f,
                "the sink face drew " + withSink + " W against " + withoutSink
                + " W from the pipes alone; a sink that adds nothing is a sink that is not there");
        }


        private static float HeatDrawnFromABlockUnderTheRing(bool sink)
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            if (sink) sinks[1] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.HeavyArmor(), cells[1] + Vector3I.Down);
            BlockInstance hot = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];

            Assert.Equal(sink ? loop.PipeCount + 1 : loop.PipeCount, loop.Links.Count);

            simulation.Solver.GetNode(hot).Temperature = 900f;
            simulation.StepExact(1, Worlds.Shadow());
            return loop.LastWattsAbsorbed;
        }

        [Fact]

        public void CoolantMovesHeatFromASinkFaceIntoTheFluid()
        {
            GridBuilder builder = GridBuilder.Large();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3), -1, sinks);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, -1, 0));
            BlockInstance hot = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            Assert.Single(simulation.Solver.Loops);

            CoolantLoop loop = simulation.Solver.Loops[0];
            ThermalNode hotNode = simulation.Solver.GetNode(hot);
            hotNode.Temperature = 900f;

            float before = loop.Temperature;
            simulation.StepExact(20, Worlds.Shadow());

            Assert.True(loop.Temperature > before, "coolant should have absorbed heat");
            Assert.True(hotNode.Temperature < 900f, "the hot block should have given some up");
        }

        [Fact]

        public void CoolantTransportConservesEnergy()
        {
            GridBuilder builder = GridBuilder.Large();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;
            sinks[5] = Vector3I.Up;

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3), -1, sinks);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, -1, 0));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 1, 2));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            simulation.Solver.GetNodeAt(new Vector3I(1, -1, 0)).Temperature = 900f;

            float before = simulation.Solver.TotalEnergy;
            simulation.StepExact(200, Worlds.Shadow());
            float after = simulation.Solver.TotalEnergy;

            Assert.Equal(1f, after / before, 3);
        }

        [Fact]

        public void AWorkingLoopReportsGrossFlowNotItsNearZeroNet()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;
            sinks[5] = Vector3I.Up;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5);
            PipeFitter.BuildRing(builder, cells, -1, sinks);

            builder.Place(Catalog.Reactor(), cells[1] + Vector3I.Down).Producing(200000f);
            builder.Place(Catalog.Radiator(), cells[5] + Vector3I.Up);


            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;

            ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 293.15f);
            CoolantLoop loop = simulation.Solver.Loops[0];

            const int Chunk = 10000;
            const int Bound = 600000;

            int stepped = 0;
            while (stepped < Bound)
            {
                simulation.StepExact(Chunk, Worlds.Shadow());
                stepped += Chunk;

                if (loop.LastWattsAbsorbed > 1000f
                    && Math.Abs(loop.LastNetWatts) < loop.LastWattsAbsorbed * 0.1f) break;
            }

            Assert.True(stepped < Bound,
                "the loop had not reached balance after " + stepped + " steps: net "
                + loop.LastNetWatts + " W against " + loop.LastWattsAbsorbed + " W absorbed");

            Assert.True(loop.LastWattsAbsorbed > 1000f,
                "the loop reports drawing only " + loop.LastWattsAbsorbed + " W off a 200 kW reactor");
            Assert.True(loop.LastWattsRejected > 1000f,
                "the loop reports shedding only " + loop.LastWattsRejected + " W into a radiator");

            Assert.True(Math.Abs(loop.LastNetWatts) < loop.LastWattsAbsorbed * 0.1f,
                "net " + loop.LastNetWatts + " W against " + loop.LastWattsAbsorbed
                + " W absorbed: the loop has not reached balance, so this is not testing the case");
        }

        [Fact]

        public void TheReportedFlowAccountsForTheFluidsChangeInHeat()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.HeavyArmor(), cells[1] + Vector3I.Down);
            BlockInstance hot = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];
            simulation.Solver.GetNode(hot).Temperature = 900f;

            float before = loop.Temperature;
            simulation.StepExact(1, Worlds.Shadow());

            float reported = loop.LastNetWatts * simulation.Settings.StepSeconds;
            float actual = (loop.Temperature - before) * loop.ThermalMass;

            Assert.Equal(1f, reported / actual, 2);
        }

        [Fact]

        public void ALoopInBalanceWithItsSurroundingsReportsNothing()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];

            simulation.StepExact(10, Worlds.Shadow());

            Assert.Equal(0f, loop.LastWattsAbsorbed, 3);
            Assert.Equal(0f, loop.LastWattsRejected, 3);
        }

        [Fact]

        public void ALongerRingHoldsMoreCoolantAndCostsTheSamePerPipe()
        {
            CoolantLoop small, large;
            ThermalNode smallSink, largeSink;
            RingOverOneHotBlock(3, 3, out small, out smallSink);
            RingOverOneHotBlock(9, 9, out large, out largeSink);

            Assert.Equal(8, small.PipeCount);
            Assert.Equal(32, large.PipeCount);

            Assert.Equal(small.SegmentThermalMass, large.SegmentThermalMass, 3);

            Assert.Equal(small.ThermalMass * 4f, large.ThermalMass, 1);


            float perLink = TotalConductance(small) / small.Links.Count;
            Assert.Equal(perLink, TotalConductance(large) / large.Links.Count, 1);
        }

        [Fact]

        public void RingLengthDoesNotChangeWhatTheSolverPaysPerParcel()
        {
            CoolantLoop small, large;
            ThermalNode smallSink, largeSink;

            ThermalSimulation a = RingOverOneHotBlock(3, 3, out small, out smallSink);

            ThermalSimulation b = RingOverOneHotBlock(20, 20, out large, out largeSink);

            Assert.Equal(8, small.PipeCount);
            Assert.Equal(76, large.PipeCount);

            a.StepExact(1, Worlds.Shadow());
            b.StepExact(1, Worlds.Shadow());

            Assert.Equal(a.Solver.LastRequiredSubsteps, b.Solver.LastRequiredSubsteps, 2);
        }


        private static float TotalConductance(CoolantLoop loop)
        {
            float total = 0f;
            for (int i = 0; i < loop.Links.Count; i++) total += loop.Links[i].Conductance;
            return total;
        }


        private static ThermalSimulation RingOverOneHotBlock(int width, int depth,
            out CoolantLoop loop, out ThermalNode sink)
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, width, depth);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.HeavyArmor(), cells[1] + Vector3I.Down);
            BlockInstance hot = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            loop = simulation.Solver.Loops[0];

            Assert.Equal(loop.PipeCount + 1, loop.Links.Count);

            sink = simulation.Solver.GetNode(hot);
            sink.Temperature = 900f;
            return simulation;
        }

        [Fact]

        public void DisablingLoopsRemovesThemFromTheSolver()
        {

            HistoricalThermalSettings settings = Isolated();
            settings.EnableCoolantLoops = false;

            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            Assert.Empty(simulation.Solver.Loops);
        }

        [Fact]

        public void PipeAndPlateConductanceRespectTheirScalers()
        {

            GridModel grid = new GridModel(2.5f);
            BlockInstance pipe = grid.Add(Catalog.CoolantPipeStraight(), Vector3I.Zero);

            LoopThermalProperties properties = LoopThermalProperties.Default();
            float baseline = CoolantLoopBuilder.PipeConductance(grid, pipe, properties);

            properties.PipeContactMultiplier = 2f;
            float doubled = CoolantLoopBuilder.PipeConductance(grid, pipe, properties);

            Assert.Equal(baseline * 2f, doubled, 2);

            LoopThermalProperties plate = LoopThermalProperties.Default();
            Assert.True(CoolantLoopBuilder.PlateConductance(grid, plate) > 0f);
        }
    }
}
