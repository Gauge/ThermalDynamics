using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Two one-parcel loops cover reordered carry-over, a self-aliased input, dissolution,
    /// and reuse of a formerly absent signature. Frozen spill/reclaim arithmetic checks that
    /// retained lookup storage cannot move or resurrect heat, fill, or pump settings.
    /// A rotated pipe and target compare direct sink iteration with the public transform oracle.
    /// </summary>
    public class LoopRebuildScratchTests
    {
        [Fact]
        public void ReusedLookupsPreserveCarrySpillAndAliasedInputWithoutRetainingDevices()
        {
            var settings = new ThermalSettings { EnableEnvironment = false, EnableSolarHeat = false,
                EnableRoomAir = false, EnableDamage = false, HeatTimeScale = 2f };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var model = BlockModel.Solid("pipe", Vector3I.One, 10f,
                new BlockThermalProperties { SpecificHeat = 200f });
            var firstPipe = new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity);
            var secondPipe = new BlockInstance(model, new Vector3I(2, 0, 0), BlockOrientation.Identity);
            simulation.AddBlock(firstPipe, 310f);
            simulation.AddBlock(secondPipe, 330f);
            var solver = simulation.Solver;
            var first = Loop(firstPipe, 470f);
            var second = Loop(secondPipe, 710f);
            solver.SetLoops(new List<CoolantLoop> { first, second });
            first.FillFraction = .25f;
            second.FillFraction = .75f;
            first.Pumps[0].Speed = .2f;
            first.Pumps[0].Enabled = false;
            first.Pumps[0].PowerAvailable = .3f;
            first.Pumps[0].MaxPowerWatts = 1234f;
            second.Pumps[0].Speed = .8f;
            second.Pumps[0].PowerAvailable = .7f;
            second.Pumps[0].MaxPowerWatts = 5678f;

            CoolantLoop nextFirst = Loop(firstPipe, 100f);
            CoolantLoop nextSecond = Loop(secondPipe, 200f);
            solver.SetLoops(new List<CoolantLoop> { nextSecond, nextFirst });
            Assert.Same(nextSecond, solver.Loops[0]);
            Assert.Same(nextFirst, solver.Loops[1]);
            Assert.Equal(470f, nextFirst.Temperature);
            Assert.Equal(710f, nextSecond.Temperature);
            Assert.Equal(.25f, nextFirst.FillFraction);
            Assert.Equal(.75f, nextSecond.FillFraction);
            AssertPump(first.Pumps[0], nextFirst.Pumps[0]);
            AssertPump(second.Pumps[0], nextSecond.Pumps[0]);
            Assert.Equal(310f, solver.GetNode(firstPipe).Temperature);
            Assert.Equal(330f, solver.GetNode(secondPipe).Temperature);

            // A copied list and the exposed list must request the same replacement.
            var exposed = (List<CoolantLoop>)solver.Loops;
            solver.SetLoops(exposed);
            Assert.Same(exposed, solver.Loops);
            Assert.Equal(2, exposed.Count);
            Assert.Same(nextSecond, exposed[0]);
            Assert.Same(nextFirst, exposed[1]);
            Assert.Equal(470f, nextFirst.Temperature);
            Assert.Equal(710f, nextSecond.Temperature);
            AssertScratchReleased(solver);

            ThermalNode node = solver.GetNode(firstPipe);
            float parcelMass = nextFirst.ThermalMass / nextFirst.PipeCount;
            float expectedSpill = ((node.Temperature * node.ThermalMass)
                + (nextFirst.SegmentTemperature(0) * parcelMass)) / (node.ThermalMass + parcelMass);
            float expectedHeld = node.HeldCoolantCapacity + parcelMass * nextFirst.HeatTimeScale;
            nextSecond.Temperature = 811f;
            solver.SetLoops(new List<CoolantLoop> { nextSecond });
            Assert.Single(solver.Loops);
            Assert.Equal(expectedSpill, node.Temperature);
            Assert.Equal(expectedHeld, node.HeldCoolantCapacity);
            Assert.Equal(811f, nextSecond.Temperature);
            Assert.Equal(0f, solver.GetNode(secondPipe).HeldCoolantCapacity);

            // The removed signature must not carry its old temperature, fill, or pump state.
            CoolantLoop returned = Loop(firstPipe, 321f);
            returned.HeatTimeScale = settings.HeatTimeScale;
            float share = expectedHeld / returned.HeatTimeScale;
            float covered = share > returned.SegmentThermalMass ? returned.SegmentThermalMass : share;
            float landed = ((expectedSpill * share) * (covered / share))
                + (321f * (returned.SegmentThermalMass - covered));
            float expectedReclaim = landed / returned.SegmentThermalMass;
            solver.SetLoops(new List<CoolantLoop> { returned, nextSecond });
            Assert.Equal(expectedReclaim, returned.Temperature);
            Assert.Equal(1f, returned.FillFraction);
            Assert.True(returned.Pumps[0].Enabled);
            Assert.Equal(1f, returned.Pumps[0].Speed);
            Assert.Equal(1f, returned.Pumps[0].PowerAvailable);
            Assert.Equal(0f, returned.Pumps[0].MaxPowerWatts);
            Assert.Equal(0f, node.HeldCoolantCapacity);
            Assert.Equal(811f, nextSecond.Temperature);
            AssertPump(second.Pumps[0], nextSecond.Pumps[0]);
            AssertScratchReleased(solver);

            solver.SetLoops(null);
            var twoParcels = Loop(firstPipe, 300f);
            twoParcels.Pipes.Add(secondPipe);
            twoParcels.RefreshSignature();
            solver.SetLoops(new List<CoolantLoop> { twoParcels });
            twoParcels.SetSegmentTemperature(0, 350f);
            twoParcels.SetSegmentTemperature(1, 950f);
            solver.SetLoops((List<CoolantLoop>)solver.Loops);
            // Self-alias follows ordinary replacement: preserve the mean, reseed parcels.
            Assert.Equal(650f, twoParcels.SegmentTemperature(0));
            Assert.Equal(650f, twoParcels.SegmentTemperature(1));
            solver.SetLoops(null);
            solver.SetLoops(new List<CoolantLoop>());
            Assert.Empty(solver.Loops);
            AssertScratchReleased(solver);
        }

        [Fact]
        public void DirectSinkIterationMatchesPublicPortTransformsAndFiltering()
        {
            var settings = new ThermalSettings { EnableEnvironment = false, EnableSolarHeat = false,
                EnableRoomAir = false, EnableDamage = false };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var pipeModel = BlockModel.Solid("rotated pipe", new Vector3I(2, 1, 1), 10f,
                new BlockThermalProperties { SpecificHeat = 200f });
            var shape = new CoolantShape
            {
                SinkPorts = new[]
                {
                    new CoolantPort(Vector3I.UnitX, Vector3I.UnitX),
                    new CoolantPort(Vector3I.Zero, Vector3I.Zero), // Self, first cell.
                    new CoolantPort(Vector3I.UnitX, Vector3I.UnitY), // Missing target.
                    new CoolantPort(Vector3I.UnitX, Vector3I.UnitX), // Duplicate target.
                    new CoolantPort(Vector3I.UnitX, Vector3I.Zero), // Self, second cell.
                    // Preserve the public transform's behavior for nonstandard authored cells.
                    new CoolantPort(new Vector3I(2, 0, 0), Vector3I.Zero),
                }
            };
            pipeModel.Coolant = shape;
            var pipe = new BlockInstance(pipeModel, new Vector3I(7, -3, 11),
                new BlockOrientation(Base6Directions.Direction.Left, Base6Directions.Direction.Backward));
            simulation.AddBlock(pipe, 400f);
            List<GridPort> publicPorts = pipe.CoolantSinkPorts();
            var target = new BlockInstance(BlockModel.Solid("target", Vector3I.One, 5f,
                new BlockThermalProperties { SpecificHeat = 100f }), publicPorts[0].Target,
                BlockOrientation.Identity);
            simulation.AddBlock(target, 300f);
            // The public method continues to return independent, caller-owned lists.
            publicPorts.Clear();
            Assert.Equal(shape.SinkPorts.Length, pipe.CoolantSinkPorts().Count);

            var loop = Loop(pipe, 500f);
            var incoming = new List<CoolantLoop> { loop };
            simulation.Solver.SetLoops(incoming);
            Assert.Equal(4, loop.Links.Count);
            AssertSinkOracle(simulation, loop);

            // A target may remain in occupancy while being excluded from thermal simulation.
            Assert.True(simulation.Solver.RemoveBlock(target));
            simulation.Solver.SetLoops(incoming);
            Assert.Single(loop.Links);
            AssertSinkOracle(simulation, loop);

            shape.SinkPorts = null;
            simulation.Solver.SetLoops(incoming);
            Assert.Single(loop.Links);
            AssertSinkOracle(simulation, loop);
            pipeModel.Coolant = null;
            simulation.Solver.SetLoops(incoming);
            Assert.Single(loop.Links);
            AssertSinkOracle(simulation, loop);
        }

        private static void AssertSinkOracle(ThermalSimulation simulation, CoolantLoop loop)
        {
            var expected = new List<LoopLink>();
            for (int p = 0; p < loop.Pipes.Count; p++)
            {
                BlockInstance pipe = loop.Pipes[p];
                ThermalNode pipeNode = simulation.Solver.GetNode(pipe);
                if (pipeNode != null)
                    expected.Add(new LoopLink(pipeNode.Index,
                        CoolantLoopBuilder.PipeConductance(simulation.Grid, pipe, loop.Properties), p));
                foreach (GridPort port in pipe.CoolantSinkPorts())
                {
                    BlockInstance target = simulation.Grid.GetAtCell(port.Target);
                    if (target == null || target == pipe) continue;
                    ThermalNode node = simulation.Solver.GetNode(target);
                    if (node == null) continue;
                    expected.Add(new LoopLink(node.Index,
                        CoolantLoopBuilder.PlateConductance(simulation.Grid, loop.Properties), p));
                }
            }
            Assert.Equal(expected.Count, loop.Links.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].NodeIndex, loop.Links[i].NodeIndex);
                Assert.Equal(expected[i].SegmentIndex, loop.Links[i].SegmentIndex);
                Assert.Equal(expected[i].Conductance, loop.Links[i].Conductance);
            }
        }

        private static CoolantLoop Loop(BlockInstance pipe, float temperature)
        {
            var loop = new CoolantLoop(LoopThermalProperties.Default(), temperature);
            loop.Pipes.Add(pipe);
            loop.Pumps.Add(new CoolantPump { Block = pipe });
            loop.RefreshSignature();
            loop.RefreshThermalMass();
            return loop;
        }

        private static void AssertPump(CoolantPump expected, CoolantPump actual)
        {
            Assert.Equal(expected.Speed, actual.Speed);
            Assert.Equal(expected.Enabled, actual.Enabled);
            Assert.Equal(expected.PowerAvailable, actual.PowerAvailable);
            Assert.Equal(expected.MaxPowerWatts, actual.MaxPowerWatts);
        }

        private static void AssertScratchReleased(ThermalSolver solver)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.Empty((IDictionary)typeof(ThermalSolver).GetField("rememberedLoops", Flags).GetValue(solver));
            Assert.Empty((IDictionary)typeof(ThermalSolver).GetField("rememberedLoopPumps", Flags).GetValue(solver));
            Assert.Empty((IList)typeof(ThermalSolver).GetField("loopInputScratch", Flags).GetValue(solver));
        }
    }
}
