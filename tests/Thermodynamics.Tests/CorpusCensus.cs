using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What every ship in the corpus *is*, before anything is stepped — the terms G4 needs and the
    /// survey's outcome rows do not carry. **This costs a build, not a simulation**, so the full
    /// corpus is minutes rather than the survey's eight hours and can be re-run whenever a definition
    /// moves.
    ///
    /// <para>
    /// <c>census.csv</c> is one row per ship, <c>composition.csv</c> one per ship and heat-making block
    /// type. See balance.md, The datasets.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class CorpusCensus
    {
        private class Censused
        {
            public string Row;

            public List<string> Composition = new List<string>();
        }

        [Fact]

        public void EveryShipInTheCorpusIsCounted()
        {
            if (CorpusFixture.Files().Count == 0) return;

            List<Censused> results = CorpusFixture.Sweep("census", Count);

            Assert.True(results.Count > 0, "the corpus yielded no censusable ships");

            int withExposure = 0;
            int withPower = 0;
            foreach (Censused ship in results)
            {
                string[] fields = ship.Row.Split(',');
                if (Positive(fields, ExposedAreaColumn)) withExposure++;
                if (Positive(fields, InstalledPowerColumn)) withPower++;
            }

            Assert.True(withExposure > 0, "not one ship reported any exposed area");
            Assert.True(withPower > 0, "not one ship reported any installed power");

            int withSources = 0;
            foreach (Censused ship in results)
            {
                if (Positive(ship.Row.Split(','), HeatSourcesColumn)) withSources++;
            }
            Assert.True(withSources > 0, "not one ship reported a single heat source");
        }

        private const int ExposedAreaColumn = 11;

        private const int InstalledPowerColumn = 16;

        private const int HeatSourcesColumn = 31;


        private static bool Positive(string[] fields, int column)
        {
            float value;
            return column < fields.Length
                && float.TryParse(fields[column], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out value) && value > 0f;
        }

        public const string Header =
            "ship,workshop_id,large,blocks,grids,joints,rooms,"
            + "exposed_blocks,buried_blocks,buried_share,sealed_blocks,exposed_area_m2,"
            + "thermal_mass_j_per_k,mass_kg,mean_capacity_j_per_k,"
            + "armor_n,producer_n,installed_power_w,store_n,store_power_w,"
            + "thruster_n,thrust_n,tool_n,consumer_n,consumer_draw_w,other_n,"
            + "waste_idle_w,waste_full_w,waste_burn_w,"
            + "exposure_m2_per_kw,capacity_j_per_k_per_w,top_source,top_source_w,top_source_n,"
            + "heat_sources,w_per_m2,max_depth,heat_depth_mean,heat_depth_max,clumping,heat_gini,"
            + "local_w_max,local_w_per_m2_max,heat_spread_m,hottest_conductance_w_per_k,"
            + "shape_factor,lift_over_drag";

        public const string CompositionHeader =
            "ship,workshop_id,subtype,type_id,count,waste_full_w,share_of_waste";

        private struct Aerodynamics
        {
            public float ShapeFactor;

            public float LiftOverDrag;
        }


        private static Aerodynamics Aero(ShipAssembly assembly)
        {
            double shapeSum = 0d;
            double ratioSum = 0d;
            int directions = 0;

            for (int f = 0; f < Face.Count; f++)
            {
                Vector3 wind = Face.Normals[f];
                double weighted = 0d;
                double projected = 0d;
                Vector3 pressure = Vector3.Zero;

                for (int g = 0; g < assembly.Simulations.Count; g++)
                {
                    ThermalSimulation simulation = assembly.Simulations[g];
                    ThermalSolver solver = simulation.Solver;
                    CellBitset occupancy = simulation.Grid.Occupancy();

                    for (int i = 0; i < solver.Nodes.Count; i++)
                    {
                        ThermalNode node = solver.Nodes[i];
                        int total = node.TotalExposedFaces;
                        if (total <= 0 || node.ExposedArea <= 0f) continue;

                        double share = (double)node.GetExposedFaces(f) / total;
                        if (share <= 0d) continue;

                        double area = node.ExposedArea * share;
                        Vector3 normal = ShapeNormal.Of(occupancy, node.Block);
                        float factor = ShapeNormal.Factor(normal, wind);

                        projected += area;
                        weighted += area * factor;

                        pressure -= normal * (float)(area * factor);
                    }
                }

                if (projected <= 0d) continue;

                shapeSum += weighted / projected;
                directions++;

                float along = Vector3.Dot(pressure, wind);
                float drag = along < 0f ? -along : along;
                if (drag > 0f)
                {
                    Vector3 transverse = pressure - (along * wind);
                    ratioSum += transverse.Length() / drag;
                }
            }


            Aerodynamics aero = new Aerodynamics();

            aero.ShapeFactor = directions == 0 ? 1f : (float)(shapeSum / directions);
            aero.LiftOverDrag = directions == 0 ? 0f : (float)(ratioSum / directions);
            return aero;
        }


        private static Censused Count(Blueprints.Ship ship)
        {
            ShipAssembly assembly = ship.Build();
            if (assembly.NodeCount == 0) return null;

            Dictionary<string, GameBlocks.Definition> definitions = GameBlocks.ByModelName();


            int exposedBlocks = 0;
            int buried = 0;
            long sealedBlocks = 0;
            double exposedArea = 0d;
            double thermalMass = 0d;

            double mass = 0d;

            for (int g = 0; g < assembly.Simulations.Count; g++)
            {
                ThermalSolver solver = assembly.Simulations[g].Solver;
                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    ThermalNode node = solver.Nodes[i];
                    exposedArea += node.ExposedArea;
                    thermalMass += node.ThermalMass;
                    mass += node.Block.Mass;

                    if (node.ExposedArea > 0f) exposedBlocks++;
                    else
                    {
                        buried++;

                        if (solver.Nodes.Count >= 2 && solver.NodeConductanceTotal(i) <= 0f)
                        {
                            sealedBlocks++;
                        }
                    }
                }
            }


            int armor = 0, producers = 0, stores = 0, thrusters = 0, tools = 0, consumers = 0, other = 0;
            double installed = 0d, storePower = 0d, thrust = 0d, draw = 0d;
            Dictionary<string, int> counts = new Dictionary<string, int>();

            foreach (ThermalNode node in assembly.Nodes)
            {
                string name = node.Block.Name;
                int seen;
                counts[name] = counts.TryGetValue(name, out seen) ? seen + 1 : 1;

                GameBlocks.Definition definition;
                if (!definitions.TryGetValue(name, out definition)) { other++; continue; }

                if (definition.PowerOutputWatts > 0f)
                {
                    if (ShipLoad.IsStore(definition.TypeId))
                    {
                        stores++;
                        storePower += definition.PowerOutputWatts;
                    }
                    else
                    {
                        producers++;
                        installed += definition.PowerOutputWatts;
                    }
                }

                else if (definition.ThrustNewtons > 0f)
                {
                    thrusters++;
                    thrust += definition.ThrustNewtons;
                }

                else if (definition.TypeId == "CubeBlock")
                {
                    armor++;
                }

                else if (definition.PowerDrawWatts > 0f)
                {
                    consumers++;
                    draw += definition.PowerDrawWatts;
                }
                else other++;

                if (ShipLoad.IsTool(definition.TypeId)) tools++;
            }


            float idle = ShipLoad.Apply(assembly, ShipLoad.State.Idle);
            float burn = ShipLoad.Apply(assembly, ShipLoad.State.Burn(Face.Forward));

            float full = ShipLoad.Apply(assembly, ShipLoad.State.Full);

            HeatGeometry.Result geometry = HeatGeometry.Measure(assembly, ship.Large);

            Dictionary<string, double> wasteBySubtype = new Dictionary<string, double>();
            foreach (ThermalNode node in assembly.Nodes)
            {
                float watts = node.HeatGenerationWatts;
                if (watts <= 0f) continue;

                string name = node.Block.Name;
                double seen;
                wasteBySubtype[name] = wasteBySubtype.TryGetValue(name, out seen)
                    ? seen + watts : watts;
            }

            string topSource = "";
            double topWatts = 0d;
            foreach (KeyValuePair<string, double> entry in wasteBySubtype)
            {
                if (entry.Value <= topWatts) continue;
                topWatts = entry.Value;
                topSource = entry.Key;
            }

            double exposurePerKilowatt = full > 0f ? exposedArea / (full / 1000d) : 0d;
            double capacityPerWatt = full > 0f ? thermalMass / full : 0d;


            Censused censused = new Censused();


            StringBuilder row = new StringBuilder();
            row.Append(CorpusRecord.Text(ship.Name)).Append(',');
            row.Append(ship.WorkshopId.ToString(CultureInfo.InvariantCulture)).Append(',');
            row.Append(ship.Large ? 1 : 0).Append(',');
            row.Append(assembly.NodeCount).Append(',');
            row.Append(ship.Grids.Count).Append(',');
            row.Append(assembly.Bridges.Count).Append(',');
            row.Append(assembly.RoomCount).Append(',');
            row.Append(exposedBlocks).Append(',');
            row.Append(buried).Append(',');
            row.Append(CorpusRecord.Num(assembly.NodeCount > 0
                ? (float)buried / assembly.NodeCount : 0f)).Append(',');
            row.Append(sealedBlocks).Append(',');
            row.Append(CorpusRecord.Num((float)exposedArea)).Append(',');
            row.Append(CorpusRecord.Num((float)thermalMass)).Append(',');
            row.Append(CorpusRecord.Num((float)mass)).Append(',');
            row.Append(CorpusRecord.Num(assembly.NodeCount > 0
                ? (float)(thermalMass / assembly.NodeCount) : 0f)).Append(',');
            row.Append(armor).Append(',');
            row.Append(producers).Append(',');
            row.Append(CorpusRecord.Num((float)installed)).Append(',');
            row.Append(stores).Append(',');
            row.Append(CorpusRecord.Num((float)storePower)).Append(',');
            row.Append(thrusters).Append(',');
            row.Append(CorpusRecord.Num((float)thrust)).Append(',');
            row.Append(tools).Append(',');
            row.Append(consumers).Append(',');
            row.Append(CorpusRecord.Num((float)draw)).Append(',');
            row.Append(other).Append(',');
            row.Append(CorpusRecord.Num(idle)).Append(',');
            row.Append(CorpusRecord.Num(full)).Append(',');
            row.Append(CorpusRecord.Num(burn)).Append(',');
            row.Append(CorpusRecord.Num((float)exposurePerKilowatt)).Append(',');
            row.Append(CorpusRecord.Num((float)capacityPerWatt)).Append(',');
            row.Append(CorpusRecord.Text(topSource)).Append(',');
            row.Append(CorpusRecord.Num((float)topWatts)).Append(',');
            row.Append(topSource.Length == 0 || !counts.ContainsKey(topSource)
                ? 0 : counts[topSource]).Append(',');
            row.Append(geometry.Sources).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.WattsPerSquareMetre)).Append(',');
            row.Append(geometry.MaxDepth).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.HeatDepthMean)).Append(',');
            row.Append(geometry.HeatDepthMax).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.Clumping)).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.Gini)).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.LocalWattsMax)).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.LocalWattsPerAreaMax)).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.SpreadMetres)).Append(',');
            row.Append(CorpusRecord.Num((float)geometry.HottestSourceConductance)).Append(',');

            Aerodynamics aero = Aero(assembly);
            row.Append(CorpusRecord.Num(aero.ShapeFactor)).Append(',');
            row.Append(CorpusRecord.Num(aero.LiftOverDrag));

            censused.Row = row.ToString();

            foreach (KeyValuePair<string, double> entry in wasteBySubtype)
            {
                GameBlocks.Definition definition;
                definitions.TryGetValue(entry.Key, out definition);


                StringBuilder line = new StringBuilder();
                line.Append(CorpusRecord.Text(ship.Name)).Append(',');
                line.Append(ship.WorkshopId.ToString(CultureInfo.InvariantCulture)).Append(',');
                line.Append(CorpusRecord.Text(entry.Key)).Append(',');
                line.Append(CorpusRecord.Text(definition == null ? "" : definition.TypeId)).Append(',');
                line.Append(counts.ContainsKey(entry.Key) ? counts[entry.Key] : 0).Append(',');
                line.Append(CorpusRecord.Num((float)entry.Value)).Append(',');
                line.Append(CorpusRecord.Num(full > 0f ? (float)(entry.Value / full) : 0f));
                censused.Composition.Add(line.ToString());
            }

            if (CorpusRecord.On)
            {
                CorpusRecord.Write("census", Header, new List<string> { censused.Row });
                if (censused.Composition.Count > 0)
                {
                    CorpusRecord.Write("composition", CompositionHeader, censused.Composition);
                }
            }

            return censused;
        }

    }
}
