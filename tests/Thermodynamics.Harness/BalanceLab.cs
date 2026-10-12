using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class BalanceLab
    {
        public const float ReferenceTemperature = 600f;

        public const float ReferenceAmbient = 2.7f;


        public class BlockRow
        {
            public string Subtype;
            public string Kind;
            public bool Large;
            public int Cells;
            public float Mass;
            public int Pcu;
            public float BuildSeconds;

            public float SpecificHeat;
            public float Emissivity;
            public float Conductivity;
            public float ExposedSurfaceMultiplier;

            public float CapacityJoulesPerKelvin;

            public float ExposedAreaAlone;

            public float ShedAloneWatts;

            public float ShedMountedWatts;

            public float ConductanceWattsPerKelvin;

            public string JointFace = "none";

            public int MountFaceCount;

            public float ShedOverReach;
        }

        public class LoadRow
        {
            public string Subtype;
            public string TypeId;
            public bool Large;
            public float RatedMegawatts;

            public float WasteFraction;

            public float WasteWatts;

            public string Path;
        }

        public class DeliveredRow
        {
            public string Label;
            public int Count;

            public float SettledKelvin;

            public float KelvinSaved;

            public float AddedMass;
            public int AddedPcu;

            public float KelvinPerTonne;
        }

        public class PumpRow
        {
            public float ColdKelvin;
            public float GapKelvin;
            public float Coefficient;
            public float LiftedWatts;
            public float DrawnWatts;

            public string Binding;
        }

        public class LoopRow
        {
            public int Pipes;
            public int Sinks;
            public float FluidThermalMass;
            public float CouplingWattsPerKelvin;
            public float SettledKelvin;
            public float KelvinSaved;
        }


        public static BlockModel Heater()
        {
            BlockThermalProperties thermal = Catalog.ReactorThermal();
            thermal.ProducerWasteEnergy = 1f;
            thermal.ConsumerWasteEnergy = 1f;
            return BlockModel.Solid("Heater", Vector3I.One, 3000f, thermal);
        }



        public static List<BlockRow> Blocks()
        {

            List<BlockRow> rows = new List<BlockRow>();

            ThermalSettings settings = new HistoricalThermalSettings();

            foreach (string subtype in ShippedBlocks.Subtypes())
            {
                ShippedBlocks.Definition definition = ShippedBlocks.Get(subtype);
                rows.Add(Measure(subtype, Kind(subtype), definition.Large, definition.Size,
                    definition.Mass, definition.Pcu, definition.BuildSeconds, definition.Thermal,
                    ShippedBlocks.Model(subtype), settings));
            }

            foreach (Vanilla.Block block in Vanilla.Reference)
            {
                if (block.TypeId != "CubeBlock") continue;

                BlockThermalProperties thermal = Catalog.DefaultThermal();
                BlockModel model = BlockModel.Solid(block.Subtype, block.Size, block.Mass, thermal);
                rows.Add(Measure(block.Subtype, "vanilla armour", block.Large, block.Size,
                    block.Mass, block.Pcu, block.BuildSeconds, thermal, model, settings));
            }

            return rows;
        }


        private static BlockRow Measure(string subtype, string kind, bool large, Vector3I size,
            float mass, int pcu, float buildSeconds, BlockThermalProperties thermal,
            BlockModel model, ThermalSettings settings)
        {
            float gridSize = large ? Catalog.LargeGridSize : Catalog.SmallGridSize;

            BlockRow row = new BlockRow
            {
                Subtype = subtype,
                Kind = kind,
                Large = large,
                Cells = size.X * size.Y * size.Z,
                Mass = mass,
                Pcu = pcu,
                BuildSeconds = buildSeconds,
                SpecificHeat = thermal.SpecificHeat,
                Emissivity = thermal.Emissivity,
                Conductivity = thermal.Conductivity,
                ExposedSurfaceMultiplier = thermal.ExposedSurfaceMultiplier,
                CapacityJoulesPerKelvin = mass * thermal.SpecificHeat / settings.HeatTimeScale,
            };


            GridBuilder alone = new GridBuilder(gridSize);
            alone.Place(model, Vector3I.Zero);
            ThermalSimulation simulation = alone.BuildSimulation(settings, ReferenceTemperature);
            ThermalNode node = simulation.Solver.GetNodeAt(Vector3I.Zero);

            row.ExposedAreaAlone = node.ExposedArea;

            row.ShedAloneWatts = Radiated(node, ReferenceTemperature);


            int joint = FirstMountFace(model);
            row.JointFace = joint < 0 ? "none" : Face.Name(joint);

            row.MountFaceCount = MountFaceCount(model);

            row.ShedMountedWatts = MountedShedding(model, gridSize, settings, joint,
                out row.ConductanceWattsPerKelvin);

            float reach = row.ConductanceWattsPerKelvin * 100f;
            row.ShedOverReach = reach > 0f ? row.ShedMountedWatts / reach : 0f;
            return row;
        }


        private static float Radiated(ThermalNode node, float temperature)
        {
            double hot = (double)temperature * temperature * temperature * temperature;
            double cold = (double)ReferenceAmbient * ReferenceAmbient * ReferenceAmbient * ReferenceAmbient;
            return (float)(node.RadiationCoefficient * (hot - cold));
        }


        private static int MountFaceCount(BlockModel model)
        {
            int count = 0;
            for (int face = 0; face < Face.Count; face++)
            {
                if (model.LocalFaceMountFraction(face) > 0f) count++;
            }
            return count;
        }


        private static int FirstMountFace(BlockModel model)
        {
            for (int face = 0; face < Face.Count; face++)
            {
                if (model.LocalFaceMountFraction(face) > 0f) return face;
            }
            return -1;
        }


        private static float MountedShedding(BlockModel model, float gridSize, ThermalSettings settings,
            int joint, out float conductance)
        {

            GridBuilder builder = new GridBuilder(gridSize);
            BlockModel armour = BlockModel.Solid("hull", Vector3I.One, 500f, Catalog.DefaultThermal());

            builder.Place(model, Vector3I.Zero);
            BlockInstance placed = builder.Last;

            if (joint >= 0)
            {
                Vector3I extents = model.Extents;
                Vector3I step = Face.Offsets[joint];
                int axis = Face.Axis(joint);

                foreach (Vector3I cell in model.LocalCells())
                {
                    int along = axis == 0 ? cell.X : axis == 1 ? cell.Y : cell.Z;
                    int span = axis == 0 ? extents.X : axis == 1 ? extents.Y : extents.Z;

                    bool onFace = IsPositive(step) ? along == span - 1 : along == 0;
                    if (onFace) builder.Place(armour, cell + step);
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, ReferenceTemperature);
            ThermalNode node = simulation.Solver.GetNodeAt(placed.Min);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int self = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (ReferenceEquals(nodes[i], node)) self = i;
            }

            conductance = 0f;
            foreach (ThermalLink link in simulation.Solver.Links)
            {
                if (link.NodeA == self || link.NodeB == self) conductance += link.Conductance;
            }


            return Radiated(node, ReferenceTemperature);
        }


        private static bool IsPositive(Vector3I step)
        {
            return step.X > 0 || step.Y > 0 || step.Z > 0;
        }


        private static string Kind(string subtype)
        {
            if (subtype.Contains("Radiator")) return "radiator";
            if (subtype.Contains("HeatPump")) return "heat pump";
            if (subtype.Contains("CoolantPump")) return "coolant pump";
            if (subtype.Contains("CoolantPipe")) return "coolant pipe";
            return "other";
        }



        public static List<LoadRow> Loads()
        {

            List<LoadRow> rows = new List<LoadRow>();

            foreach (Vanilla.Block block in Vanilla.Reference)
            {
                if (block.TypeId == "CubeBlock") continue;

                BlockThermalProperties thermal = block.Thermal;
                bool produces = block.PowerOutputMegawatts > 0f && block.TypeId != "Thrust";
                float rated = produces ? block.PowerOutputMegawatts : block.PowerDrawMegawatts;

                float fraction = produces ? thermal.ProducerWasteEnergy : thermal.ConsumerWasteEnergy;

                rows.Add(new LoadRow
                {
                    Subtype = block.Subtype,
                    TypeId = block.TypeId,
                    Large = block.Large,
                    RatedMegawatts = rated,
                    WasteFraction = fraction,
                    WasteWatts = rated * 1000000f * fraction,
                    Path = produces ? "produced" : "consumed",
                });
            }

            return rows;
        }



        public static List<DeliveredRow> Delivered()
        {

            List<DeliveredRow> rows = new List<DeliveredRow>();

            foreach (float watts in new float[] { 200000f, 2000000f })
            {
                rows.AddRange(DeliveredAt(watts));
            }
            return rows;
        }


        private static List<DeliveredRow> DeliveredAt(float watts)
        {

            List<DeliveredRow> rows = new List<DeliveredRow>();
            ShippedBlocks.Definition radiator = ShippedBlocks.Get("Gauge_LG_Radiator");

            string load = N(watts / 1000f, 0) + " kW ";


            float bare = Column(0, false, watts);
            rows.Add(new DeliveredRow { Label = load + "bare source", Count = 0, SettledKelvin = bare });

            foreach (int count in new int[] { 1, 2, 4, 8 })
            {

                float settled = Column(count, false, watts);
                float mass = radiator.Mass * count;
                rows.Add(new DeliveredRow
                {
                    Label = load + "radiators",
                    Count = count,
                    SettledKelvin = settled,
                    KelvinSaved = bare - settled,
                    AddedMass = mass,
                    AddedPcu = radiator.Pcu * count,
                    KelvinPerTonne = mass > 0f ? (bare - settled) / (mass / 1000f) : 0f,
                });
            }

            foreach (int count in new int[] { 1, 2, 4, 8 })
            {

                float settled = Column(count, true, watts);
                float mass = ArmourSlabMass * count;
                rows.Add(new DeliveredRow
                {
                    Label = load + "armour slab",
                    Count = count,
                    SettledKelvin = settled,
                    KelvinSaved = bare - settled,
                    AddedMass = mass,
                    AddedPcu = count,
                    KelvinPerTonne = mass > 0f ? (bare - settled) / (mass / 1000f) : 0f,
                });
            }

            return rows;
        }

        private const float ArmourSlabMass = 5000f;


        private static float Column(int panels, bool armourInstead, float watts)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Heater(), Vector3I.Zero);
            builder.Last.PowerConsumedWatts = watts;
            BlockInstance source = builder.Last;

            BlockModel panel = armourInstead
                ? BlockModel.Solid("ArmourSlab", new Vector3I(1, 5, 2), ArmourSlabMass, Catalog.DefaultThermal())
                : ShippedBlocks.Model("Gauge_LG_Radiator");

            for (int i = 0; i < panels; i++)
            {
                builder.Place(panel, new Vector3I(0, 1 + (i * 5), 0));
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("source", source);
            runner.Run(14400f, 1200f);

            float value;
            return runner.Final.Tracked.TryGetValue("source", out value) ? value : 0f;
        }


        public class SensitivityRow
        {
            public string Dial;
            public string Change;
            public float SettledKelvin;

            public float KelvinVersusShipped;

            public float PanelKelvin;

            public float JointDropKelvin;

            public float JointWattsPerKelvin;

            public float ThroughJointWatts;

            public float SubstepDemand;
        }


        public static List<SensitivityRow> Sensitivity()
        {

            List<SensitivityRow> rows = new List<SensitivityRow>();
            ShippedBlocks.Definition shipped = ShippedBlocks.Get("Gauge_LG_Radiator");


            float baseline = PanelColumn(ShippedBlocks.Model("Gauge_LG_Radiator"));
            rows.Add(Row("(shipped)", "as built", ShippedBlocks.Model("Gauge_LG_Radiator"), baseline));

            foreach (float scaler in new float[] { 2.5f, 5f, 10f })
            {
                rows.Add(Row("ExposedSurfaceMultiplier", "x" + N(scaler / shipped.Thermal.ExposedSurfaceMultiplier, 1)
                    + "  (" + N(scaler, 2) + ")", Variant(shipped, t => t.ExposedSurfaceMultiplier = scaler), baseline));
            }

            rows.Add(Row("Emissivity", N(shipped.Thermal.Emissivity, 2) + " -> 1.00",
                Variant(shipped, t => t.Emissivity = 1f), baseline));

            rows.Add(Row("mount faces", "2/6 -> 6/6", MountEverywhere(shipped), baseline));
            rows.Add(Row("panel depth", "1x5x2 -> 1x1x2, same mass", ShortPath(shipped), baseline));
            rows.Add(Row("both of the above", "6/6 and 1x1x2", ShortPathEverywhere(shipped), baseline));

            rows.Add(CoolantFed(baseline, 0f, 0f));

            foreach (float coefficient in new float[] { 400f, 1000f, 2000f })
            {
                rows.Add(CoolantFed(baseline, coefficient, 0f));
            }

            foreach (float mass in new float[] { 200f, 515f, 820f, 2000f })
            {
                rows.Add(CoolantFed(baseline, 1000f, mass));
            }

            return rows;
        }


        private static SensitivityRow CoolantFed(float baseline, float coefficient, float massPerPipe)
        {
            GridBuilder builder = GridBuilder.Large();


            Vector3I sourceCell = new Vector3I(1, 0, 0);

            Vector3I panelTop = new Vector3I(4, 0, 0);

            builder.Place(Heater(), sourceCell);
            builder.Last.PowerConsumedWatts = 200000f;
            BlockInstance source = builder.Last;

            builder.Place(ShippedBlocks.Model("Gauge_LG_Radiator"), panelTop - new Vector3I(0, 4, 0));
            BlockInstance panel = builder.Last;

            List<Vector3I> ring = PipeFitter.RectangleXZ(new Vector3I(0, 1, 0), 6, 3);
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[ring.IndexOf(sourceCell + Vector3I.Up)] = Vector3I.Down;
            sinks[ring.IndexOf(panelTop + Vector3I.Up)] = Vector3I.Down;
            PipeFitter.BuildRing(builder, ring, -1, sinks);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            if (coefficient > 0f || massPerPipe > 0f)
            {
                LoopThermalProperties properties = LoopThermalProperties.Default();
                if (coefficient > 0f) properties.HeatTransferCoefficient = coefficient;
                if (massPerPipe > 0f) properties.CoolantMassPerPipe = massPerPipe;
                simulation.LoopProperties = properties;
                simulation.RebuildAll();
            }


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("source", source);
            runner.Track("panel", panel);
            runner.Run(14400f, 1200f);

            float sourceKelvin;
            float panelKelvin;
            runner.Final.Tracked.TryGetValue("source", out sourceKelvin);
            runner.Final.Tracked.TryGetValue("panel", out panelKelvin);

            float coupling = 0f;
            IList<CoolantLoop> loops = simulation.Solver.Loops;
            if (loops != null && loops.Count > 0)
            {
                foreach (LoopLink link in loops[0].Links) coupling = Math.Max(coupling, link.Conductance);
            }

            return new SensitivityRow
            {
                Dial = "coolant sink",

                Change = Describe(coefficient, massPerPipe),
                SettledKelvin = sourceKelvin,
                KelvinVersusShipped = baseline - sourceKelvin,
                PanelKelvin = panelKelvin,
                JointDropKelvin = sourceKelvin - panelKelvin,
                JointWattsPerKelvin = coupling,
                ThroughJointWatts = 0f,
                SubstepDemand = simulation.Solver.RequiredSubsteps(new HistoricalThermalSettings().StepSeconds),
            };
        }


        private static string Describe(float coefficient, float massPerPipe)
        {
            if (coefficient <= 0f && massPerPipe <= 0f) return "fed by a loop, not bolted";


            string text = coefficient > 0f ? "h " + N(coefficient, 0) : "h shipped";
            if (massPerPipe > 0f) text += ", " + N(massPerPipe, 0) + " kg/pipe";
            return text;
        }


        private static SensitivityRow Row(string dial, string change, BlockModel model, float baseline)
        {

            ColumnResult result = PanelColumnDetail(model);

            return new SensitivityRow
            {
                Dial = dial,
                Change = change,
                SettledKelvin = result.SourceKelvin,
                KelvinVersusShipped = baseline - result.SourceKelvin,
                PanelKelvin = result.PanelKelvin,
                JointDropKelvin = result.SourceKelvin - result.PanelKelvin,
                JointWattsPerKelvin = result.JointWattsPerKelvin,
                ThroughJointWatts = result.JointWattsPerKelvin * (result.SourceKelvin - result.PanelKelvin),
            };
        }


        private static BlockModel Variant(ShippedBlocks.Definition shipped, Action<BlockThermalProperties> change)
        {
            BlockModel model = ShippedBlocks.Model("Gauge_LG_Radiator");

            BlockThermalProperties thermal = Clone(shipped.Thermal);
            change(thermal);
            model.Thermal = thermal;
            return model;
        }


        private static BlockThermalProperties Clone(BlockThermalProperties source)
        {
            return source.Clone();
        }


        private static BlockModel MountEverywhere(ShippedBlocks.Definition shipped)
        {
            return BlockModel.Solid("RadiatorAllMounts", shipped.Size, shipped.Mass, shipped.Thermal);
        }


        private static BlockModel ShortPath(ShippedBlocks.Definition shipped)
        {
            BlockModel model = BlockModel.Solid("RadiatorShallow", new Vector3I(1, 1, 2),
                shipped.Mass, shipped.Thermal);

            foreach (Vector3I cell in model.LocalCells())
            {
                int state = CellSurface.SelfAirtightMask;
                state = CellSurface.WithSelfMount(state, Face.Up, true);
                state = CellSurface.WithSelfMount(state, Face.Down, true);
                model.SetLocalSurface(cell, state);
            }
            return model;
        }


        private static BlockModel ShortPathEverywhere(ShippedBlocks.Definition shipped)
        {
            return BlockModel.Solid("RadiatorShallowAllMounts", new Vector3I(1, 1, 2),
                shipped.Mass, shipped.Thermal);
        }

        private class ColumnResult
        {
            public float SourceKelvin;
            public float PanelKelvin;
            public float JointWattsPerKelvin;
        }


        private static float PanelColumn(BlockModel panel)
        {
            return PanelColumnDetail(panel).SourceKelvin;
        }


        private static ColumnResult PanelColumnDetail(BlockModel panel)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Heater(), Vector3I.Zero);
            builder.Last.PowerConsumedWatts = 200000f;
            BlockInstance source = builder.Last;
            builder.Place(panel, new Vector3I(0, 1, 0));
            BlockInstance placed = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("source", source);
            runner.Track("panel", placed);
            runner.Run(14400f, 1200f);


            ColumnResult result = new ColumnResult();
            runner.Final.Tracked.TryGetValue("source", out result.SourceKelvin);
            runner.Final.Tracked.TryGetValue("panel", out result.PanelKelvin);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int a = -1;
            int b = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (ReferenceEquals(nodes[i].Block, source)) a = i;
                if (ReferenceEquals(nodes[i].Block, placed)) b = i;
            }
            foreach (ThermalLink link in simulation.Solver.Links)
            {
                bool joins = (link.NodeA == a && link.NodeB == b) || (link.NodeA == b && link.NodeB == a);
                if (joins) result.JointWattsPerKelvin += link.Conductance;
            }
            return result;
        }



        public static List<PumpRow> Pump()
        {

            List<PumpRow> rows = new List<PumpRow>();

            ThermalSettings settings = new HistoricalThermalSettings();
            HeatPumpShape shape = ThermalHeatPumpShapes.Get("Gauge_LG_HeatPump", Vector3I.One);

            float cold = 300f;
            foreach (float gap in new float[] { 5f, 10f, 20f, 40f, 60f, 100f, 200f, 400f })
            {
                float raw = settings.HeatPumpCarnotFraction * cold / gap;
                float coefficient = Math.Min(raw, settings.HeatPumpMaxCoefficient);
                float lifted = Math.Min(coefficient * shape.MaxPowerWatts, shape.RatedWatts);
                float drawn = coefficient > 0f ? lifted / coefficient : 0f;

                string binding;
                if (raw > settings.HeatPumpMaxCoefficient) binding = "coefficient cap";
                else if (coefficient * shape.MaxPowerWatts >= shape.RatedWatts) binding = "rating";
                else binding = "carnot";

                rows.Add(new PumpRow
                {
                    ColdKelvin = cold,
                    GapKelvin = gap,
                    Coefficient = coefficient,
                    LiftedWatts = lifted,
                    DrawnWatts = drawn,
                    Binding = binding,
                });
            }

            return rows;
        }



        public static List<LoopRow> Loops()
        {

            List<LoopRow> rows = new List<LoopRow>();

            foreach (int side in new int[] { 3, 4, 6, 8 })
            {
                rows.Add(Ring(side));
            }
            return rows;
        }


        private static LoopRow Ring(int side)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));


            Vector3I under = new Vector3I(1, 0, 0);
            BlockInstance occupant = builder.Grid.GetAtCell(under);
            builder.Grid.Remove(occupant);
            builder.Placed.Remove(occupant);
            builder.Place(Heater(), under);
            builder.Last.PowerConsumedWatts = 500000f;
            BlockInstance source = builder.Last;

            List<Vector3I> ring = PipeFitter.RectangleXZ(new Vector3I(0, 1, 0), side, side);

            int above = ring.IndexOf(under + Vector3I.Up);
            if (above < 0) throw new InvalidOperationException("No ring cell sits above the source");

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[above] = Vector3I.Down;

            List<BlockInstance> pipes = PipeFitter.BuildRing(builder, ring, -1, sinks);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("source", source);
            runner.Run(14400f, 1200f);

            float settled;
            runner.Final.Tracked.TryGetValue("source", out settled);

            float fluid = 0f;
            float coupling = 0f;
            IList<CoolantLoop> loops = simulation.Solver.Loops;
            if (loops != null)
            {
                foreach (CoolantLoop loop in loops)
                {
                    fluid += loop.ThermalMass;
                    foreach (LoopLink link in loop.Links) coupling += link.Conductance;
                }
            }

            return new LoopRow
            {
                Pipes = pipes.Count,
                Sinks = sinks.Count,
                FluidThermalMass = fluid,
                CouplingWattsPerKelvin = coupling,
                SettledKelvin = settled,
            };
        }



        private static string N(float value, int decimals = 1)
        {
            return value.ToString("n" + decimals, CultureInfo.InvariantCulture);
        }


        public static string Report()
        {

            StringBuilder sb = new StringBuilder();

            ThermalSettings settings = new HistoricalThermalSettings();

            sb.AppendLine("BLOCKS  (shed at " + N(ReferenceTemperature, 0) + " K into a "

                + N(ReferenceAmbient, 1) + " K sky; reach = conduction across 100 K)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-42} {1,-13} {2,5} {3,8} {4,5} {5,9} {6,8} {7,10} {8,6} {9,9} {10,10}",
                "block", "kind", "cells", "mass kg", "pcu", "cap J/K", "area m2", "shed W",
                "mounts", "joint W/K", "shed/reach"));

            foreach (BlockRow row in Blocks())
            {
                sb.AppendLine(string.Format("{0,-42} {1,-13} {2,5} {3,8} {4,5} {5,9} {6,8} {7,10} {8,6} {9,9} {10,10}",

                    row.Subtype, row.Kind, row.Cells, N(row.Mass, 0), row.Pcu,
                    N(row.CapacityJoulesPerKelvin, 0), N(row.ExposedAreaAlone, 1),
                    N(row.ShedMountedWatts, 0), row.MountFaceCount + "/6",
                    N(row.ConductanceWattsPerKelvin, 0), N(row.ShedOverReach, 2)));
            }

            sb.AppendLine();
            sb.AppendLine("LOAD  (waste heat at full rating, as the shipped definitions compute it)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-30} {1,-13} {2,10} {3,9} {4,10} {5,12}",
                "block", "type", "rated MW", "path", "fraction", "waste W"));

            foreach (LoadRow row in Loads())
            {
                sb.AppendLine(string.Format("{0,-30} {1,-13} {2,10} {3,9} {4,10} {5,12}",

                    row.Subtype, row.TypeId, N(row.RatedMegawatts, 2), row.Path,
                    N(row.WasteFraction, 3), N(row.WasteWatts, 0)));
            }

            sb.AppendLine();
            sb.AppendLine("DELIVERED  (one source, panels stacked on it, shadow, 4 h to steady state)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-28} {1,6} {2,12} {3,12} {4,10} {5,12}",
                "fit", "count", "settled K", "saved K", "mass kg", "K per tonne"));

            foreach (DeliveredRow row in Delivered())
            {
                sb.AppendLine(string.Format("{0,-28} {1,6} {2,12} {3,12} {4,10} {5,12}",

                    row.Label, row.Count, N(row.SettledKelvin, 1), N(row.KelvinSaved, 1),
                    N(row.AddedMass, 0), N(row.KelvinPerTonne, 2)));
            }

            sb.AppendLine();
            sb.AppendLine("SENSITIVITY  (200 kW source, one panel; which dial moves the answer)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-20} {1,-30} {2,10} {3,8} {4,9} {5,8} {6,10} {7,11} {8,9}",
                "dial", "change", "source K", "gain K", "panel K", "drop K", "joint W/K", "through W",
                "substeps"));

            foreach (SensitivityRow row in Sensitivity())
            {
                sb.AppendLine(string.Format("{0,-20} {1,-30} {2,10} {3,8} {4,9} {5,8} {6,10} {7,11} {8,9}",

                    row.Dial, row.Change, N(row.SettledKelvin, 1), N(row.KelvinVersusShipped, 1),
                    N(row.PanelKelvin, 1), N(row.JointDropKelvin, 1),
                    N(row.JointWattsPerKelvin, 0), N(row.ThroughJointWatts, 0),

                    row.SubstepDemand > 0f ? N(row.SubstepDemand, 2) : "-"));
            }

            sb.AppendLine();
            sb.AppendLine("HEAT PUMP  (large grid, cold side 300 K)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,8} {1,12} {2,10} {3,10} {4,16}",
                "gap K", "coefficient", "lifted W", "drawn W", "binding limit"));

            foreach (PumpRow row in Pump())
            {
                sb.AppendLine(string.Format("{0,8} {1,12} {2,10} {3,10} {4,16}",
                    N(row.GapKelvin, 0), N(row.Coefficient, 2), N(row.LiftedWatts, 0),
                    N(row.DrawnWatts, 0), row.Binding));
            }

            sb.AppendLine();
            sb.AppendLine("COOLANT RINGS  (500 kW block, one sink face)");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,7} {1,7} {2,12} {3,14} {4,12}",
                "pipes", "sinks", "fluid J/K", "coupling W/K", "settled K"));

            foreach (LoopRow row in Loops())
            {
                sb.AppendLine(string.Format("{0,7} {1,7} {2,12} {3,14} {4,12}",

                    row.Pipes, row.Sinks, N(row.FluidThermalMass, 0),
                    N(row.CouplingWattsPerKelvin, 0), N(row.SettledKelvin, 1)));
            }

            sb.AppendLine();
            sb.AppendLine("thermal clock: HeatTimeScale = " + N(settings.HeatTimeScale, 0));
            return sb.ToString();
        }


        public static string BlocksCsv()
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("subtype,kind,large,cells,mass_kg,pcu,build_s,specific_heat,emissivity,"
                + "conductivity,exposed_surface_multiplier,capacity_j_per_k,area_m2,shed_alone_w,shed_mounted_w,"
                + "mount_faces,joint_face,joint_w_per_k,shed_over_reach");

            foreach (BlockRow row in Blocks())
            {
                sb.AppendLine(string.Join(",", new string[]
                {
                    row.Subtype, row.Kind, row.Large ? "large" : "small",
                    row.Cells.ToString(CultureInfo.InvariantCulture),
                    F(row.Mass), row.Pcu.ToString(CultureInfo.InvariantCulture), F(row.BuildSeconds),
                    F(row.SpecificHeat), F(row.Emissivity), F(row.Conductivity), F(row.ExposedSurfaceMultiplier),
                    F(row.CapacityJoulesPerKelvin), F(row.ExposedAreaAlone), F(row.ShedAloneWatts),
                    F(row.ShedMountedWatts), row.MountFaceCount.ToString(CultureInfo.InvariantCulture),

                    row.JointFace, F(row.ConductanceWattsPerKelvin), F(row.ShedOverReach),
                }));
            }
            return sb.ToString();
        }


        private static string F(float value)
        {
            return value.ToString("r", CultureInfo.InvariantCulture);
        }
    }
}
