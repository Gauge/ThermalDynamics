using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The scan that turns a grid's temperatures into cues, and the two things it must not do:
    /// cost anything on a cold hull, and repeat itself on a hot one.
    /// </summary>
    public class HeatCueScanTests
    {
        [Fact]

        public void AColdHullProducesNoCuesAndRemembersNothing()
        {
            Rig rig = Rig.Build(300f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Empty(cues);
            Assert.Equal(0, rig.State.Count);
        }

        [Fact]

        public void TheFloorIsTheLowerOfTheGlowStartAndTheWatchPoint()
        {
            Rig shipped = Rig.Build(300f, 900f);
            Assert.Equal(900f * HeatCueState.WatchFraction,
                shipped.Simulation.Solver.CueFloorTemperature(), 1);
            Assert.True(900f * HeatCueState.WatchFraction < Incandescence.GlowStartKelvin(900f));

            Rig fragile = Rig.Build(200f, 300f);
            Assert.Equal(Incandescence.GlowStartKelvin(300f),
                fragile.Simulation.Solver.CueFloorTemperature(), 1);

            Assert.True(fragile.Simulation.Solver.CueFloorTemperature()
                < shipped.Simulation.Solver.CueFloorTemperature(),
                "a grid of low-rated blocks should start looking sooner");
        }

        [Fact]

        public void TheLowestCriticalTemperatureIsTheCoolestBlocks()
        {
            Rig rig = Rig.Build(300f, 1400f, 700f, 1);
            Assert.Equal(700f, rig.Simulation.Solver.LowestCriticalTemperature, 1);
        }

        [Fact]

        public void AGlowingBlockIsReportedWithItsColourTemperature()
        {
            Rig rig = Rig.Build(1000f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Single(cues);
            Assert.Equal(1000f, cues[0].Kelvin, 0);
            Assert.Equal(Incandescence.Glow(1000f, 900f), cues[0].Glow, 4);
            Assert.Equal(HeatCueStage.Critical, cues[0].Stage);
            Assert.True(cues[0].Announce, "the first scan of a block already over should announce");
        }

        [Fact]

        public void AStageAnnouncesOnceRatherThanEveryScan()
        {
            Rig rig = Rig.Build(1000f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);
            Assert.True(cues[0].Announce);

            for (int i = 0; i < 5; i++)
            {
                cues.Clear();
                rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

                Assert.Single(cues);
                Assert.Equal(HeatCueStage.Critical, cues[0].Stage);
                Assert.False(cues[0].Announce, "scan " + i + " announced again");
                Assert.True(cues[0].Glow > 0f, "and it is still glowing");
            }
        }

        [Fact]

        public void FallingBackDoesNotAnnounce()
        {
            Rig rig = Rig.Build(1000f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);
            Assert.Equal(HeatCueStage.Critical, cues[0].Stage);

            rig.Hot.Temperature = 800f;
            rig.Step();
            cues.Clear();
            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Single(cues);
            Assert.NotEqual(HeatCueStage.Critical, cues[0].Stage);
            Assert.False(cues[0].Announce);
        }

        [Fact]

        public void ABlockThatCoolsIsForgotten()
        {
            Rig rig = Rig.Build(1000f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);
            Assert.Equal(1, rig.State.Count);

            rig.Hot.Temperature = 300f;
            rig.Step();
            cues.Clear();
            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Empty(cues);
            Assert.Equal(0, rig.State.Count);
        }

        [Fact]

        public void ABlockRatedBelowTheDraperPointStillGlows()
        {
            Rig rig = Rig.Build(560f, 600f);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Single(cues);
            Assert.True(cues[0].Kelvin < Incandescence.DraperKelvin);
            Assert.Equal(0.6f, cues[0].Glow, 3);
        }

        [Fact]

        public void AHullAtRoomTemperatureGlowsNothing()
        {
            Rig rig = Rig.Build(295f, 900f, 900f, 4);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.Empty(cues);
            Assert.True(rig.Simulation.Solver.HottestNode().Temperature
                < rig.Simulation.Solver.CueFloorTemperature(),
                "the grid-level floor did not cover an ordinary hull");
        }

        [Fact]

        public void OnlyTheHotBlockIsWalkedTwice()
        {
            Rig rig = Rig.Build(1000f, 900f, 900f, 8);

            List<HeatCue> cues = new List<HeatCue>();

            rig.Simulation.Solver.CollectHeatCues(rig.State, 0.25f, cues);

            Assert.True(rig.Simulation.Solver.Nodes.Count > 100,
                "the rig is meant to be a hull rather than a handful of blocks");
            Assert.Single(cues);
            Assert.Equal(1, rig.State.Count);
        }

        private class Rig
        {
            public ThermalSimulation Simulation;
            public ThermalNode Hot;

            public HeatCueState State = new HeatCueState();


            public static Rig Build(float kelvin)
            {

                return Build(kelvin, 900f);
            }


            public static Rig Build(float kelvin, float critical)
            {

                return Build(kelvin, critical, critical, 1);
            }


            public static Rig Build(float kelvin, float critical, float hullCritical, int radius)
            {

                ThermalSettings settings = new ThermalSettings();
                settings.EnableDamage = false;
                settings.EnableConduction = false;
                settings.EnableRadiation = false;
                settings.EnableConvection = false;
                settings.EnableEnvironment = false;
                settings.Derive();

                BlockThermalProperties source = Catalog.DefaultThermal();
                source.CriticalTemperature = critical;

                BlockThermalProperties hull = Catalog.DefaultThermal();
                hull.CriticalTemperature = hullCritical;

                GridBuilder builder = GridBuilder.Large();
                builder.Place(BlockModel.Solid("source", Vector3I.One, 1000f, source), Vector3I.Zero);

                BlockModel armour = BlockModel.Solid("hull", Vector3I.One, 500f, hull);
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            if (x == 0 && y == 0 && z == 0) continue;
                            builder.Place(armour, new Vector3I(x, y, z));
                        }
                    }
                }


                Rig rig = new Rig();
                rig.Simulation = builder.BuildSimulation(settings, 60f);
                rig.Hot = rig.Simulation.Solver.GetNode(builder.Placed[0]);

                for (int i = 0; i < rig.Simulation.Solver.Nodes.Count; i++)
                {
                    rig.Simulation.Solver.Nodes[i].Temperature = 300f;
                }

                rig.Hot.Temperature = kelvin;
                rig.Step();
                return rig;
            }


            public void Step()
            {
                Simulation.Update(Simulation.Settings.StepSeconds, Worlds.Shadow());
            }
        }
    }
}
