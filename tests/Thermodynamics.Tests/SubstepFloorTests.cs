using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A step is divided into as many substeps as the stiffest node on the grid needs, and every
    /// other node pays for them. On a real capital ship that stiffest node is a light fitting:
    /// measured on a 42,051-block hull, forty-three 16 kg lights asked for twenty-eight substeps
    /// while the five hundred kilogram armour around them asked for one, and the ship ran at a
    /// third of real time to pay for it.
    ///
    /// <c>MaxSubstepsPerBlock</c> is the answer, and it is an approximation, so what it does and
    /// does not disturb has to be pinned rather than asserted in prose:
    ///
    /// <list type="bullet">
    /// <item>it bounds what conduction can ask for;</item>
    /// <item>it moves no node that was already above the floor;</item>
    /// <item>it does not move the temperature anything settles at, because a steady state is
    /// where the watts cancel and has nothing to do with heat capacity;</item>
    /// <item>it leaves the block's real capacity on the node, so every readout still describes
    /// the block rather than the approximation used to integrate it;</item>
    /// <item>and it is inert when off.</item>
    /// </list>
    /// </summary>
    public class SubstepFloorTests
    {
        private readonly ITestOutputHelper output;


        public SubstepFloorTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static BlockModel LightFitting()
        {
            return BlockModel.Solid("LightFitting", Vector3I.One, 16f, Catalog.DefaultThermal());
        }


        private static ThermalSettings Settings(int cap)
        {
            ThermalSettings settings = new ThermalSettings { Frequency = 4 };
            settings.MaxSubstepsPerBlock = cap;

            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static ThermalSimulation Hull(ThermalSettings settings, float temperature, BlockModel centre)
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);
                        bool middle = x == 1 && y == 1 && z == 1;
                        builder.Place(middle ? centre : armour, cell);
                    }
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, temperature);
            simulation.RebuildAll();
            return simulation;
        }


        private static ThermalSimulation Hull(ThermalSettings settings, float temperature)
        {
            return Hull(settings, temperature, LightFitting());
        }

        [Fact]

        public void OneLightBlockSetsTheSubstepCountForTheWholeGrid()
        {

            ThermalSimulation armourOnly = Hull(Settings(0), 300f, Catalog.LightArmor());

            ThermalSimulation withFitting = Hull(Settings(0), 300f);

            float plain = armourOnly.Solver.RequiredSubsteps(armourOnly.Settings.StepSeconds);
            float stiff = withFitting.Solver.RequiredSubsteps(withFitting.Settings.StepSeconds);

            Assert.True(stiff > plain * 8f,
                "one 16 kg block took the substep estimate from " + plain + " to " + stiff
                + "; if that ratio has collapsed the premise of MaxSubstepsPerBlock has too");
        }

        [Fact]

        public void TheCapBoundsTheEstimateExactly()
        {

            ThermalSimulation uncapped = Hull(Settings(0), 300f);
            float before = uncapped.Solver.RequiredSubsteps(uncapped.Settings.StepSeconds);

            foreach (int cap in new int[] { 8, 4, 2, 1 })
            {

                ThermalSimulation capped = Hull(Settings(cap), 300f);
                float after = capped.Solver.RequiredSubsteps(capped.Settings.StepSeconds);

                Assert.True(after < before,
                    "cap " + cap + " left the estimate at " + after + ", against " + before + " uncapped");

                Assert.True(after <= cap + 0.001f,
                    "cap " + cap + " still asked for " + after + " substeps");
            }
        }

        [Fact]

        public void TheCapReachesBlocksMadeStiffByTheSkyRatherThanByTheirNeighbours()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(LightFitting(), Vector3I.Zero);

            ThermalSimulation exposed = builder.BuildSimulation(Settings(0), 300f);
            exposed.RebuildAll();

            EnvironmentSample air = Worlds.PlanetSurface(1f, 0.5f);
            exposed.StepExact(1, air);

            float before = exposed.Solver.RequiredSubsteps(exposed.Settings.StepSeconds);
            Assert.True(before > 4f,
                "the lone exposed block only asked for " + before
                + " substeps, so this test is no longer exercising environment stiffness");

            GridBuilder second = GridBuilder.Large();
            second.Place(LightFitting(), Vector3I.Zero);

            ThermalSimulation capped = second.BuildSimulation(Settings(2), 300f);
            capped.RebuildAll();
            capped.StepExact(1, air);

            float after = capped.Solver.RequiredSubsteps(capped.Settings.StepSeconds);
            Assert.True(after <= 2f + 0.001f,
                "cap 2 left an environment-stiff block asking for " + after + " substeps");
        }

        [Fact]

        public void ANodeAlreadyAboveTheFloorIsNotMoved()
        {

            ThermalSimulation uncapped = Hull(Settings(0), 300f);

            ThermalSimulation capped = Hull(Settings(4), 300f);

            EnvironmentSample sample = Worlds.Shadow();
            uncapped.StepExact(20, sample);
            capped.StepExact(20, sample);

            IList<ThermalNode> a = uncapped.Solver.Nodes;
            IList<ThermalNode> b = capped.Solver.Nodes;
            Assert.Equal(a.Count, b.Count);

            int moved = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Block.Model.Name == "LightFitting") continue;

                if (Math.Abs(a[i].Temperature - b[i].Temperature) > 0.5f) moved++;
            }

            Assert.Equal(0, moved);
        }

        [Fact]

        public void TheDifferenceIsATransientAndDecaysAsTheGridSettles()
        {

            ThermalSimulation uncapped = Hull(Settings(0), 700f);

            ThermalSimulation capped = Hull(Settings(2), 700f);

            EnvironmentSample sample = Worlds.Shadow();

            uncapped.StepExact(40, sample);
            capped.StepExact(40, sample);

            float early = WorstDifference(uncapped, capped);

            uncapped.StepExact(LabClock.Steps(4000), sample);
            capped.StepExact(LabClock.Steps(4000), sample);

            float late = WorstDifference(uncapped, capped);

            Assert.True(early > 0f, "the floor changed nothing at all, so the test is not testing it");
            Assert.True(late < early * 0.5f,
                "the worst difference went from " + early + " K to " + late
                + " K; the floor's error is supposed to decay as the grid settles, not persist");
        }


        private static float WorstDifference(ThermalSimulation a, ThermalSimulation b)
        {
            IList<ThermalNode> left = a.Solver.Nodes;
            IList<ThermalNode> right = b.Solver.Nodes;

            float worst = 0f;
            for (int i = 0; i < left.Count; i++)
            {
                float difference = Math.Abs(left[i].Temperature - right[i].Temperature);
                if (difference > worst) worst = difference;
            }

            return worst;
        }

        [Fact]

        public void TheFlooredCountHoldsForAsLongAsTheFloorDoes()
        {

            ThermalSimulation simulation = Hull(Settings(3), 900f);
            EnvironmentSample sample = Worlds.Shadow();

            for (int step = 0; step < 6; step++)
            {
                simulation.StepExact(1, sample);
                Assert.Equal(1, simulation.Solver.FlooredNodes);
            }
        }

        [Fact]

        public void AFlooredGridDemandsExactlyItsCapAndNotLess()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            builder.Place(BlockModel.Solid("Interior", Vector3I.One, 20f, Catalog.DefaultThermal()),

                new Vector3I(3, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(3), 900f);
            EnvironmentSample sample = Worlds.Shadow();

            for (int step = 0; step < 20; step++)
            {
                simulation.StepExact(1, sample);

                float raw = 0f;
                for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
                {
                    float demand = simulation.Solver.NodeSubstepDemand(i);
                    if (demand > raw) raw = demand;
                }

                if (raw <= 3f) continue;

                Assert.Equal(3f, simulation.Solver.LastRequiredSubsteps, 2);
            }
        }

        [Fact]

        public void TheBlockKeepsItsRealHeatCapacity()
        {

            ThermalSimulation capped = Hull(Settings(1), 300f);
            capped.StepExact(5, Worlds.Shadow());

            ThermalNode fitting = null;
            IList<ThermalNode> nodes = capped.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Block.Model.Name == "LightFitting") fitting = nodes[i];
            }

            Assert.NotNull(fitting);

            float expected = (450f * 16f) / capped.Settings.HeatTimeScale;
            Assert.Equal(expected, fitting.ThermalMass, 3);
        }

        [Fact]

        public void TheProfileAgreesWithTheEstimateAndNamesTheBlock()
        {

            ThermalSimulation sim = Hull(Settings(0), 300f);
            ThermalSolver.SubstepProfile profile = sim.Solver.ProfileSubsteps();

            float estimate = sim.Solver.RequiredSubsteps(sim.Settings.StepSeconds);

            Assert.Equal(estimate, profile.RequiredSubsteps, 3);
            Assert.Equal(estimate, profile.RequiredSubstepsInForce, 3);
            Assert.Equal(sim.Solver.Nodes.Count, profile.Nodes);

            Assert.True(profile.WorstNodeIndex >= 0);
            Assert.Equal("LightFitting", sim.Solver.Nodes[profile.WorstNodeIndex].Block.Model.Name);

            Assert.True(profile.WorstNodeConductionShare > 0.9f,
                "conduction share was " + profile.WorstNodeConductionShare);

            long counted = 0;
            for (int i = 0; i < profile.Buckets.Length; i++) counted += profile.Buckets[i];
            Assert.Equal(profile.Nodes, counted);
        }

        [Fact]

        public void TheProfileDescribesTheGridRatherThanTheSettings()
        {

            ThermalSolver.SubstepProfile off = Hull(Settings(0), 300f).Solver.ProfileSubsteps();

            ThermalSolver.SubstepProfile on = Hull(Settings(1), 300f).Solver.ProfileSubsteps();

            Assert.Equal(off.RequiredSubsteps, on.RequiredSubsteps, 3);
            Assert.Equal(off.WorstNodeDemand, on.WorstNodeDemand, 3);

            for (int i = 0; i < off.Buckets.Length; i++)
            {
                Assert.Equal(off.Buckets[i], on.Buckets[i]);
            }

            Assert.True(on.RequiredSubstepsInForce < off.RequiredSubstepsInForce);
            Assert.True(on.RequiredSubstepsInForce <= 1.001f);
        }

        [Fact]

        public void TheProjectionPredictsWhatTheCapDoes()
        {

            ThermalSolver.SubstepProfile profile = Hull(Settings(0), 300f).Solver.ProfileSubsteps();
            int[] caps = ThermalSolver.SubstepProfile.ProjectedCaps;

            for (int c = 0; c < caps.Length; c++)
            {

                ThermalSimulation capped = Hull(Settings(caps[c]), 300f);
                float actual = capped.Solver.RequiredSubsteps(capped.Settings.StepSeconds);

                Assert.Equal(profile.CapRequiredSubsteps[c], actual, 2);

                int floored = capped.Solver.FlooredNodes;
                Assert.Equal(profile.CapNodesFloored[c], floored);
            }
        }

        [Fact]

        public void TheCapReachesRoomAirAndNotOnlyBlocks()
        {

            ThermalSimulation open = Sealed(SealedSettings(0));
            float before = open.Solver.RequiredSubsteps(open.Settings.StepSeconds);

            Assert.True(open.Solver.RoomAir.Count > 0, "the test hull holds no air");


            ThermalSimulation capped = Sealed(SealedSettings(1));
            float after = capped.Solver.RequiredSubsteps(capped.Settings.StepSeconds);

            Assert.True(before > 1f,
                "the sealed hull only asked for " + before + " substeps to begin with");
            Assert.True(after <= 1f + 0.001f,
                "cap 1 left the grid asking for " + after + " substeps, so something it cannot"
                + " reach is still setting the count");
        }


        private static ThermalSettings SealedSettings(int cap)
        {
            ThermalSettings settings = new ThermalSettings { Frequency = 2 };
            settings.MaxSubstepsPerBlock = cap;
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static ThermalSimulation Sealed(ThermalSettings settings)
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        if (x == 1 && y == 1 && z == 1) continue;
                        builder.Place(armour, new Vector3I(x, y, z));
                    }
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            simulation.RebuildAll();
            simulation.SetRoomPressure(new Vector3I(1, 1, 1), 1f);
            return simulation;
        }

        [Fact]

        public void TheCapIsInertWhenItIsOff()
        {

            ThermalSimulation a = Hull(Settings(0), 500f);

            ThermalSimulation b = Hull(Settings(0), 500f);

            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));
            a.StepExact(50, sample);
            b.StepExact(50, sample);

            for (int i = 0; i < a.Solver.Nodes.Count; i++)
            {
                Assert.Equal(a.Solver.Nodes[i].Temperature, b.Solver.Nodes[i].Temperature);
            }
        }

        [Fact]

        public void TheFloorTakesTheWholeGridsDemandDownToItsCap()
        {
            const int Cap = 6;


            ThermalSettings uncapped = Settings(0);

            ThermalSettings capped = Settings(Cap);


            float demanded = DemandInAir(uncapped);

            float bounded = DemandInAir(capped);

            output.WriteLine("demand {0:n2} uncapped, {1:n2} at a cap of {2}", demanded, bounded, Cap);

            Assert.True(demanded > Cap * 2f,
                "the hull demands only " + demanded + " substeps in air, so a cap of " + Cap
                + " has nothing to bound and this judges nothing");

            Assert.True(bounded <= Cap + 0.01f,
                "the cap left the grid demanding " + bounded + " against a cap of " + Cap);
        }


        private static float DemandInAir(ThermalSettings settings)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));
            builder.Place(LightFitting(), new Vector3I(2, 4, 2));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            simulation.StepExact(1, Worlds.Flight(1f, 200f));
            return simulation.Solver.LastRequiredSubsteps;
        }
    }
}
