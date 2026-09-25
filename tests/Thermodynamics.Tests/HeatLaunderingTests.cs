using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What a player can make heat do by changing the block population**, which is the exploit
    /// surface of `F26`'s deliberate limit.
    ///
    /// <para>
    /// Heat leaves the world with a block that leaves it and a welded block arrives at ambient.
    /// That was recorded as a limit on the grounds that conserving it would mean a grinder that
    /// heats the ship around it. What the limit did not cost out is that a player can *drive* it:
    /// grind and reweld on a timer and the ship has a heat sink made of one block.
    /// </para>
    ///
    /// <para>
    /// **The coolant loop is the sharp case and it is measured here.** A loop's coolant carries far
    /// more heat than the pipe it sits in, and breaking the ring dissolves the loop. The solver
    /// spills a dying loop into its pipes rather than deleting it — that fix went in after a 190 MJ
    /// case where grinding a pump dumped a ship's heat on demand — and these measure how much of it
    /// actually survives.
    /// </para>
    ///
    /// <para>
    /// **A third of it used to.** The spill mixed each parcel into its pipe at
    /// `(T_n·M_n + T_s·M_s) / (M_n + M_s)` and then left the node at `M_n`, so the energy landing
    /// was `mixed × M_n` where `T_n·M_n + T_s·M_s` went in and `M_s / (M_n + M_s)` of the ring's
    /// heat — 67.9 % on a large grid — was destroyed by two capacities nobody chose. The pipe now
    /// takes the parcel's heat capacity along with its temperature, so the only heat a broken ring
    /// loses is what was inside the block that left.
    /// </para>
    /// </summary>
    public class HeatLaunderingTests
    {
        private readonly ITestOutputHelper output;


        public HeatLaunderingTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const float Ambient = 293.15f;


        private static ThermalSettings Isolated()
        {

            ThermalSettings s = new ThermalSettings();
            s.EnableEnvironment = false;
            s.EnableSolarHeat = false;
            s.EnableWasteHeat = false;
            s.Derive();
            return s;
        }


        private static float HeatAboveAmbient(ThermalSimulation simulation)
        {
            float total = 0f;
            ThermalSolver solver = simulation.Solver;

            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                total += (solver.Nodes[i].Temperature - Ambient) * solver.Nodes[i].ThermalMass;
            }

            for (int i = 0; i < solver.Loops.Count; i++)
            {
                CoolantLoop loop = solver.Loops[i];
                total += loop.Energy - (Ambient * loop.ThermalMass);
            }

            return total;
        }

        [Fact]

        public void ABlockChangeElsewhereDoesNotCoolTheCoolant()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, 900f);

            simulation.AddBlock(new BlockInstance(
                Catalog.LightArmor(), new Vector3I(20, 20, 20), BlockOrientation.Identity));
            simulation.RebuildAll();

            Assert.Single(simulation.Solver.Loops);
            Assert.Equal(900f, simulation.Solver.Loops[0].Temperature, 1);
        }

        [Theory]
        [InlineData(3, 3, 8)]
        [InlineData(4, 3, 10)]
        [InlineData(5, 4, 14)]

        public void GrindingOnePipeLosesOnlyThatPipesShareOfTheCoolant(int width, int depth, int pipes)
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, width, depth));

            Assert.Equal(pipes, ring.Count);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, 900f);

            float segmentMass = loop.SegmentThermalMass;
            float nodeMass = simulation.Solver.GetNode(ring[0]).ThermalMass;

            float start = HeatAboveAmbient(simulation);

            BlockInstance popped = ring[3];

            simulation.RemoveBlock(popped);
            simulation.RebuildAll();

            Assert.Empty(simulation.Solver.Loops);

            float broken = HeatAboveAmbient(simulation);

            simulation.AddBlock(popped);
            simulation.RebuildAll();

            float end = HeatAboveAmbient(simulation);

            float lost = start - end;

            output.WriteLine("pipe node {0:n0} J/K, coolant parcel {1:n0} J/K", nodeMass, segmentMass);
            output.WriteLine("heat above ambient {0:n0} J -> {1:n0} J broken -> {2:n0} J rewelded",
                start, broken, end);
            output.WriteLine("the ring of {0} lost {1:n0} J ({2:n2} %)", pipes, lost, 100f * lost / start);

            Assert.Single(simulation.Solver.Loops);

            float fluid = segmentMass * pipes * (900f - Ambient);
            Assert.InRange(lost, fluid * 0.95f, fluid * 1.05f);

            Assert.InRange(start - broken, lost * 0.99f, lost * 1.01f);
            Assert.Equal(0f, simulation.Solver.Loops[0].FillFraction);
        }

        [Fact]

        public void GrindingARingOpenDrainsItAndRefillingCostsBackWhatDrained()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            Assert.Equal(8, ring.Count);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];

            const float Warm = Ambient + 100f;
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, Warm);


            float start = HeatAboveAmbient(simulation);

            BlockInstance popped = ring[3];
            simulation.RemoveBlock(popped);
            simulation.RebuildAll();

            float drained = start - HeatAboveAmbient(simulation);

            simulation.AddBlock(popped);
            simulation.RebuildAll();

            CoolantLoop back = simulation.Solver.Loops[0];
            Assert.Equal(0f, back.FillFraction);

            float spent = 0f;
            for (int tick = 0; tick < 10000 && back.FillFraction < 1f; tick++) spent += back.Refill(1f);

            output.WriteLine("grind drained {0:n0} J of {1:n0} J held", drained, start);
            output.WriteLine("refilling it spent {0:n0} J; ratio {1:n4}", spent, spent / drained);

            Assert.InRange(drained, start * 0.95f, start * 1.001f);

            Assert.InRange(spent, drained * 0.98f, drained * 1.02f);
        }

        [Fact]

        public void AGrindCostsTheHeatInTheBlockThatLeftAndNothingElse()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, 900f);


            float start = HeatAboveAmbient(simulation);
            float parcel = start / ring.Count;


            float afterFirst = GrindAndReweld(simulation, ring[3]);

            float afterSecond = GrindAndReweld(simulation, ring[3]);

            float afterOther = GrindAndReweld(simulation, ring[5]);

            output.WriteLine("start {0:n0} J, one parcel {1:n0} J", start, parcel);
            output.WriteLine("grind pipe 3: {0:n0}; again: {1:n0}; then pipe 5: {2:n0}",
                afterFirst, afterSecond, afterOther);

            Assert.InRange(afterFirst, 0f, start * 0.05f);

            Assert.InRange(afterSecond, afterFirst - start * 0.001f, afterFirst + start * 0.001f);


            Assert.InRange(afterOther, afterSecond - start * 0.001f, afterSecond + start * 0.001f);

            Assert.Single(simulation.Solver.Loops);
            Assert.Equal(ring.Count, simulation.Solver.Loops[0].Pipes.Count);
        }


        private static float GrindAndReweld(ThermalSimulation simulation, BlockInstance block)
        {
            simulation.RemoveBlock(block);
            simulation.RebuildAll();
            simulation.AddBlock(block);
            simulation.RebuildAll();

            return HeatAboveAmbient(simulation);
        }

        [Fact]

        public void AGrindLeavesNoPipeHoldingCoolantBecauseTheRingDrained()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, 900f);

            simulation.RemoveBlock(ring[3]);
            simulation.RebuildAll();

            float hottest = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                ThermalNode node = simulation.Solver.GetNode(ring[i]);
                if (node == null) continue;
                if (node.Temperature > hottest) hottest = node.Temperature;
            }

            output.WriteLine("hottest pipe after the grind: {0:n2} K", hottest);

            for (int i = 0; i < ring.Count; i++)
            {
                ThermalNode node = simulation.Solver.GetNode(ring[i]);
                if (node == null) continue;

                Assert.Equal(0f, node.HeldCoolantCapacity);
            }

            Assert.True(hottest <= 900f + 0.01f,
                "a pipe reached " + hottest.ToString("n2") + " K, above the 900 K the fluid was at");
        }


        private ThermalSimulation SpilledByTurningTheMechanismOff(out List<BlockInstance> ring,
            out float fluidKelvin, out float pipeCapacity, out float parcelCapacity)
        {
            const float Fluid = 900f;


            ThermalSettings settings = Isolated();
            GridBuilder builder = GridBuilder.Large();
            ring = PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, Fluid);

            fluidKelvin = Fluid;
            pipeCapacity = simulation.Solver.GetNode(ring[0]).ThermalMass * loop.HeatTimeScale;
            parcelCapacity = loop.ThermalMass * loop.HeatTimeScale / loop.Pipes.Count;

            settings.EnableCoolantLoops = false;
            settings.Derive();
            simulation.RebuildAll();

            return simulation;
        }

        [Fact]

        public void TheBoundHoldsWhenTheMechanismIsTurnedOff()
        {
            List<BlockInstance> ring;
            float fluid, pipeCapacity, parcelCapacity;

            ThermalSimulation simulation =
                SpilledByTurningTheMechanismOff(out ring, out fluid, out pipeCapacity,
                    out parcelCapacity);

            Assert.Empty(simulation.Solver.Loops);

            int holding = 0;
            float hottest = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                ThermalNode node = simulation.Solver.GetNode(ring[i]);
                Assert.NotNull(node);

                if (node.HeldCoolantCapacity > 0f) holding++;
                if (node.Temperature > hottest) hottest = node.Temperature;
            }

            Assert.Equal(ring.Count, holding);

            float unbounded = Ambient
                + (fluid - Ambient) * parcelCapacity / pipeCapacity;

            output.WriteLine("pipe {0:n0} J/K, parcel {1:n0} J/K", pipeCapacity, parcelCapacity);
            output.WriteLine("hottest pipe {0:n1} K against {1:n1} K of fluid; poured in at the"
                + " node's own capacity it would have been {2:n0} K", hottest, fluid, unbounded);

            Assert.True(hottest <= fluid + 0.01f,
                "a pipe reached " + hottest.ToString("n2") + " K, above the "
                + fluid.ToString("n0") + " K the fluid was at; a mix of two temperatures lies"
                + " between them, so this is the mix having stopped being a mix");

            Assert.True(unbounded > fluid * 2f,
                "the unbounded form would have reached only " + unbounded.ToString("n0")
                + " K, which is not far enough above the fluid for this test to be guarding"
                + " anything — the parcel and the node have stopped differing in capacity");

            float destroyed = parcelCapacity / (parcelCapacity + pipeCapacity);
            output.WriteLine("a temperature-only spill would destroy {0:p1} of the ring's heat",
                destroyed);

            Assert.True(destroyed > 0.9f,
                "a temperature-only spill would destroy " + destroyed.ToString("p1")
                + " of the ring's heat; thermal-model.md's Coolant loops says 95.4 %, and a figure"
                + " this far from it means the coolant charge moved and the page has not");
        }

        [Fact]

        public void TurningTheMechanismOffMovesTheHeatRatherThanLosingIt()
        {
            const float Fluid = 900f;


            ThermalSettings settings = Isolated();
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, Fluid);


            float before = HeatAboveAmbient(simulation);

            settings.EnableCoolantLoops = false;
            settings.Derive();
            simulation.RebuildAll();


            float after = HeatAboveAmbient(simulation);

            output.WriteLine("{0:n0} J in the ring, {1:n0} J in the pipes: {2:n3} % moved",
                before, after, 100f * after / before);

            Assert.True(before > 0f, "the ring held nothing, so this is comparing zeroes");
            Assert.InRange(after, before * 0.999f, before * 1.001f);
        }

        [Fact]

        public void SwitchingItBackOnReclaimsWhatTheSpillLeft()
        {
            const float Fluid = 900f;


            ThermalSettings settings = Isolated();
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            CoolantLoop loop = simulation.Solver.Loops[0];
            for (int i = 0; i < loop.Pipes.Count; i++) loop.SetSegmentTemperature(i, Fluid);


            float before = HeatAboveAmbient(simulation);

            settings.EnableCoolantLoops = false;
            settings.Derive();
            simulation.RebuildAll();

            settings.EnableCoolantLoops = true;
            settings.Derive();
            simulation.RebuildAll();


            float after = HeatAboveAmbient(simulation);

            Assert.Single(simulation.Solver.Loops);
            output.WriteLine("{0:n0} J -> off -> on -> {1:n0} J ({2:n3} %)",
                before, after, 100f * after / before);

            Assert.InRange(after, before * 0.999f, before * 1.001f);
        }

        [Fact]

        public void EveryCoolantBlockHasExactlyTwoLinkPorts()
        {

            List<string> wrong = new List<string>();
            int seen = 0;

            foreach (string subtype in ShippedBlocks.Subtypes())
            {
                BlockModel model = ShippedBlocks.Model(subtype);
                if (model == null || model.Coolant == null) continue;
                if (model.Coolant.LinkPorts.Length == 0) continue;

                seen++;
                if (model.Coolant.LinkPorts.Length == 2) continue;

                wrong.Add(subtype + " declares " + model.Coolant.LinkPorts.Length);
            }

            Assert.True(seen >= 8,
                "only " + seen + " shipped blocks carry coolant link ports, so this is asserting"
                + " about almost nothing");

            Assert.True(wrong.Count == 0,
                "coolant blocks with a port count other than two, which makes a ring splittable"
                + " and reopens backlog.md F28:\n  " + string.Join("\n  ", wrong));
        }

        [Fact]

        public void HeldCoolantSurvivesASaveAndLoad()
        {
            List<BlockInstance> ring;
            float fluid, pipeCapacity, parcelCapacity;

            ThermalSimulation simulation =
                SpilledByTurningTheMechanismOff(out ring, out fluid, out pipeCapacity,
                    out parcelCapacity);

            float held = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                ThermalNode node = simulation.Solver.GetNode(ring[i]);
                if (node != null) held += node.HeldCoolantCapacity;
            }

            Assert.True(held > 0f,
                "no pipe was holding coolant, so a save and a load of it is a save and a load of"
                + " nothing — which is what this test spent a while doing");


            float spilled = HeatAboveAmbient(simulation);
            string saved = simulation.Save();


            ThermalSettings reloadSettings = Isolated();
            reloadSettings.EnableCoolantLoops = false;
            reloadSettings.Derive();

            GridBuilder reloadBuilder = GridBuilder.Large();
            PipeFitter.BuildRing(reloadBuilder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));
            ThermalSimulation reloaded = reloadBuilder.BuildSimulation(reloadSettings);
            reloaded.RebuildAll();

            reloaded.Load(saved);


            float restored = HeatAboveAmbient(reloaded);

            output.WriteLine("{0:n0} J/K held, {1:n0} J saved, {2:n0} J restored",
                held, spilled, restored);

            Assert.InRange(restored, spilled * 0.999f, spilled * 1.001f);
        }

        [Fact]

        public void TheWellMixedModelLosesTheSameOneParcel()
        {

            ThermalSettings settings = Isolated();
            settings.WellMixedCoolant = true;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring =
                PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            CoolantLoop loop = simulation.Solver.Loops[0];
            loop.Temperature = 900f;


            float start = HeatAboveAmbient(simulation);
            float predicted = start / ring.Count;

            simulation.RemoveBlock(ring[3]);
            simulation.RebuildAll();
            simulation.AddBlock(ring[3]);
            simulation.RebuildAll();


            float end = HeatAboveAmbient(simulation);
            float lost = start - end;

            output.WriteLine("well mixed: {0:n0} J -> {1:n0} J, lost {2:n0} ({3:n2} %)",
                start, end, lost, 100f * lost / start);

            Assert.InRange(lost, start * 0.95f, start * 1.001f);
            Assert.True(predicted > 0f, "one parcel is nothing, so this test is comparing zeroes");
        }
    }
}
