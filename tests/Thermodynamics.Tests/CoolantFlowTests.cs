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
    /// How the coolant is carried round the ring.
    ///
    /// The fluid does not move: the ring's origin does. Parcels sit in a fixed array and each pipe
    /// reads the parcel currently passing through it. Because a pipe's index is a whole number, the
    /// rounded offset collapses to one integer shift shared by every pipe — which is what makes it a
    /// bijection at any speed, exact, and free of the stability limit a blended scheme has.
    /// </summary>
    public class CoolantFlowTests
    {

        private static HistoricalThermalSettings Isolated(float segmentsPerSecond = 4f)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static ThermalSimulation Ring(int width, int depth, float segmentsPerSecond,
            out CoolantLoop loop)
        {
            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, width, depth);
            PipeFitter.BuildRing(builder, cells);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            loop = simulation.Solver.Loops[0];

            loop.Properties.LargeGridFlowRate = segmentsPerSecond * loop.ParcelLengthMetres;
            loop.Properties.SmallGridFlowRate = loop.Properties.LargeGridFlowRate;
            loop.RefreshFlow();
            return simulation;
        }

        [Fact]

        public void EachGridSizeHasItsOwnFlowRate()
        {

            CoolantLoop large = RingOfSize(Catalog.LargeGridSize);

            CoolantLoop small = RingOfSize(Catalog.SmallGridSize);

            Assert.Equal(10f, Math.Abs(large.FlowMetresPerSecond), 3);
            Assert.Equal(10f, Math.Abs(small.FlowMetresPerSecond), 3);

            Assert.Equal(4f, Math.Abs(large.FlowSegmentsPerSecond), 3);
            Assert.Equal(20f, Math.Abs(small.FlowSegmentsPerSecond), 3);

            small.Properties.SmallGridFlowRate = 2f;
            large.Properties.SmallGridFlowRate = 2f;
            small.RefreshFlow();
            large.RefreshFlow();

            Assert.Equal(2f, Math.Abs(small.FlowMetresPerSecond), 3);
            Assert.Equal(10f, Math.Abs(large.FlowMetresPerSecond), 3);
        }


        private static CoolantLoop RingOfSize(float gridSize)
        {

            GridBuilder builder = new GridBuilder(gridSize);
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];
            loop.RefreshFlow();
            return loop;
        }

        [Fact]

        public void TheParcelMappingIsABijectionAtEveryRotation()
        {
            CoolantLoop loop;

            ThermalSimulation simulation = Ring(5, 5, 4f, out loop);

            int count = loop.PipeCount;
            Assert.Equal(16, count);

            for (int step = 0; step < 200; step++)
            {
                simulation.StepExact(1, Worlds.Shadow());

                bool[] seen = new bool[count];
                for (int pipe = 0; pipe < count; pipe++)
                {
                    int parcel = loop.ParcelOf(pipe);

                    Assert.InRange(parcel, 0, count - 1);
                    Assert.False(seen[parcel],
                        "two pipes read parcel " + parcel + " at step " + step);
                    seen[parcel] = true;
                }
            }
        }

        [Fact]

        public void RotationAloneMovesHeatWithoutChangingIt()
        {
            CoolantLoop loop;

            ThermalSimulation simulation = Ring(5, 5, 4f, out loop);

            for (int i = 0; i < loop.PipeCount; i++)
            {
                loop.SetSegmentTemperature(i, 250f + (i * 40f));
            }

            float before = loop.Energy;
            float hottestBefore = loop.HottestSegment;
            float coldestBefore = loop.ColdestSegment;

            for (int i = 0; i < 500; i++) loop.Advect(1f / 24f);

            Assert.Equal(before, loop.Energy, 3);
            Assert.Equal(hottestBefore, loop.HottestSegment, 3);
            Assert.Equal(coldestBefore, loop.ColdestSegment, 3);
        }

        [Fact]

        public void AHotParcelTravelsRoundTheRingIntact()
        {
            CoolantLoop loop;
            Ring(5, 5, 4f, out loop);

            int count = loop.PipeCount;
            loop.Temperature = 300f;
            loop.SetSegmentTemperature(0, 900f);

            float peak = loop.HottestSegment;
            Assert.True(peak > 400f, "the pulse should start hot: " + peak);


            int startPipe = PipeHoldingThePulse(loop);

            for (int i = 0; i < count / 4; i++) loop.Advect(1f / 4f);


            int nowPipe = PipeHoldingThePulse(loop);

            Assert.NotEqual(startPipe, nowPipe);
            Assert.Equal(peak, loop.HottestSegment, 3);
        }


        private static int PipeHoldingThePulse(CoolantLoop loop)
        {
            int worst = 0;
            float best = float.MinValue;
            for (int i = 0; i < loop.PipeCount; i++)
            {
                if (loop.SegmentTemperature(i) <= best) continue;
                best = loop.SegmentTemperature(i);
                worst = i;
            }
            return worst;
        }

        [Theory]
        [InlineData(1f)]
        [InlineData(4f)]
        [InlineData(50f)]
        [InlineData(5000f)]

        public void FlowSpeedDoesNotCostSubsteps(float segmentsPerSecond)
        {
            CoolantLoop slow, fast;

            ThermalSimulation a = Ring(5, 5, 1f, out slow);

            ThermalSimulation b = Ring(5, 5, segmentsPerSecond, out fast);

            a.StepExact(20, Worlds.Shadow());
            b.StepExact(20, Worlds.Shadow());

            Assert.Equal(a.Solver.LastRequiredSubsteps, b.Solver.LastRequiredSubsteps, 3);

            Assert.False(float.IsNaN(fast.Temperature));
            Assert.False(float.IsInfinity(fast.Temperature));
        }

        [Fact]

        public void AStoppedPumpLeavesTheFarSideOfTheRingCold()
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 6, 5);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(125000f);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Enabled = false;
            loop.RefreshFlow();
            Assert.Equal(0f, loop.FlowSegmentsPerSecond);

            simulation.StepExact(LabClock.Steps(2000), Worlds.Shadow());

            float spread = loop.HottestSegment - loop.ColdestSegment;
            Assert.True(spread > 25f,
                "with nothing circulating the ring should be unevenly hot, spread was " + spread + " K");

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Enabled = true;
            loop.RefreshFlow();
            simulation.StepExact(LabClock.Steps(2000), Worlds.Shadow());

            float mixed = loop.HottestSegment - loop.ColdestSegment;
            Assert.True(mixed < spread * 0.5f,
                "circulating should even the ring out: " + spread + " K then " + mixed + " K");
        }
    
        [Fact]

        public void HalfSpeedIsAboutSeventyPercentOfTheFlow()
        {
            CoolantLoop loop;
            Ring(6, 5, 4f, out loop);

            float full = Math.Abs(loop.FlowSegmentsPerSecond);
            Assert.True(full > 0f);

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Speed = 0.5f;
            loop.RefreshFlow();
            float half = Math.Abs(loop.FlowSegmentsPerSecond);

            Assert.True(half < full);
            Assert.Equal(full * (float)Math.Sqrt(0.5), half, 3);

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Speed = 0f;
            loop.RefreshFlow();
            Assert.Equal(0f, loop.FlowSegmentsPerSecond);
        }

        [Fact]

        public void AChangedPumpSettingReachesTheFluidOnlyAfterARefresh()
        {
            CoolantLoop loop;
            Ring(6, 5, 4f, out loop);

            float before = loop.FlowSegmentsPerSecond;
            Assert.True(Math.Abs(before) > 0f);

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Enabled = false;
            Assert.Equal(before, loop.FlowSegmentsPerSecond);

            loop.RefreshFlow();
            Assert.Equal(0f, loop.FlowSegmentsPerSecond);
        }

        [Fact]

        public void TheWellMixedRingIsOneParcelHoldingEverything()
        {

            HistoricalThermalSettings settings = Isolated();
            settings.WellMixedCoolant = true;

            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            CoolantLoop mixed = simulation.Solver.Loops[0];

            Assert.Equal(16, mixed.PipeCount);
            Assert.Equal(1, mixed.ParcelCount);

            Assert.Equal(mixed.ThermalMass, mixed.SegmentThermalMass, 3);

            for (int i = 0; i < mixed.PipeCount; i++)
            {
                Assert.Equal(0, mixed.ParcelOf(i));
            }
            Assert.Equal(mixed.HottestSegment, mixed.ColdestSegment, 3);
        }

        [Fact]

        public void BothModelsHoldTheSameHeatAtTheSameTemperature()
        {

            HistoricalThermalSettings mixedSettings = Isolated();
            mixedSettings.WellMixedCoolant = true;

            GridBuilder a = GridBuilder.Large();
            PipeFitter.BuildRing(a, PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5));
            CoolantLoop segmented = a.BuildSimulation(Isolated(), 400f).Solver.Loops[0];

            GridBuilder b = GridBuilder.Large();
            PipeFitter.BuildRing(b, PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5));
            CoolantLoop mixed = b.BuildSimulation(mixedSettings, 400f).Solver.Loops[0];

            Assert.Equal(segmented.ThermalMass, mixed.ThermalMass, 2);
            Assert.Equal(segmented.Energy, mixed.Energy, 1);
        }
    

        [Theory]
        [InlineData(2f)]
        [InlineData(4f)]
        [InlineData(8f)]
        [InlineData(64f)]

        public void FasterFlowNeverEvensTheRingOutLessThanSlowFlow(float fastRate)
        {

            float slow = SpreadAcrossAHeatedRing(1f);

            float fast = SpreadAcrossAHeatedRing(fastRate);

            Assert.True(fast <= slow,
                "at " + fastRate + " parcels per second the ring's spread was " + fast
                + " K against " + slow + " K at one; faster flow is transporting less");
        }

        [Fact]

        public void VeryFastFlowConvergesOnAWellMixedRing()
        {

            float spread = SpreadAcrossAHeatedRing(64f);

            Assert.True(spread < 5f,
                "a ring lapping 64 times a second should be near uniform, spread was " + spread + " K");
        }

        [Fact]

        public void MixingReportsWhenFlowOutrunsTheStep()
        {
            CoolantLoop loop;
            Ring(3, 3, 4f, out loop);

            Assert.Equal(0f, loop.MixingFraction(0.25f));

            Assert.Equal(0.5f, loop.MixingFraction(0.5f), 3);

            Assert.True(loop.MixingFraction(100f) < 1f);
            Assert.True(loop.MixingFraction(100f) > 0.99f);
        }

        [Fact]

        public void MixingConservesHeat()
        {
            CoolantLoop loop;
            Ring(5, 5, 1000f, out loop);

            for (int i = 0; i < loop.PipeCount; i++)
            {
                loop.SetSegmentTemperature(i, 200f + (i * 60f));
            }

            float before = loop.Energy;
            for (int i = 0; i < 200; i++) loop.Advect(1f / 4f);

            Assert.Equal(1f, loop.Energy / before, 5);
        }

        [Theory]
        [InlineData(400f, 5f)]
        [InlineData(1600f, 100f)]

        public void TheLoopPathSurvivesAVeryConductivePipe(float conductivity, float tolerance)
        {
            try
            {
                Catalog.MaterialOverride = properties =>
                {
                    if (Math.Abs(properties.SpecificHeat - 385f) < 0.5f) properties.Conductivity = conductivity;
                    return properties;
                };


                float spread = SpreadAcrossAHeatedRing(64f);

                Assert.True(spread < tolerance,
                    "a ring lapping 64 times a second should stay bounded whatever the pipe is made "
                    + "of; at " + conductivity + " W/(m K) it spread " + spread + " K");
            }
            finally
            {
                Catalog.MaterialOverride = null;
            }
        }

        [Fact]

        public void RefusingTheRingsDemandApproximatesRatherThanDiverging()
        {

            float bare = DemandOfTheHullWithoutItsRing();

            float withRing = DemandOfTheHeatedRing(4096);

            Assert.Equal(1f, bare, 0);
            Assert.True(withRing >= 8f,
                "the ring demanded only " + withRing + " substeps, so it is not what sets the"
                + " demand on this grid and the rest of this measures nothing");


            int halfWay = Ceiling(withRing, 4.5f);

            int hard = Ceiling(withRing, 9f);


            float granted = SpreadAcrossAHeatedRing(1f, 4096);

            float halved = SpreadAcrossAHeatedRing(1f, halfWay);

            float refused = SpreadAcrossAHeatedRing(1f, hard);

            Assert.True(halved < granted * 2f,
                "granting " + halfWay + " substeps of " + withRing + " left the ring at "
                + halved + " K against "
                + granted + " K granted in full, which is not the approximation this pins");

            Assert.True(refused < granted * 4f,
                "granting " + hard + " substeps of " + withRing + " left the ring at "
                + refused + " K against "
                + granted + " K granted in full; the coolant path has lost its bound and A10 is"
                + " open again");

            Assert.True(refused >= halved * 0.99f,
                hard + " substeps left the ring at " + refused + " K and " + halfWay + " left it at "
                + halved + " K, so the error is not monotone in what was refused");
        }

        [Fact]

        public void TheClampIsInertWhileTheDemandIsGranted()
        {

            float clamped = SpreadAcrossAHeatedRing(1f, 4096);


            ThermalSimulation unclamped = HeatedRing(1f, 4096);
            unclamped.Settings.ClampConductionOvershoot = false;
            unclamped.StepExact(300, Worlds.Shadow());

            CoolantLoop loop = unclamped.Solver.Loops[0];
            float bare = loop.HottestSegment - loop.ColdestSegment;

            Assert.Equal(bare, clamped, 5);
        }

        [Fact]

        public void NoNodeIsDrivenPastTheHottestThingPullingOnIt()
        {

            ThermalSimulation simulation = HeatedRing(64f, 1);

            float hottest = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            for (int step = 0; step < 600; step++)
            {
                simulation.StepExact(1, Worlds.Shadow());

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Temperature > hottest) hottest = nodes[i].Temperature;
                }
            }

            float reactor = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Block.PowerProducedWatts <= 0f) continue;
                if (nodes[i].Temperature > reactor) reactor = nodes[i].Temperature;
            }

            Assert.True(hottest <= reactor * 1.05f,
                "the hottest node reached " + hottest + " K against the reactor's " + reactor
                + " K, so something was driven past the only thing making heat");
        }


        private static int Ceiling(float demand, float oversubscription)
        {
            int ceiling = (int)Math.Round(demand / oversubscription);
            return ceiling < 1 ? 1 : ceiling;
        }


        private static float DemandOfTheHeatedRing(int maxSubsteps)
        {

            ThermalSimulation simulation = HeatedRing(1f, maxSubsteps);
            simulation.StepExact(300, Worlds.Shadow());
            return simulation.Solver.LastSubsteps;
        }


        private static float DemandOfTheHullWithoutItsRing()
        {

            HistoricalThermalSettings settings = HeatedRingSettings(4096);

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            foreach (Vector3I cell in cells) builder.Place(Catalog.LightArmor(), cell);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(75000f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            simulation.StepExact(300, Worlds.Shadow());
            return simulation.Solver.LastSubsteps;
        }


        private static HistoricalThermalSettings HeatedRingSettings(int maxSubsteps)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableDamage = false;
            settings.MaxSubsteps = maxSubsteps;
            settings.MaxSubstepsPerBlock = 0;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation HeatedRing(float segmentsPerSecond, int maxSubsteps)
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(75000f);

            ThermalSimulation simulation = builder.BuildSimulation(
                HeatedRingSettings(maxSubsteps), 300f);

            CoolantLoop loop = simulation.Solver.Loops[0];
            loop.Properties.LargeGridFlowRate = segmentsPerSecond * loop.ParcelLengthMetres;
            loop.Properties.SmallGridFlowRate = loop.Properties.LargeGridFlowRate;
            loop.RefreshFlow();
            return simulation;
        }


        private static float SpreadAcrossAHeatedRing(float segmentsPerSecond, int maxSubsteps)
        {

            ThermalSimulation simulation = HeatedRing(segmentsPerSecond, maxSubsteps);
            simulation.StepExact(300, Worlds.Shadow());

            CoolantLoop loop = simulation.Solver.Loops[0];
            return loop.HottestSegment - loop.ColdestSegment;
        }


        private static float SpreadAcrossAHeatedRing(float segmentsPerSecond)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.Frequency = 1;
            settings.EnableEnvironment = false;
            settings.EnableDamage = false;
            settings.MaxSubsteps = 1;
            settings.MaxSubstepsPerBlock = 0;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            PipeFitter.BuildRing(builder, cells, -1, sinks);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(75000f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            CoolantLoop loop = simulation.Solver.Loops[0];
            loop.Properties.LargeGridFlowRate = segmentsPerSecond * loop.ParcelLengthMetres;
            loop.Properties.SmallGridFlowRate = loop.Properties.LargeGridFlowRate;
            loop.RefreshFlow();

            simulation.StepExact(300, Worlds.Shadow());
            return loop.HottestSegment - loop.ColdestSegment;
        }
    

        [Fact]

        public void PumpPowerIsLinearInSpeed()
        {

            CoolantPump pump = new CoolantPump();
            pump.MaxPowerWatts = 20000f;

            pump.Speed = 1f;
            Assert.Equal(20000f, pump.DemandWatts, 2);

            pump.Speed = 0.5f;
            Assert.Equal(10000f, pump.DemandWatts, 2);

            pump.Speed = 0.25f;
            Assert.Equal(5000f, pump.DemandWatts, 2);

            pump.Enabled = false;
            Assert.Equal(0f, pump.DemandWatts);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(9)]

        public void TheBillForAGivenFlowDoesNotDependOnHowManyPumpsDeliverIt(int pumpCount)
        {
            CoolantLoop loop;
            Ring(9, 9, 4f, out loop);

            loop.Pumps.Clear();

            const float totalDemand = 1f;
            for (int i = 0; i < pumpCount; i++)
            {

                CoolantPump pump = new CoolantPump();
                pump.MaxPowerWatts = 20000f;
                pump.Speed = totalDemand / pumpCount;
                loop.Pumps.Add(pump);
            }

            loop.RefreshFlow();

            float bill = 0f;
            for (int i = 0; i < loop.Pumps.Count; i++) bill += loop.Pumps[i].DemandWatts;

            Assert.Equal(loop.Properties.FlowRateFor(loop.ParcelLengthMetres) / loop.ParcelLengthMetres,
                loop.FlowSegmentsPerSecond, 3);

            Assert.Equal(20000f, bill, 1);
        }

        [Fact]

        public void FlowRisesWithTheSquareRootOfCombinedPumping()
        {
            CoolantLoop loop;
            Ring(9, 9, 10f, out loop);

            loop.Pumps.Clear();
            for (int i = 0; i < 4; i++)
            {

                CoolantPump pump = new CoolantPump();
                pump.MaxPowerWatts = 20000f;
                loop.Pumps.Add(pump);

                loop.RefreshFlow();
                Assert.Equal(10f * (float)Math.Sqrt(i + 1), loop.FlowSegmentsPerSecond, 3);
            }
        }
    

        [Fact]

        public void APumpFittedBackwardsDrivesTheRingInReverse()
        {

            CoolantLoop forward = RingWithPump(false);

            CoolantLoop reverse = RingWithPump(true);

            Assert.Single(forward.Pumps);
            Assert.Single(reverse.Pumps);

            Assert.Equal(-forward.Pumps[0].Direction, reverse.Pumps[0].Direction);
            Assert.Equal(forward.FlowSegmentsPerSecond, -reverse.FlowSegmentsPerSecond, 3);
            Assert.NotEqual(0f, reverse.FlowSegmentsPerSecond);
        }

        [Fact]

        public void AReversedRingCarriesHeatJustAsWell()
        {

            float forward = SpreadAfterHeatingOneSink(false);

            float reverse = SpreadAfterHeatingOneSink(true);

            Assert.True(forward > 0f && reverse > 0f, "both directions should be transporting");
            Assert.Equal(1f, reverse / forward, 1);
        }

        [Fact]

        public void OpposedPumpsCancelAndTheRingStops()
        {
            CoolantLoop loop;
            Ring(5, 5, 4f, out loop);

            loop.Pumps.Clear();


            CoolantPump forward = new CoolantPump();
            forward.MaxPowerWatts = 20000f;
            forward.Direction = 1;
            loop.Pumps.Add(forward);

            loop.RefreshFlow();
            Assert.True(loop.FlowSegmentsPerSecond > 0f);


            CoolantPump against = new CoolantPump();
            against.MaxPowerWatts = 20000f;
            against.Direction = -1;
            loop.Pumps.Add(against);

            loop.RefreshFlow();
            Assert.Equal(0f, loop.FlowSegmentsPerSecond, 4);
            Assert.Equal(0f, loop.PumpDemand, 4);

            Assert.Equal(20000f, forward.DemandWatts, 2);
            Assert.Equal(20000f, against.DemandWatts, 2);
        }

        [Fact]

        public void OpposedPumpsSubtractBeforeTheSquareRoot()
        {
            CoolantLoop loop;
            Ring(5, 5, 10f, out loop);

            loop.Pumps.Clear();
            for (int i = 0; i < 4; i++)
            {

                CoolantPump pump = new CoolantPump();
                pump.MaxPowerWatts = 20000f;
                pump.Direction = i < 3 ? 1 : -1;
                loop.Pumps.Add(pump);
            }

            loop.RefreshFlow();

            Assert.Equal(2f, loop.PumpDemand, 3);
            Assert.Equal(10f * (float)Math.Sqrt(2f), loop.FlowSegmentsPerSecond, 3);
        }

        [Fact]

        public void ReverseRotationIsExactToo()
        {
            CoolantLoop loop;
            Ring(5, 5, 4f, out loop);

            loop.Pumps.Clear();

            CoolantPump against = new CoolantPump();
            against.MaxPowerWatts = 20000f;
            against.Direction = -1;
            loop.Pumps.Add(against);
            loop.RefreshFlow();

            for (int i = 0; i < loop.PipeCount; i++)
            {
                loop.SetSegmentTemperature(i, 250f + (i * 40f));
            }

            float before = loop.Energy;
            float hottest = loop.HottestSegment;

            for (int i = 0; i < 4 * loop.PipeCount; i++) loop.Advect(1f / 4f);

            Assert.Equal(1f, loop.Energy / before, 5);
            Assert.Equal(hottest, loop.HottestSegment, 2);
        }


        private static CoolantLoop RingWithPump(bool reversed)
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5), -1, null, reversed);

            return builder.BuildSimulation(Isolated(), 300f).Solver.Loops[0];
        }


        private static float SpreadAfterHeatingOneSink(bool reversed)
        {
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5);
            PipeFitter.BuildRing(builder, cells, -1, sinks, reversed);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(75000f);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            simulation.StepExact(4000, Worlds.Shadow());

            CoolantLoop loop = simulation.Solver.Loops[0];
            return loop.HottestSegment - loop.ColdestSegment;
        }
    
    
        [Fact]

        public void FlowIsReportedInMetresPerSecond()
        {
            CoolantLoop loop;
            Ring(5, 5, 4f, out loop);

            Assert.Equal(2.5f, loop.ParcelLengthMetres, 3);
            Assert.Equal(loop.FlowSegmentsPerSecond * 2.5f, loop.FlowMetresPerSecond, 3);
            Assert.Equal(10f, Math.Abs(loop.FlowMetresPerSecond), 2);

            float before = loop.FlowMetresPerSecond;
            loop.Pumps[0].Direction = -loop.Pumps[0].Direction;
            loop.RefreshFlow();
            Assert.Equal(-before, loop.FlowMetresPerSecond, 2);

            loop.Pumps[0].Enabled = false;
            loop.RefreshFlow();
            Assert.Equal(0f, loop.FlowMetresPerSecond, 4);
        }

        [Fact]

        public void TheFlowRateDialIsMetresPerSecondWhicheverGridItIsOn()
        {
            CoolantLoop loop;
            Ring(6, 5, 4f, out loop);

            float baseline = Math.Abs(loop.FlowSegmentsPerSecond);
            Assert.True(baseline > 0f);

            loop.Properties.LargeGridFlowRate *= 2f;
            loop.Properties.SmallGridFlowRate *= 2f;
            loop.RefreshFlow();

            Assert.Equal(baseline * 2f, Math.Abs(loop.FlowSegmentsPerSecond), 3);

            loop.Properties.LargeGridFlowRate = 0f;
            loop.Properties.SmallGridFlowRate = 0f;
            loop.RefreshFlow();

            Assert.Equal(0f, loop.FlowSegmentsPerSecond);
        }
    }
}
