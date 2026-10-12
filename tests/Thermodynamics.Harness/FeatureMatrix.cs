using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class FeatureMatrix
    {
        private class Toggle
        {
            public string Name;
            public Action<HistoricalThermalSettings, bool> Set;
        }

        private static readonly Toggle[] Toggles =
        {
            new Toggle { Name = "conduction",  Set = (s, v) => s.EnableConduction = v },
            new Toggle { Name = "radiation",   Set = (s, v) => s.EnableRadiation = v },
            new Toggle { Name = "convection",  Set = (s, v) => s.EnableConvection = v },
            new Toggle { Name = "solar",       Set = (s, v) => s.EnableSolarHeat = v },
            new Toggle { Name = "waste",       Set = (s, v) => s.EnableWasteHeat = v },
            new Toggle { Name = "loops",       Set = (s, v) => s.EnableCoolantLoops = v },
            new Toggle { Name = "roomair",     Set = (s, v) => s.EnableRoomAir = v },
            new Toggle { Name = "heatpumps",   Set = (s, v) => s.EnableHeatPumps = v },
            new Toggle { Name = "friction",    Set = (s, v) => s.EnableFriction = v },
            new Toggle { Name = "damage",      Set = (s, v) => s.EnableDamage = v },
            new Toggle { Name = "shadowing",   Set = (s, v) => s.SolarSelfShadowing = v },
            new Toggle { Name = "wellmixed",   Set = (s, v) => s.WellMixedCoolant = !v },
        };

        public class Row
        {
            public string Profile;
            public string Combination;
            public float PeakKelvin;
            public float ColdestKelvin;
            public bool Converged;
            public bool Diverged;
            public bool Failed;
            public string Error;
            public float SubstepsMean;

            public bool RunawayExpected;
        }


        private static ScenarioRunner Rig(HistoricalThermalSettings settings)
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 3, 4));

            BlockInstance floor = builder.Grid.GetAtCell(new Vector3I(1, 0, 1));
            if (floor != null)
            {
                builder.Grid.Remove(floor);
                builder.Placed.Remove(floor);
            }
            builder.Place(Catalog.Reactor(), new Vector3I(1, 0, 1));
            BlockInstance source = builder.Last;
            source.PowerConsumedWatts = 2000000f;

            List<Vector3I> ring = PipeFitter.RectangleXZ(new Vector3I(0, 3, 0), 4, 4);
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            int index = PipeFitter.FirstStraightIndexAvoiding(ring, sinks);
            sinks[index] = Vector3I.Down;
            PipeFitter.BuildRing(builder, ring, -1, sinks);

            builder.Place(Catalog.Radiator(), new Vector3I(0, 4, 0));
            builder.Place(Catalog.HeatPump(), new Vector3I(2, 4, 1));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.SetRoomPressure(new Vector3I(2, 1, 2), 1f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.PlanetSurface(0.6f, timeOfDay: 0.3f, windSpeed: 40f);
            runner.Track("source", source);
            runner.Run(1800f, 60f);
            return runner;
        }


        public static List<Row> Run()
        {

            List<Row> rows = new List<Row>();

            foreach (BalanceProfile profile in BalanceProfile.All())
            {
                rows.Add(Measure(profile, "all on", null, true));

                foreach (Toggle toggle in Toggles)
                {
                    rows.Add(Measure(profile, "no " + toggle.Name, toggle, false));
                }
                foreach (Toggle toggle in Toggles)
                {
                    rows.Add(Measure(profile, "only " + toggle.Name, toggle, true));
                }
            }
            return rows;
        }


        private static Row Measure(BalanceProfile profile, string label, Toggle single, bool only)
        {
            Row row = new Row { Profile = profile.Name, Combination = label };

            HistoricalThermalSettings wanted = profile.ToSettings();
            if (single != null)
            {
                if (only)
                {
                    foreach (Toggle toggle in Toggles) toggle.Set(wanted, false);
                    single.Set(wanted, true);
                }
                else
                {
                    single.Set(wanted, false);
                }
                wanted.Derive();
            }

            bool source = wanted.EnableWasteHeat || wanted.EnableSolarHeat
                || wanted.EnableFriction || wanted.EnableHeatPumps;
            bool sink = wanted.EnableRadiation || wanted.EnableConvection;
            row.RunawayExpected = source && !sink;


            GridBuilder.SettingsOverride = existing => Copy(existing, wanted);
            Catalog.MaterialOverride = profile.Material;

            try
            {

                ScenarioRunner runner = Rig(wanted);
                if (runner == null || runner.Samples.Count == 0)
                {
                    row.Failed = true;
                    row.Error = "no-samples";
                    return row;
                }

                Sample last = runner.Final;
                row.PeakKelvin = last.HottestTemperature;
                row.SubstepsMean = last.Substeps;

                float coldest = float.MaxValue;
                IList<ThermalNode> nodes = runner.Simulation.Solver.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Temperature < coldest) coldest = nodes[i].Temperature;
                }
                row.ColdestKelvin = coldest == float.MaxValue ? 0f : coldest;

                row.Converged = ProfileSweep.Settled(runner.Samples);

                row.Diverged = float.IsNaN(row.PeakKelvin) || float.IsInfinity(row.PeakKelvin)
                    || row.ColdestKelvin < 0f
                    || (row.PeakKelvin > ProfileSweep.DivergenceKelvin && !row.Converged);
            }
            catch (Exception error)
            {
                row.Failed = true;
                row.Error = error.GetType().Name;
            }
            finally
            {
                GridBuilder.SettingsOverride = null;
                Catalog.MaterialOverride = null;
            }

            return row;
        }


        private static ThermalSettings Copy(ThermalSettings target, HistoricalThermalSettings wanted)
        {
            target.HeatTimeScale = wanted.HeatTimeScale;
            target.Frequency = wanted.Frequency;
            target.MaxSubsteps = wanted.MaxSubsteps;
            target.ClampConductionOvershoot = wanted.ClampConductionOvershoot;
            target.ClampEnvironmentOvershoot = wanted.ClampEnvironmentOvershoot;
            target.SolarEnergy = wanted.SolarEnergy;
            target.VacuumTemperature = wanted.VacuumTemperature;
            target.RoomConvectionCoefficient = wanted.RoomConvectionCoefficient;

            target.EnableConduction = wanted.EnableConduction;
            target.EnableRadiation = wanted.EnableRadiation;
            target.EnableConvection = wanted.EnableConvection;
            target.EnableSolarHeat = wanted.EnableSolarHeat;
            target.EnableWasteHeat = wanted.EnableWasteHeat;
            target.EnableCoolantLoops = wanted.EnableCoolantLoops;
            target.EnableRoomAir = wanted.EnableRoomAir;
            target.EnableHeatPumps = wanted.EnableHeatPumps;
            target.SurfaceHeating = wanted.SurfaceHeating;
            target.SurfaceHeatingGate = wanted.SurfaceHeatingGate;
            target.EnableDamage = wanted.EnableDamage;
            target.SolarSelfShadowing = wanted.SolarSelfShadowing;
            target.WellMixedCoolant = wanted.WellMixedCoolant;
            return target.Derive();
        }


        private static string N(float value, int decimals = 1)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "inf" : value.ToString("n" + decimals, CultureInfo.InvariantCulture);
        }


        public static string Report()
        {

            List<Row> rows = Run();

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("FEATURE MATRIX — one rig with every mechanism on it, combinations toggled");
            sb.AppendLine();
            sb.AppendLine("Only rows that broke are listed. A combination that merely runs cooler is");
            sb.AppendLine("doing its job, and one with a source and no sink is *supposed* to run away —");
            sb.AppendLine("those are counted separately rather than reported as defects.");
            sb.AppendLine();
            sb.AppendLine(string.Format("{0,-11} {1,-18} {2,16} {3,12} {4,10}",
                "profile", "combination", "peak K", "coldest K", "why"));

            int bad = 0;
            foreach (Row row in rows)
            {
                if (!row.Failed && !row.Diverged) continue;
                if (row.RunawayExpected && !row.Failed) continue;
                bad++;
                sb.AppendLine(string.Format("{0,-11} {1,-18} {2,16} {3,12} {4,10}",

                    row.Profile, row.Combination, N(row.PeakKelvin, 0), N(row.ColdestKelvin, 1),
                    row.Failed ? row.Error : "DIVERGED"));
            }

            sb.AppendLine();
            int expected = 0;
            foreach (Row row in rows)
            {
                if (row.RunawayExpected && row.Diverged && !row.Failed) expected++;
            }

            sb.AppendLine(bad + " of " + rows.Count + " combinations broke ("
                + expected + " more ran away with a source and no sink, as they should).");
            sb.AppendLine();
            sb.AppendLine("PEAK BY COMBINATION — shipped profile, for the shape of each mechanism");
            sb.AppendLine();
            foreach (Row row in rows)
            {
                if (row.Profile != "shipped" || row.Failed) continue;
                sb.AppendLine(string.Format("  {0,-18} {1,12} {2,12}",

                    row.Combination, N(row.PeakKelvin, 1), row.Converged ? "" : "~"));
            }
            return sb.ToString();
        }


        public static string Csv()
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("profile,combination,peak_k,coldest_k,converged,diverged,failed,error,substeps");
            foreach (Row row in Run())
            {
                sb.AppendLine(string.Join(",", new string[]
                {
                    row.Profile, row.Combination,
                    row.PeakKelvin.ToString("r", CultureInfo.InvariantCulture),
                    row.ColdestKelvin.ToString("r", CultureInfo.InvariantCulture),
                    row.Converged ? "1" : "0", row.Diverged ? "1" : "0", row.Failed ? "1" : "0",
                    row.Error ?? "",
                    row.SubstepsMean.ToString("r", CultureInfo.InvariantCulture),
                }));
            }
            return sb.ToString();
        }
    }
}
