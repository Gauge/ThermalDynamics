using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// "I built a ring and nothing happened" is the commonest way a coolant loop fails, and the loop
    /// list cannot answer it: the whole symptom is that the loop is absent. These pin the reason
    /// given for each way a run can fail to close.
    ///
    /// The case that prompted it: a field dump held six copies of one ship, four with a working loop
    /// and two without, and nothing in the report could say what differed between them.
    /// </summary>
    public class CoolantFaultTests
    {

        private static ThermalSettings Isolated()
        {
            return Isolation.DeadWorld();
        }

        [Fact]

        public void AWorkingRingReportsNoFaultAtAll()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            CoolantLoopDiagnostics diagnostics = builder.BuildSimulation(Isolated()).DiagnoseLoops();

            Assert.Equal(1, diagnostics.Loops);
            Assert.Equal(8, diagnostics.PipesInLoops);
            Assert.Equal(0, diagnostics.PipesAdrift);
            Assert.False(diagnostics.HasFaults);
        }

        [Fact]

        public void AClosedRingWithNoPumpIsALoopThatCirculatesNothing()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildPumplessRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoopDiagnostics diagnostics = simulation.DiagnoseLoops();

            Assert.Equal(1, diagnostics.Loops);
            Assert.Equal(8, diagnostics.PipesInLoops);
            Assert.Equal(0, diagnostics.PipesAdrift);
            Assert.False(diagnostics.HasFaults);

            CoolantLoop loop = simulation.Solver.Loops[0];
            Assert.False(loop.HasPump);
            Assert.Empty(loop.Pumps);
            Assert.Equal(0f, loop.FlowSegmentsPerSecond);
        }

        [Fact]

        public void ARingBrokenOpenReportsAnOpenEnd()
        {
            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3);
            List<BlockInstance> ring = PipeFitter.BuildRing(builder, cells);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            Assert.Single(simulation.Solver.Loops);

            simulation.RemoveBlock(ring[3]);

            CoolantLoopDiagnostics diagnostics = simulation.DiagnoseLoops();

            Assert.Equal(0, diagnostics.Loops);
            Assert.Equal(7, diagnostics.PipesAdrift);
            Assert.True(diagnostics.CountOf(CoolantFault.OpenEnd) > 0,

                "a broken ring should report an open end, not " + Reasons(diagnostics));
        }

        [Fact]

        public void PipesTouchingAtTheWrongRotationSayTheirPortsDoNotMeet()
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Place(Catalog.CoolantPump(), Vector3I.Zero,

                new BlockOrientation(Base6Directions.Direction.Backward, Base6Directions.Direction.Left));
            builder.Place(Catalog.CoolantPipeStraight(), new Vector3I(0, 0, 1),

                new BlockOrientation(Base6Directions.Direction.Left, Base6Directions.Direction.Up));

            CoolantLoopDiagnostics diagnostics = builder.BuildSimulation(Isolated()).DiagnoseLoops();

            Assert.Equal(0, diagnostics.Loops);
            Assert.Equal(1, diagnostics.CountOf(CoolantFault.PortsDoNotMeet));
        }

        [Fact]

        public void APipeRunningIntoOrdinaryArmourNamesTheBlockingBlock()
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Place(Catalog.CoolantPump(), Vector3I.Zero,

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(0, 0, 1));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(0, 0, -1));

            CoolantLoopDiagnostics diagnostics = builder.BuildSimulation(Isolated()).DiagnoseLoops();

            Assert.Equal(0, diagnostics.Loops);
            Assert.Equal(1, diagnostics.PipesAdrift);
            Assert.Equal(1, diagnostics.CountOf(CoolantFault.BlockedByNonCoolant));
        }

        [Fact]

        public void AnExampleNamesACellThePlayerCanWalkTo()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.CoolantPump(), new Vector3I(4, 5, 6),

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));

            CoolantLoopDiagnostics diagnostics = builder.BuildSimulation(Isolated()).DiagnoseLoops();

            Assert.Single(diagnostics.Examples);
            Assert.Equal(new Vector3I(4, 5, 6), diagnostics.Examples[0].Cell);
            Assert.Equal(CoolantFault.OpenEnd, diagnostics.Examples[0].Fault);
        }

        [Fact]

        public void ExamplesAreCappedButCountsAreNot()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int i = 0; i < 30; i++)
            {
                builder.Place(Catalog.CoolantPump(), new Vector3I(i * 2, 0, 0),

                    new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
            }

            CoolantLoopDiagnostics diagnostics = builder.BuildSimulation(Isolated()).DiagnoseLoops(3);

            Assert.Equal(30, diagnostics.PipesAdrift);
            Assert.Equal(30, diagnostics.CountOf(CoolantFault.OpenEnd));
            Assert.Equal(3, diagnostics.Examples.Count);
        }

        [Fact]

        public void TheOrdinarySearchDoesNotBuildDiagnostics()
        {
            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));
            GridModel grid = builder.Grid;


            SimulationWork work = new SimulationWork();
            List<CoolantLoop> loops = CoolantLoopBuilder.FindLoops(
                grid, LoopThermalProperties.Default(), 293.15f, work);

            Assert.Single(loops);
            Assert.Equal(1, work.LoopSearches);
        }

        [Fact]

        public void EveryCoolantBlockIsAccountedForExactlyOnce()
        {
            GridBuilder builder = GridBuilder.Large();

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            List<Vector3I> pumpless = PipeFitter.RectangleXZ(new Vector3I(0, 10, 0), 3, 3);
            PipeFitter.BuildPumplessRing(builder, pumpless);

            builder.Place(Catalog.CoolantPump(), new Vector3I(0, 20, 0),

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());
            CoolantLoopDiagnostics diagnostics = simulation.DiagnoseLoops();

            int coolantBlocks = 0;
            IList<BlockInstance> blocks = simulation.Grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Model.Coolant != null) coolantBlocks++;
            }

            Assert.Equal(8 + 8 + 1, coolantBlocks);
            Assert.Equal(coolantBlocks, diagnostics.PipesInLoops + diagnostics.PipesAdrift);

            Assert.Equal(2, diagnostics.Loops);
            Assert.Equal(16, diagnostics.PipesInLoops);
            Assert.Equal(1, diagnostics.PipesAdrift);
            Assert.Equal(1, diagnostics.CountOf(CoolantFault.OpenEnd));
        }

        [Fact]

        public void DiagnosingOneBlockAgreesWithDiagnosingTheGrid()
        {
            GridBuilder builder = GridBuilder.Large();

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            List<Vector3I> pumpless = PipeFitter.RectangleXZ(new Vector3I(0, 10, 0), 3, 3);
            PipeFitter.BuildPumplessRing(builder, pumpless);

            builder.Place(Catalog.CoolantPump(), new Vector3I(0, 20, 0),

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            int[] perBlock = new int[6];
            IList<BlockInstance> blocks = simulation.Grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Model.Coolant == null) continue;
                perBlock[(int)simulation.DiagnoseBlock(blocks[i])]++;
            }

            CoolantLoopDiagnostics whole = simulation.DiagnoseLoops();

            for (int fault = 1; fault < perBlock.Length; fault++)
            {
                Assert.Equal(whole.Counts[fault], perBlock[fault]);
            }

            Assert.Equal(whole.PipesInLoops, perBlock[(int)CoolantFault.None]);
        }

        [Fact]

        public void ABlockInAWorkingLoopReportsItsLoopAndNoFault()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring = PipeFitter.BuildRing(
                builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            for (int i = 0; i < ring.Count; i++)
            {
                Assert.Equal(CoolantFault.None, simulation.DiagnoseBlock(ring[i]));
                Assert.NotNull(simulation.FindLoopContaining(ring[i]));
            }
        }

        [Fact]

        public void ABlockWithNoPlumbingIsNotDiagnosedAtAll()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            BlockInstance armour = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(Isolated());

            Assert.Equal(CoolantFault.None, simulation.DiagnoseBlock(armour));
            Assert.Null(simulation.FindLoopContaining(armour));
        }


        private static string Reasons(CoolantLoopDiagnostics diagnostics)
        {
            string text = "";
            for (int i = 1; i < diagnostics.Counts.Length; i++)
            {
                if (diagnostics.Counts[i] == 0) continue;
                text += CoolantLoopDiagnostics.Describe((CoolantFault)i) + " x" + diagnostics.Counts[i] + "; ";
            }
            return text.Length == 0 ? "no faults" : text;
        }
    }
}
