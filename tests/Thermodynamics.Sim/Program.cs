using Aerodynamics.Core;
using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Thermodynamics.Core;
using Thermodynamics.Harness;

namespace Thermodynamics.Sim
{
    public static class Program
    {

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
            {
                PrintUsage();
                return 0;
            }

            switch (args[0])
            {
                case "list":
                    foreach (string name in Scenarios.Names) Console.WriteLine("  " + name);
                    return 0;

                case "run":

                    return RunCommand(args);

                case "bench":

                    return BenchCommand(args);

                case "balance":

                    return BalanceCommand(args);

                case "wind":

                    return WindCommand(args);

                case "planets":

                    return PlanetsCommand(args);

                case "descent":

                    return DescentCommand(args);

                case "drift":

                    return DriftCommand(args);

                case "inputs":

                    return InputsCommand(args);

                case "prefabs":

                    return PrefabCommand(args);

                case "aero-environment":
                    if (args.Length != 1) { Console.Error.WriteLine("aero-environment takes no arguments"); return 1; }
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { game_content = GameBlocks.ContentPath(), authored_data = ShippedBlocks.DataRoot() }));
                    return 0;

                case "aero-corpus-inputs":
                    if (args.Length != 2) { Console.Error.WriteLine("aero-corpus-inputs requires a blueprint"); return 1; }
                    Console.WriteLine(AeroPopulationInputs.Selection(args[1]));
                    return 0;

                case "aero-population-drag":
                    if (args.Length != 3) { Console.Error.WriteLine("aero-population-drag requires blueprint and thrust JSON"); return 1; }
                    Console.WriteLine(AeroPopulationDrag.Report(args[1], args[2]));
                    return 0;

                case "aero-inputs":
                    if (args.Length < 2 || args.Length > 3 || (args.Length == 3 && args[2] != "--json"))
                    { Console.Error.WriteLine("aero-inputs requires one blueprint path and optional --json"); return 1; }
                    Console.WriteLine(args.Length == 3 ? AeroPopulationInputs.Json(args[1]) : AeroPopulationInputs.Report(args[1]));
                    return 0;

                case "aero-flight":
                    Console.WriteLine(AeroFlightLab.Report());
                    return 0;

                case "aero-profile":
                    Console.WriteLine(AeroProfileLab.Report());
                    return 0;

                case "aero-calibrate":
                    Console.WriteLine(AeroCalibrationLab.Report(AeroCalibrationLab.Fit(AeroCalibrationLab.Rigs(AeroCalibrationLab.CalibrationRadius))));
                    return 0;

                case "thermal-corpus-export":
                    if (args.Length != 2) { Console.Error.WriteLine("thermal-corpus-export requires output directory"); return 1; }
                    return ThermalCorpusExport.Run(args[1]);
                case "thermal-vision-volumes":
                    if (args.Length != 2) { Console.Error.WriteLine("thermal-vision-volumes requires an output directory outside the mod tree"); return 1; }
                    Console.WriteLine(ThermalVisionVolumeLab.WriteReport(args[1]));
                    return 2;

                case "thermal-vision-reuse":
                    if (args.Length != 2) { Console.Error.WriteLine("thermal-vision-reuse requires an output directory outside the mod tree"); return 1; }
                    Console.WriteLine(ThermalVisionReuseLab.WriteReport(args[1]));
                    return 0;

                case "thermal-vision-rays":
                    if (args.Length != 2) { Console.Error.WriteLine("thermal-vision-rays requires an output directory outside the mod tree"); return 1; }
                    Console.WriteLine(ThermalVisionRayLab.WriteReport(args[1]));
                    return 0;

                case "thermal-vision":
                    if (args.Length < 2) { Console.Error.WriteLine("thermal-vision requires an output directory outside the mod tree"); return 1; }
                    Console.WriteLine(ThermalVisionLab.WriteReport(args[1]));
                    if (args.Length > 2 && args[2] == "--require-complete")
                    { Console.Error.WriteLine("Full-scene readiness gate: INCOMPLETE (see report)."); return 2; }
                    return 0;

                case "occlusion":
                    Console.Write(OcclusionLadderLab.Report());
                    return 0;

                case "roomsweep":
                    Console.Write(RoomSweepLab.Report());
                    return 0;

                case "profiles":
                    Console.Write(ProfileLab.Report());
                    return 0;

                case "frequency":
                    Console.Write(FrequencyLab.Report());
                    return 0;

                case "reactors":
                    Console.Write(ReactorLab.Report());
                    return 0;

                case "triage":
                {
                    int take;
                    int.TryParse(ValueAfter(args, "--top") ?? "25", out take);

                    string census = ValueAfter(args, "--census")
                        ?? "out/census-2026-08-25/composition.csv";


                    string triageCsv = ValueAfter(args, "--csv");
                    if (triageCsv != null)
                    {
                        Directory.CreateDirectory(triageCsv);
                        string file = Path.Combine(triageCsv, "triage.csv");
                        File.WriteAllText(file, BlockTriageLab.Csv(census));
                        Console.WriteLine("wrote " + file);
                        return 0;
                    }


                    string levers = ValueAfter(args, "--levers");
                    if (levers != null)
                    {
                        float target;
                        if (!float.TryParse(levers, out target)) target = 3f;
                        Console.Write(BlockTriageLab.Levers(census, target, 0.001f));
                        return 0;
                    }

                    Console.Write(BlockTriageLab.Report(census, take));
                    return 0;
                }

                case "oxygen":
                    Console.Write(OxygenGeneratorLab.Report());
                    return 0;

                case "basevariants":
                {
                    int take;
                    int.TryParse(ValueAfter(args, "--ships") ?? "500", out take);


                    string one_file = ValueAfter(args, "--file");
                    if (one_file != null)
                    {
                        Blueprints.Ship probe = Blueprints.Probe(one_file);
                        Console.WriteLine("grids " + probe.Grids.Count + "  blocks " + probe.Blocks
                            + "  unknown " + probe.UnknownBlocks
                            + "  ambiguous " + probe.AmbiguousBlocks
                            + "  unresolved: " + string.Join(" ", probe.UnknownSubtypes.ToArray()));
                        return 0;
                    }

                    System.Collections.Generic.List<string> sample =
                        BaseVariantLab.Sample(ValueAfter(args, "--path"), take);


                    string ofType = ValueAfter(args, "--type");
                    Console.Write(ofType != null
                        ? BaseVariantLab.ShareReport(sample, ofType)
                        : BaseVariantLab.Report(sample));
                    return 0;
                }

                case "designed":
                {
                    float watts = float.Parse(Option(args, "--watts", "200000"),
                        System.Globalization.CultureInfo.InvariantCulture);

                    if (Has(args, "--sweep"))
                    {
                        float critical = float.Parse(Option(args, "--critical", "689"),
                            System.Globalization.CultureInfo.InvariantCulture);
                        Console.Write(DesignedHullLab.Sweep(watts, critical,
                            OptionInt(args, "--panels", 8)));
                        return 0;
                    }

                    Console.Write(DesignedHullLab.Report(watts, OptionInt(args, "--panels", 4)));
                    return 0;
                }

                case "coolers":
                    Console.Write(CoolingLadder.Report());
                    return 0;

                case "conductance":
                    Console.Write(ModHardwareRetest.Report());
                    return 0;

                case "blocks":
                    Console.Write(BlockCatalogLab.Report());
                    return 0;

                case "dump":

                    return DumpCommand(args);

                case "corpus":
                    Console.Write(Has(args, "--list")
                        ? CorpusLab.List(ValueAfter(args, "--path"))
                        : CorpusLab.Report(ValueAfter(args, "--path")));
                    return 0;

                case "corpus-fetch":
                    return CorpusFetch.Run(args);

                case "sealed":
                    Console.Write(HotSpotLab.SealedReport(ValueAfter(args, "--path")));
                    return 0;

                case "hotspot":
                {
                    int top;
                    int.TryParse(ValueAfter(args, "--top") ?? "0", out top);
                    Console.Write(HotSpotLab.Report(ValueAfter(args, "--path"),
                        ValueAfter(args, "--ship"), ValueAfter(args, "--scenario"), top));
                    return 0;
                }

                case "battery":
                {
                    int size;
                    int.TryParse(ValueAfter(args, "--panel") ?? "0", out size);
                    Console.Write(BatteryLab.Report(ValueAfter(args, "--path"), size, LabRun.ModeOf(args), Has(args, "--all")));
                    return 0;
                }

                case "retrofit":
                {
                    int retrofitShips;
                    int.TryParse(ValueAfter(args, "--ships") ?? "200", out retrofitShips);
                    Console.Write(RetrofitLab.Report(
                        ValueAfter(args, "--path"), retrofitShips, LabRun.ModeOf(args)));


                    string retrofitCsv = ValueAfter(args, "--csv");
                    if (retrofitCsv != null && RetrofitLab.LastRows != null)
                    {
                        Directory.CreateDirectory(retrofitCsv);
                        string file = Path.Combine(retrofitCsv, "retrofit.csv");
                        File.WriteAllText(file, RetrofitLab.Csv(RetrofitLab.LastRows));
                        Console.WriteLine("wrote " + file);
                    }
                    return 0;
                }

                case "stiffness":
                {
                    Console.Write(StiffnessLab.Report(ValueAfter(args, "--path"), LabRun.ModeOf(args)));


                    string stiffCsv = ValueAfter(args, "--csv");
                    if (stiffCsv != null && StiffnessLab.LastRows != null)
                    {
                        Directory.CreateDirectory(stiffCsv);
                        string file = Path.Combine(stiffCsv, "stiffness.csv");
                        File.WriteAllText(file, StiffnessLab.Csv(StiffnessLab.LastRows));
                        Console.WriteLine("wrote " + file);
                    }
                    return 0;
                }

                case "screen":
                {
                    int panel;
                    int.TryParse(ValueAfter(args, "--panel") ?? "0", out panel);
                    Console.Write(ScreeningLab.Report(ValueAfter(args, "--path"), panel, LabRun.ModeOf(args)));
                    return 0;
                }

                case "features":
                    Console.Write(FeatureMatrix.Report());
                    return 0;

                case "sweep":
                {
                    Console.Write(ProfileSweep.Report());

                    string directory = ValueAfter(args, "--csv");
                    if (directory != null)
                    {
                        Directory.CreateDirectory(directory);
                        string path = Path.Combine(directory, "profile-sweep.csv");
                        File.WriteAllText(path, ProfileSweep.Csv());
                        Console.WriteLine();
                        Console.WriteLine("wrote " + path);
                    }
                    return 0;
                }

                default:
                    Console.Error.WriteLine("Unknown command: " + args[0]);
                    PrintUsage();
                    return 1;
            }
        }


        private static int PrefabCommand(string[] args)
        {
            int limit = 0;

            string configured = ValueAfter(args, "--limit");
            if (configured != null) int.TryParse(configured, out limit);

            if (Blueprints.PrefabPath() == null)
            {
                Console.Error.WriteLine("No installed game found, so there are no prefabs to read.");
                return 1;
            }

            bool loaded = Array.IndexOf(args, "--load") >= 0;
            ShipLoad.State load = loaded ? ShipLoad.State.Everything : ShipLoad.State.Idle;

            Console.WriteLine(loaded
                ? "Under full load, which is the control rather than the criterion."
                : "Idle, which is what G7 asks.");
            Console.WriteLine();

            List<PrefabLab.Outcome> outcomes = PrefabLab.Run(null, limit, load);
            Console.Write(PrefabLab.Report(outcomes));


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "prefabs.csv");
                File.WriteAllText(path, PrefabLab.Csv(outcomes));
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
            }

            return outcomes.Count == 0 ? 1 : 0;
        }


        private static int DriftCommand(string[] args)
        {

            string scenario = ValueAfter(args, "--scenario") ?? "shadow";

            float watch = 600f;

            string configured = ValueAfter(args, "--watch");
            if (configured != null) float.TryParse(configured, out watch);

            int blocks = 2000;

            string sized = ValueAfter(args, "--size");
            if (sized != null) int.TryParse(sized, out blocks);

            float[] stale = { 60f, 300f, 1800f };

            Console.WriteLine("A client joining from a save, against the server that kept running.");
            Console.WriteLine("Hull: " + blocks.ToString("n0") + " census blocks, " + scenario
                + ", watched for " + watch.ToString("n0") + " simulated seconds.");
            Console.WriteLine();


            List<ClientDriftLab.Run> runs = new List<ClientDriftLab.Run>();
            for (int i = 0; i < stale.Length; i++)
            {
                runs.Add(ClientDriftLab.Measure(scenario, stale[i], watch, blocks));
            }

            Console.Write(ClientDriftLab.Report(runs));


            List<ClientDriftLab.Run> corrected = new List<ClientDriftLab.Run>();
            if (HasFlag(args, "--correct"))
            {
                float worst = stale[stale.Length - 1];
                float[] intervals = { 1f, 5f, 15f, 60f };
                int[] budgets = { 0, 1000 };

                corrected.Add(ClientDriftLab.Measure(scenario, worst, watch, blocks, null,
                    ClientDriftLab.Correction.None));

                for (int i = 0; i < intervals.Length; i++)
                {
                    for (int b = 0; b < budgets.Length; b++)
                    {
                        corrected.Add(ClientDriftLab.Measure(scenario, worst, watch, blocks, null,
                            new ClientDriftLab.Correction
                            {
                                IntervalSeconds = intervals[i],
                                MaxBlocks = budgets[b],
                            }));
                    }
                }

                for (int i = 0; i < intervals.Length; i++)
                {
                    corrected.Add(ClientDriftLab.Measure(scenario, worst, watch, blocks, null,
                        new ClientDriftLab.Correction
                        {
                            IntervalSeconds = intervals[i],
                            WholeHullOnJoin = true,
                        }));
                }

                for (int i = 0; i < intervals.Length; i++)
                {
                    corrected.Add(ClientDriftLab.Measure(scenario, worst, watch, blocks, null,
                        new ClientDriftLab.Correction
                        {
                            IntervalSeconds = intervals[i],
                            BandKelvin = 100000f,
                        }));
                }

                Console.WriteLine();
                Console.Write(ClientDriftLab.CorrectionReport(corrected));
            }


            List<ClientDriftLab.Run> hitching = new List<ClientDriftLab.Run>();
            if (HasFlag(args, "--hitch"))
            {
                float[] every = { 0f, 60f, 30f, 10f };
                float[] costs = { 1f, 5f };

                for (int i = 0; i < every.Length; i++)
                {
                    for (int c = 0; c < costs.Length; c++)
                    {
                        if (every[i] <= 0f && c > 0) continue;

                        ClientDriftLab.Machine host = new ClientDriftLab.Machine
                        {
                            HitchEverySeconds = every[i],
                            HitchLosesSeconds = costs[c],
                        };

                        hitching.Add(ClientDriftLab.Measure(scenario, 0f, watch, blocks, null,
                            ClientDriftLab.Correction.None, host));

                        hitching.Add(ClientDriftLab.Measure(scenario, 0f, watch, blocks, null,
                            new ClientDriftLab.Correction { IntervalSeconds = 5f, MaxBlocks = 250 },
                            host));
                    }
                }

                Console.WriteLine();
                Console.Write(ClientDriftLab.HitchReport(hitching));
            }


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("scenario,blocks,stale_s,seconds,max_k,mean_k,disagree_on_critical,hot_blocks");

                foreach (ClientDriftLab.Run run in runs)
                {
                    foreach (ClientDriftLab.Sample sample in run.Samples)
                    {
                        csv.Append(run.Scenario).Append(',').Append(run.Blocks).Append(',')
                           .Append(run.StaleSeconds.ToString("0.###")).Append(',')
                           .Append(sample.Seconds.ToString("0.###")).Append(',')
                           .Append(sample.MaxKelvin.ToString("0.####")).Append(',')
                           .Append(sample.MeanKelvin.ToString("0.####")).Append(',')
                           .Append(sample.DisagreeOnCritical).Append(',')
                           .Append(sample.HotBlocks).AppendLine();
                    }
                }

                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "drift.csv");
                File.WriteAllText(path, csv.ToString());
                Console.WriteLine();
                Console.WriteLine("wrote " + path);

                if (corrected.Count > 0)
                {

                    StringBuilder sweep = new StringBuilder();
                    sweep.AppendLine("scenario,blocks,stale_s,interval_s,band_k,max_blocks,"
                        + "misreading_s,showing_safe_s,crying_wolf_s,updates,bytes,bytes_per_s,peak_blocks,dropped");

                    foreach (ClientDriftLab.Run run in corrected)
                    {
                        sweep.Append(run.Scenario).Append(',').Append(run.Blocks).Append(',')
                             .Append(run.StaleSeconds.ToString("0.###")).Append(',')
                             .Append(run.Protocol.IntervalSeconds.ToString("0.###")).Append(',')
                             .Append(run.Protocol.BandKelvin.ToString("0.###")).Append(',')
                             .Append(run.Protocol.MaxBlocks).Append(',')
                             .Append(run.SecondsMisreadingCritical.ToString("0.###")).Append(',')
                             .Append(run.SecondsShowingSafe.ToString("0.###")).Append(',')
                             .Append(run.SecondsCryingWolf.ToString("0.###")).Append(',')
                             .Append(run.Updates).Append(',').Append(run.Bytes).Append(',')
                             .Append(run.BytesPerSecond.ToString("0.##")).Append(',')
                             .Append(run.PeakBlocksSent).Append(',')
                             .Append(run.BlocksDropped).AppendLine();
                    }

                    string sweepPath = Path.Combine(directory, "drift-correction.csv");
                    File.WriteAllText(sweepPath, sweep.ToString());
                    Console.WriteLine("wrote " + sweepPath);
                }
            }

            return 0;
        }


        private static int InputsCommand(string[] args)
        {

            string scenario = ValueAfter(args, "--scenario") ?? "planet";

            float watch = 600f;

            string configured = ValueAfter(args, "--watch");
            if (configured != null) float.TryParse(configured, out watch);

            int blocks = 2000;

            string sized = ValueAfter(args, "--size");
            if (sized != null) int.TryParse(sized, out blocks);

            ClientDriftLab.Correction fix = new ClientDriftLab.Correction
            {
                IntervalSeconds = 5f,
                WholeHullOnJoin = true,
            };

            Console.WriteLine("A client whose view of the world is worse than the server's.");
            Console.WriteLine("Hull: " + blocks.ToString("n0") + " census blocks, " + scenario
                + ", " + watch.ToString("n0") + " simulated seconds, load alternating every "
                + ClientInputLab.LoadPeriodSeconds.ToString("n0") + " s.");
            Console.WriteLine();

            List<ClientInputLab.Degradation> cases = ClientInputLab.All();

            List<ClientInputLab.Result> results = new List<ClientInputLab.Result>();

            foreach (ClientInputLab.Degradation one in cases)
            {
                results.Add(ClientInputLab.Measure(one, ClientDriftLab.Correction.None,
                    scenario, watch, blocks));
                results.Add(ClientInputLab.Measure(one, fix, scenario, watch, blocks));
            }

            Console.Write(ClientInputLab.Report(results));
            Console.WriteLine();
            Console.WriteLine("What each case degrades, and why that is what the engine does:");
            Console.WriteLine();

            foreach (ClientInputLab.Degradation one in cases)
            {
                Console.WriteLine("  " + one.Name.PadRight(20) + one.Because);
            }


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("degradation,scenario,blocks,correction_s,whole_hull_on_join,"
                    + "peak_k,standing_k,misreading_s,peak_disagreeing,peak_server_critical,"
                    + "peak_blocks_sent,bytes_per_s");

                foreach (ClientInputLab.Result result in results)
                {
                    bool off = result.Protocol == null || result.Protocol.IntervalSeconds <= 0f;
                    csv.Append('"').Append(result.Name).Append('"').Append(',')
                       .Append(result.Scenario).Append(',').Append(result.Blocks).Append(',')
                       .Append(off ? "0" : result.Protocol.IntervalSeconds.ToString("0.###")).Append(',')
                       .Append(!off && result.Protocol.WholeHullOnJoin ? "1" : "0").Append(',')
                       .Append(result.PeakKelvin.ToString("0.####")).Append(',')
                       .Append(result.StandingKelvin.ToString("0.####")).Append(',')
                       .Append(result.SecondsMisreading.ToString("0.###")).Append(',')
                       .Append(result.PeakDisagreeing).Append(',')
                       .Append(result.PeakServerCritical).Append(',')
                       .Append(result.PeakBlocksSent).Append(',')
                       .Append(result.BytesPerSecond.ToString("0.##")).AppendLine();
                }

                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "client-inputs.csv");
                File.WriteAllText(path, csv.ToString());
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
            }

            return 0;
        }


        private static int DescentCommand(string[] args)
        {
            List<Descent.Reading> readings = Descent.Run();
            string csv = Descent.Csv(readings);

            Console.WriteLine("A ship of " + Descent.HullReach.ToString("n1")
                + " m reach digging from the surface to the core.");
            Console.WriteLine();
            Console.Write(csv.Replace(",", "\t"));


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "descent.csv");
                File.WriteAllText(path, csv);
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
            }

            return 0;
        }


        private static int BalanceCommand(string[] args)
        {
            Console.Write(BalanceLab.Report());


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "balance-blocks.csv");
                File.WriteAllText(path, BalanceLab.BlocksCsv());
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
            }
            return 0;
        }


        private static int WindCommand(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "scenarios" || args[i] == "--scenarios")
                {
                    Console.Write(WindScenarios.Report());
                    return 0;
                }
            }

            WindLab.Planet planet = new WindLab.Planet();
            WindLab.Options options = new WindLab.Options();


            string world = ValueAfter(args, "--planet");
            if (world != null) planet = WindLab.Planet.Vanilla(world);


            string diameter = ValueAfter(args, "--diameter");
            if (diameter != null)
            {
                planet.AverageRadius = double.Parse(
                    diameter, System.Globalization.CultureInfo.InvariantCulture) * 0.5d;
            }

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--flat") planet.Ground = new WindLab.FlatTerrain(planet);
            }


            string weather = ValueAfter(args, "--weather");
            if (weather != null)
            {
                options.WeatherIntensity = float.Parse(
                    weather, System.Globalization.CultureInfo.InvariantCulture);
            }

            Console.Write(WindLab.Report(planet, options));


            string animation = ValueAfter(args, "--animation");
            if (animation != null)
            {
                File.WriteAllText(animation, WindAnimation.Json());
                Console.WriteLine("wrote " + animation);
                return 0;
            }


            string directory = ValueAfter(args, "--csv");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "wind-day.csv");
                File.WriteAllText(path, WindLab.Csv(WindLab.Run(planet, options)));
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
            }

            return 0;
        }


        private static int PlanetsCommand(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--xml")
                {
                    Console.Write(PlanetLab.Xml());
                    return 0;
                }
            }


            string path = ValueAfter(args, "--write");
            if (path != null)
            {
                File.WriteAllText(path, PlanetLab.Xml());
                Console.Write(PlanetLab.Report());
                Console.WriteLine();
                Console.WriteLine("wrote " + path);
                return 0;
            }

            Console.Write(PlanetLab.Report());
            return 0;
        }


        private static bool HasFlag(string[] args, string flag)
        {
            return Cli.Has(args, flag);
        }


        private static string ValueAfter(string[] args, string flag)
        {
            return Cli.Value(args, flag);
        }


        private static int RunCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("run needs a scenario name, or 'all'.");
                return 1;
            }

            string csvDirectory = null;
            for (int i = 2; i < args.Length - 1; i++)
            {
                if (args[i] == "--csv") csvDirectory = args[i + 1];
            }

            string[] names = args[1] == "all" ? Scenarios.Names : new string[] { args[1] };

            foreach (string name in names)
            {
                ScenarioResult result;
                try
                {
                    result = Scenarios.Run(name);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(name + ": FAILED - " + error.Message);
                    return 1;
                }

                Console.WriteLine();
                Console.WriteLine("== " + result.Name + " ==");
                Console.WriteLine(result.Summary);

                if (result.Runner != null && result.Runner.Samples.Count > 0)
                {
                    PrintTable(result);
                }

                if (csvDirectory != null && !string.IsNullOrEmpty(result.Csv))
                {
                    Directory.CreateDirectory(csvDirectory);
                    string path = Path.Combine(csvDirectory, result.Name + ".csv");
                    File.WriteAllText(path, result.Csv);
                    Console.WriteLine("csv -> " + path);
                }
            }

            return 0;
        }


        private static int BenchCommand(string[] args)
        {
            string name = args.Length > 1 ? args[1] : "scale";

            string shape = Option(args, "--shape", "ship");

            int size = OptionInt(args, "--size", 125000);

            int max = OptionInt(args, "--max", int.MaxValue);

            int ticks = OptionInt(args, "--ticks", 0);

            string csvDirectory = Option(args, "--csv", null);


            LoadBenchmarks.CollectDiagnostics = Has(args, "--diagnostics");

            switch (name)
            {
                case "scale":
                {

                    List<int> sizes = new List<int>();
                    foreach (int rung in LoadBenchmarks.DefaultSizes)
                    {
                        if (rung <= max) sizes.Add(rung);
                    }
                    if (sizes.Count == 0) sizes.Add(max);

                    Console.WriteLine("Scale ladder, " + shape + " shape. Building "
                        + sizes[sizes.Count - 1].ToString("n0") + " blocks takes a while.");
                    Console.WriteLine();

                    List<ScaleRow> rows = LoadBenchmarks.Scale(
                        shape, sizes, message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(LoadBenchmarks.Table(rows));

                    if (csvDirectory != null)
                    {
                        Directory.CreateDirectory(csvDirectory);
                        string path = Path.Combine(csvDirectory, "scale-" + shape + ".csv");
                        File.WriteAllText(path, LoadBenchmarks.Csv(rows));
                        Console.WriteLine("csv -> " + path);
                    }
                    return 0;
                }

                case "coolant":
                {
                    int[] ringCounts = new int[] { 1, 4, 16, 48 };
                    int hull = size > 0 ? size : 8000;
                    int runSteps = ticks > 0 ? ticks : 200;
                    float flow = float.Parse(Option(args, "--flow", "0"), System.Globalization.CultureInfo.InvariantCulture);

                    Console.WriteLine("Coolant model comparison on a " + hull.ToString("n0")
                        + " block ship, " + runSteps + " steps per reading.");
                    Console.WriteLine();


                    List<CoolantBenchmarks.Row> rows = new List<CoolantBenchmarks.Row>();
                    for (int i = 0; i < ringCounts.Length; i++)
                    {
                        Console.Error.WriteLine("  " + ringCounts[i] + " rings...");
                        rows.Add(CoolantBenchmarks.Measure(hull, ringCounts[i], runSteps, flow));
                    }

                    Console.WriteLine(CoolantBenchmarks.Table(rows));

                    if (csvDirectory != null)
                    {
                        Directory.CreateDirectory(csvDirectory);
                        string path = Path.Combine(csvDirectory, "coolant-models.csv");
                        File.WriteAllText(path, CoolantBenchmarks.Csv(rows));
                        Console.WriteLine("csv -> " + path);
                    }
                    return 0;
                }

                case "hitch":
                    PrintHitch(LoadBenchmarks.Hitch(shape, size, ticks > 0 ? ticks : 400));
                    return 0;

                case "weld":
                    PrintHitch(LoadBenchmarks.Weld(shape, size, ticks > 0 ? ticks : 120));
                    return 0;

                case "load":
                    PrintHitch(LoadBenchmarks.Load(shape, size));
                    return 0;

                case "reach":
                {

                    float seconds = OptionInt(args, "--seconds", 10);

                    int length = OptionInt(args, "--length", 200);


                    List<LoadBenchmarks.ReachRow> rows = new List<LoadBenchmarks.ReachRow>();

                    rows.Add(LoadBenchmarks.Reach("sim f8 h225", 8, 1f, 225f, 64, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("def f4 h225", 4, 1f, 225f, 16, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f4 h3600", 4, 1f, 3600f, 16, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f4 h20k", 4, 1f, 20000f, 16, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f4 h100k", 4, 1f, 100000f, 16, seconds, length));

                    rows.Add(LoadBenchmarks.Reach("f4 h20k x1", 4, 1f, 20000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f4 h100k x1", 4, 1f, 100000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f4 h1M x1", 4, 1f, 1000000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f8 h1M x1", 8, 1f, 1000000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f16 h1M x1", 16, 1f, 1000000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f16 h10M x1", 16, 1f, 10000000f, 1, seconds, length));
                    rows.Add(LoadBenchmarks.Reach("f1 h1M x1", 1, 1f, 1000000f, 1, seconds, length));

                    Console.WriteLine();
                    Console.WriteLine("== reach, " + length + " blocks, " + seconds + " real seconds ==");
                    Console.WriteLine();
                    Console.WriteLine(LoadBenchmarks.ReachTable(rows));
                    return 0;
                }

                case "elements":
                {

                    int nodes = OptionInt(args, "--nodes", 8000);

                    float seconds = OptionInt(args, "--seconds", 20);


                    string only = Option(args, "--shapes", null);
                    ElementCostLab.OnlyShapes = only == null ? null : only.Split(',');

                    Console.WriteLine();
                    Console.WriteLine(ElementCostLab.Report(nodes, seconds));

                    if (csvDirectory != null)
                    {
                        Directory.CreateDirectory(csvDirectory);
                        string path = Path.Combine(csvDirectory, "element-cost.csv");
                        File.WriteAllText(path, ElementCostLab.Csv(nodes, seconds));
                        Console.WriteLine("csv -> " + path);
                    }
                    return 0;
                }

                case "memory":
                {
                    Console.WriteLine();
                    Console.WriteLine("== memory, " + shape + " " + size.ToString("n0") + " ==");
                    Console.WriteLine();
                    Console.WriteLine(LoadBenchmarks.MemoryTable(LoadBenchmarks.Memory(shape, size)));
                    return 0;
                }

                case "stability":
                {

                    float seconds = OptionInt(args, "--seconds", 20);

                    List<LoadBenchmarks.StabilityRow> rows = new List<LoadBenchmarks.StabilityRow>();

                    rows.Add(LoadBenchmarks.Stability("default", 4, 225f, 16, seconds));
                    rows.Add(LoadBenchmarks.Stability("h3600 x16", 4, 3600f, 16, seconds));
                    rows.Add(LoadBenchmarks.Stability("h3600 x1", 4, 3600f, 1, seconds));
                    rows.Add(LoadBenchmarks.Stability("h20k x1", 4, 20000f, 1, seconds));
                    rows.Add(LoadBenchmarks.Stability("h100k x1", 4, 100000f, 1, seconds));
                    rows.Add(LoadBenchmarks.Stability("h1M x1", 4, 1000000f, 1, seconds));
                    rows.Add(LoadBenchmarks.Stability("h20k x1 f16", 16, 20000f, 1, seconds));
                    rows.Add(LoadBenchmarks.Stability("h20k x4", 4, 20000f, 4, seconds));

                    Console.WriteLine();
                    Console.WriteLine("== stability, environment on, " + seconds + " real seconds ==");
                    Console.WriteLine();
                    Console.WriteLine(LoadBenchmarks.StabilityTable(rows));
                    return 0;
                }

                case "pace":
                {

                    float seconds = OptionInt(args, "--seconds", 20);
                    Console.WriteLine();
                    Console.WriteLine("== pace " + shape + " " + size.ToString("n0") + " ==");
                    Console.WriteLine("  Constant SimulationSpeed x HeatTimeScale, then Frequency alone.");
                    Console.WriteLine();
                    Console.WriteLine(LoadBenchmarks.PaceTable(
                        LoadBenchmarks.Pace(shape, size, seconds), seconds));
                    return 0;
                }

                case "report":
                {

                    List<int> ladder = new List<int>();
                    foreach (int rung in LoadBenchmarks.DefaultSizes)
                    {
                        if (rung <= max) ladder.Add(rung);
                    }
                    if (ladder.Count == 0) ladder.Add(max);

                    int reportTicks = ticks > 0 ? ticks : 20;

                    PerformanceReport.Repeats = Math.Max(1,
                        OptionInt(args, "--repeats", PerformanceReport.Repeats));


                    string baseline = Option(args, "--baseline", null);
                    string outDir = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== performance report ==");
                    Console.WriteLine("  " + shape + ", feature and configuration cases on "
                        + size.ToString("n0") + " blocks, " + reportTicks + " steps each.");
                    Console.WriteLine("  Ladder: " + string.Join(", ", ladder.ConvertAll(
                        delegate (int n) { return n.ToString("n0"); }).ToArray()));
                    Console.WriteLine();

                    List<ReportRow> rows = PerformanceReport.Run(shape, size, reportTicks, ladder,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(PerformanceReport.Table(rows));

                    Directory.CreateDirectory(outDir);
                    string path = Path.Combine(outDir, "performance.csv");
                    File.WriteAllText(path, PerformanceReport.Csv(rows));
                    Console.WriteLine("csv -> " + path);

                    if (baseline != null && File.Exists(baseline))
                    {
                        Console.WriteLine();
                        Console.WriteLine("== against " + baseline + " ==");
                        Console.WriteLine(PerformanceReport.Compare(
                            PerformanceReport.ParseCsv(File.ReadAllText(baseline)), rows));
                    }

                    else if (baseline != null)
                    {
                        Console.Error.WriteLine("  baseline not found: " + baseline);
                    }

                    return 0;
                }

                case "stepfloor":
                {
                    int floorBlocks = size > 0 ? size : 125000;

                    Console.WriteLine();
                    Console.WriteLine("== step floor, " + shape + " " + floorBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  What each pass of a substep would cost doing no arithmetic:");
                    Console.WriteLine("  the same rows, the same sizes, the grid's own link indices.");
                    Console.WriteLine();

                    List<StageLab.Row> measured = StageLab.StepPhases(shape, floorBlocks,
                        message => Console.Error.WriteLine("  " + message));
                    List<StepFloorLab.Row> floorRows = StepFloorLab.Run(shape, floorBlocks);

                    Console.WriteLine(StepFloorLab.Table(floorRows, measured));
                    return 0;
                }

                case "stepphases":
                {
                    int phaseBlocks = size > 0 ? size : 125000;

                    Console.WriteLine();
                    Console.WriteLine("== step phases, " + shape + " " + phaseBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  Each stage inside one settled step on its own clock, best of "
                        + StageLab.Repeats + ".");
                    Console.WriteLine("  The visits column is what the solver charges that stage;"
                        + " it must repeat exactly or the lab says so.");
                    Console.WriteLine();

                    List<StageLab.Row> phaseRows = StageLab.StepPhases(shape, phaseBlocks,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(StageLab.Table(phaseRows));

                    string phaseOut = csvDirectory ?? "out";
                    Directory.CreateDirectory(phaseOut);
                    string phasePath = Path.Combine(phaseOut, "stepphases.csv");
                    File.WriteAllText(phasePath, StageLab.Csv(phaseRows));
                    Console.WriteLine("csv -> " + phasePath);
                    return 0;
                }

                case "stages":
                {
                    int stageBlocks = size > 0 ? size : 125000;


                    int stageRepeats = OptionInt(args, "--repeats", StageLab.Repeats);
                    StageLab.Repeats = Math.Max(1, stageRepeats);

                    if (Array.IndexOf(args, "--trace") >= 0)
                    {
                        StageLab.TraceRepeat = line => Console.WriteLine("  trace " + line);
                    }
                    string stageOut = csvDirectory ?? "out";

                    string stageList = Option(args, "--stages", null);

                    List<string> stages = new List<string>(stageList == null
                        ? StageLab.Stages
                        : stageList.Split(','));

                    Console.WriteLine();
                    Console.WriteLine("== stages, " + shape + " " + stageBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  Each stage of a grid's life on its own clock, on one prebuilt grid,"
                        + " fastest of at least " + StageLab.Repeats + " repeats and then until "
                        + StageLab.ConfirmingRepeats + " of them land within "
                        + ((StageLab.ConfirmingBand - 1d) * 100d).ToString("n0") + "% of it.");
                    Console.WriteLine("  The stopped column says which ended each row: a capped row's"
                        + " best was never reproduced, and is not comparable with anything.");
                    Console.WriteLine("  The work column must repeat exactly, or the readings are of"
                        + " different walks and the lab says so.");
                    Console.WriteLine();

                    bool isolate = Array.IndexOf(args, "--isolate") >= 0;
                    if (isolate)
                    {
                        Console.WriteLine("  --isolate: one stage per process, so no stage carries"
                            + " what the stage before it did to the heap.");
                        Console.WriteLine();
                    }

                    List<StageLab.Row> stageRows = isolate

                        ? StageInSeparateProcesses(args, stages, stageOut)
                        : StageLab.Run(shape, stageBlocks, stages,
                            message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(StageLab.Table(stageRows));

                    Directory.CreateDirectory(stageOut);
                    string stagePath = Path.Combine(stageOut, "stages.csv");
                    File.WriteAllText(stagePath, StageLab.Csv(stageRows));
                    Console.WriteLine("csv -> " + stagePath);

                    string samplePath = Path.Combine(stageOut, "samples.csv");
                    File.WriteAllText(samplePath, StageLab.SamplesCsv(stageRows));
                    Console.WriteLine("csv -> " + samplePath);
                    return 0;
                }

                case "samplestats":
                {

                    string from = Option(args, "--from", null);
                    if (from == null)
                    {
                        Console.Error.WriteLine("bench samplestats needs --from dir1,dir2,... —"
                            + " two or more directories `bench stages` wrote a samples.csv into.");
                        return 2;
                    }

                    string[] dirs = from.Split(',');
                    if (dirs.Length < 2)
                    {
                        Console.Error.WriteLine("bench samplestats compares runs, so it needs at"
                            + " least two; it was given " + dirs.Length + ".");
                        return 2;
                    }


                    List<SampleStatisticLab.Series> series = new List<SampleStatisticLab.Series>();
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        string path = Path.Combine(dirs[i].Trim(), "samples.csv");
                        if (!File.Exists(path))
                        {
                            Console.Error.WriteLine("no samples.csv in " + dirs[i].Trim()
                                + " — a run that is not there is a run this comparison would be"
                                + " silently short of.");
                            return 2;
                        }
                        series.AddRange(SampleStatisticLab.Read(path, "run" + (i + 1)));
                    }

                    List<string> droppedStages;
                    List<SampleStatisticLab.Row> statRows =
                        SampleStatisticLab.Compare(series, out droppedStages);

                    Console.WriteLine();
                    Console.WriteLine("== which statistic reproduces, " + dirs.Length + " runs ==");
                    Console.WriteLine("  The spread between runs of one binary, per stage, per"
                        + " candidate summary of its repeats.");
                    Console.WriteLine("  The one to keep is the one two runs of the same code agree"
                        + " on, which is the only property a comparison uses.");
                    Console.WriteLine();

                    SampleStatisticLab.WindowSpan window = SampleStatisticLab.Window(series);
                    if (window.Unstamped > 0)
                    {
                        Console.WriteLine("  " + window.Unstamped + " of these runs"
                            + (window.Unstamped == 1 ? " carries" : " carry")
                            + " no taken_utc, so nothing says whether they are one window; the"
                            + " spreads below may be between sessions rather than between"
                            + " processes.");
                    }

                    else if (window.IsOneWindow)
                    {
                        Console.WriteLine("  one window: " + window.Earliest + " to "
                            + window.Latest + ", "
                            + window.Elapsed.TotalMinutes.ToString("n0") + " minutes apart.");
                    }
                    else
                    {
                        Console.WriteLine("  ACROSS SESSIONS: " + window.Earliest + " to "
                            + window.Latest + ", "
                            + window.Elapsed.TotalHours.ToString("n1") + " hours apart — the"
                            + " spreads below include whatever moved between them, which for the"
                            + " exposure stage has been a factor of two on identical code.");
                    }

                    Console.WriteLine();
                    Console.WriteLine(SampleStatisticLab.Table(statRows));

                    for (int i = 0; i < droppedStages.Count; i++)
                    {
                        Console.WriteLine("  dropped: " + droppedStages[i]
                            + ", so it is not compared here");
                    }

                    string statOut = csvDirectory ?? "out";
                    Directory.CreateDirectory(statOut);
                    string statPath = Path.Combine(statOut, "samplestats.csv");
                    File.WriteAllText(statPath, SampleStatisticLab.Csv(statRows));
                    Console.WriteLine("csv -> " + statPath);
                    return 0;
                }

                case "se2tax":
                {

                    int taxBlocks = OptionInt(args, "--size", 8904);

                    int[] factors = ParseInts(Option(args, "--factors", "1,2,5,10"));

                    StageLab.Repeats = Math.Max(1, OptionInt(args, "--repeats", 10));

                    Console.WriteLine();
                    Console.WriteLine("== se2 lattice tax, " + shape + " "
                        + taxBlocks.ToString("n0") + " blocks, factors "
                        + string.Join(",", Array.ConvertAll(factors,
                            delegate (int v) { return v.ToString(); })) + " ==");
                    Console.WriteLine("  The same blocks at every rung; only the lattice under"
                        + " them divides. Factor 10 is SE2's 25 cm under SE1's 2.5 m.");
                    Console.WriteLine();


                    List<Se2LatticeLab.Summary> taxSummaries = new List<Se2LatticeLab.Summary>();

                    List<Se2LatticeLab.StageRow> taxStages = new List<Se2LatticeLab.StageRow>();
                    Se2LatticeLab.Run(shape, taxBlocks, factors, taxSummaries, taxStages,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(Se2LatticeLab.SummaryTable(taxSummaries));
                    Console.WriteLine();
                    Console.WriteLine(Se2LatticeLab.StageTable(taxStages));

                    string taxOut = csvDirectory ?? "out";
                    Directory.CreateDirectory(taxOut);
                    string taxSummaryPath = Path.Combine(taxOut, "se2tax-summary.csv");
                    File.WriteAllText(taxSummaryPath, Se2LatticeLab.SummaryCsv(taxSummaries));
                    Console.WriteLine("csv -> " + taxSummaryPath);
                    string taxStagePath = Path.Combine(taxOut, "se2tax-stages.csv");
                    File.WriteAllText(taxStagePath, Se2LatticeLab.StageCsv(taxStages));
                    Console.WriteLine("csv -> " + taxStagePath);
                    return 0;
                }

                case "coarserooms":
                {

                    int roomBlocks = OptionInt(args, "--size", 126731);

                    int refine = OptionInt(args, "--refine", 1);

                    int[] edges = ParseInts(Option(args, "--edges", refine > 1
                        ? refine.ToString() + "," + (refine * 2)
                        : "2,4,8"));

                    StageLab.Repeats = Math.Max(1, OptionInt(args, "--repeats", 20));

                    Console.WriteLine();
                    Console.WriteLine("== coarse rooms, " + shape + " "
                        + roomBlocks.ToString("n0") + " blocks"
                        + (refine > 1 ? ", lattice refined x" + refine : "") + " ==");
                    Console.WriteLine("  Every prototype row is verified cell for cell against"
                        + " the shipped mapper before its clock is trusted.");
                    Console.WriteLine();

                    GridBuilder roomBuilder = GridBuilder.Large();
                    roomBuilder.PlaceCensus(LoadShapes.Build(shape, roomBlocks));
                    if (refine > 1) roomBuilder = Se2Refine.Refined(roomBuilder, refine);

                    List<CoarseRoomLab.Row> roomRows = CoarseRoomLab.Run(roomBuilder, edges,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(CoarseRoomLab.Table(roomRows));

                    string roomOut = csvDirectory ?? "out";
                    Directory.CreateDirectory(roomOut);
                    string roomPath = Path.Combine(roomOut, "coarserooms.csv");
                    File.WriteAllText(roomPath, CoarseRoomLab.Csv(roomRows));
                    Console.WriteLine("csv -> " + roomPath);

                    for (int i = 0; i < roomRows.Count; i++)
                    {
                        if (roomRows[i].Mismatch != null) return 1;
                    }
                    return 0;
                }

                case "condlayout":
                {

                    int condBlocks = OptionInt(args, "--size", 126731);

                    int[] condThreads = ParseInts(Option(args, "--threads",
                        string.Join(",", Array.ConvertAll(ConductionLayoutLab.DefaultThreads,
                            delegate (int v) { return v.ToString(); }))));

                    StageLab.Repeats = Math.Max(1, OptionInt(args, "--repeats", StageLab.Repeats));

                    Console.WriteLine();
                    Console.WriteLine("== conduction layout, " + shape + " "
                        + condBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  Plain flux arithmetic on the solver's own graph — the"
                        + " kernels compare with each other, not with bench stepphases.");
                    Console.WriteLine();

                    List<ConductionLayoutLab.Row> condRows = ConductionLayoutLab.Run(
                        shape, condBlocks, condThreads,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(ConductionLayoutLab.Table(condRows));

                    string condOut = csvDirectory ?? "out";
                    Directory.CreateDirectory(condOut);
                    string condPath = Path.Combine(condOut, "condlayout.csv");
                    File.WriteAllText(condPath, ConductionLayoutLab.Csv(condRows));
                    Console.WriteLine("csv -> " + condPath);
                    return 0;
                }

                case "smallgrids":
                {

                    int fleetGrids = OptionInt(args, "--grids", 200);
                    int fleetSteps = ticks > 0 ? ticks : 40;
                    string smallOut = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== small grids, " + fleetGrids.ToString("n0") + " of each ==");
                    Console.WriteLine("  A fleet swept from one block a grid upwards, on a planet"
                        + " surface, through the host's entry point.");
                    Console.WriteLine("  What one grid costs before any of its blocks do.");
                    Console.WriteLine();

                    List<SmallGridLab.Row> smallRows = SmallGridLab.Run(fleetGrids,
                        SmallGridLab.DefaultSizes, fleetSteps,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(SmallGridLab.Table(smallRows));

                    Directory.CreateDirectory(smallOut);
                    string smallPath = Path.Combine(smallOut, "smallgrids.csv");
                    File.WriteAllText(smallPath, SmallGridLab.Csv(smallRows));
                    Console.WriteLine("csv -> " + smallPath);
                    return 0;
                }

                case "franken":
                {
                    int frankenTarget = size > 0 ? size : 1000000;

                    string ships = Option(args, "--ships", "out/corpus-2026-08-21/ships.csv");

                    List<KeyValuePair<int, string>> paths = FrankenHull.LargestFirst(ships);
                    if (paths.Count == 0)
                    {
                        Console.Error.WriteLine("no blueprints listed in " + ships
                            + " — point --ships at a survey's ships.csv");
                        return 1;
                    }

                    Console.WriteLine();
                    Console.WriteLine("== franken hull, " + frankenTarget.ToString("n0")
                        + " blocks from " + paths.Count.ToString("n0")
                        + " blueprints, largest first ==");
                    Console.WriteLine("  Every figure here is about the stress bound. This is the"
                        + " biggest ships in the corpus,");
                    Console.WriteLine("  tiled — which is what a million-block grid would have to"
                        + " be made of, and nobody's median.");
                    Console.WriteLine();

                    FrankenHull.Manifest manifest = new FrankenHull.Manifest();
                    System.Diagnostics.Stopwatch frankenBuild =
                        System.Diagnostics.Stopwatch.StartNew();
                    GridBuilder welded = FrankenHull.Build(paths, frankenTarget, manifest,
                        message => Console.Error.WriteLine("  " + message));
                    frankenBuild.Stop();

                    Console.WriteLine("  " + manifest.Describe());
                    Console.WriteLine("  read and tiled in "
                        + frankenBuild.Elapsed.TotalSeconds.ToString("n1") + " s");
                    Console.WriteLine();

                    for (int i = 0; i < manifest.Ships.Count; i++)
                    {
                        Console.WriteLine("    " + manifest.Copies[i].ToString().PadLeft(5) + "x  "
                            + manifest.Ships[i]);
                    }

                    Console.WriteLine();
                    Console.WriteLine(LoadBenchmarks.Table(
                        new List<ScaleRow> { LoadBenchmarks.MeasureBuilt(welded) }));
                    return 0;
                }

                case "allowance":
                {

                    List<int> allowanceSizes = new List<int>();
                    foreach (int rung in AllowanceLab.DefaultSizes)
                    {
                        if (rung <= max) allowanceSizes.Add(rung);
                    }

                    int allowanceFrames = ticks > 0 ? ticks : AllowanceLab.DefaultFrames;
                    string allowanceOut = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== element-visit allowance, " + shape + ", "
                        + allowanceFrames + " frames ==");
                    Console.WriteLine("  A step is spread across the frames of its own window, so"
                        + " an allowance V at Frequency f");
                    Console.WriteLine("  bounds a frame at V * f / 60 element visits. The trade is"
                        + " frame milliseconds against");
                    Console.WriteLine("  the share of simulated time a grid keeps; the kelvin"
                        + " column converts the second at F23's rate.");
                    Console.WriteLine();

                    List<AllowanceLab.PriceRow> allowancePrices = AllowanceLab.PriceRates(
                        AllowanceLab.DefaultDeficits,
                        message => Console.Error.WriteLine("  " + message));

                    List<AllowanceLab.Row> allowanceRows = AllowanceLab.Run(shape, allowanceSizes,
                        AllowanceLab.DefaultAllowances, AllowanceLab.DefaultWorlds,
                        allowanceFrames, message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine("-- what a lost rate is worth, under a moving load --");
                    Console.WriteLine();
                    Console.WriteLine(AllowanceLab.PriceTable(allowancePrices));
                    Console.WriteLine("-- what each allowance costs and buys --");
                    Console.WriteLine();
                    Console.WriteLine(AllowanceLab.Table(allowanceRows, allowancePrices));

                    Directory.CreateDirectory(allowanceOut);
                    string allowancePath = Path.Combine(allowanceOut, "allowance.csv");
                    File.WriteAllText(allowancePath,
                        AllowanceLab.Csv(allowanceRows, allowancePrices));
                    string allowancePricePath = Path.Combine(allowanceOut, "allowance-rate.csv");
                    File.WriteAllText(allowancePricePath, AllowanceLab.PriceCsv(allowancePrices));
                    Console.WriteLine("csv -> " + allowancePath);
                    Console.WriteLine("csv -> " + allowancePricePath);
                    return 0;
                }

                case "rowfill":
                {
                    int fillBlocks = size > 0 ? size : 32000;
                    int fillSteps = ticks > 0 ? ticks : 30;
                    string fillOut = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== row fill, " + fillBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  The per-node environment terms a step fills once and every"
                        + " later substep reads.");
                    Console.WriteLine("  Measured by turning the cache off, so a step of N substeps"
                        + " pays N fills instead of one:");
                    Console.WriteLine("  the difference is N-1 fills, which multiplies the signal"
                        + " by the substep count rather than dividing it.");
                    Console.WriteLine();

                    List<RowFillLab.Row> fillRows = RowFillLab.Run(RowFillLab.DefaultWorlds,
                        RowFillLab.DefaultCaps, fillBlocks, fillSteps,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(RowFillLab.Table(fillRows));

                    Directory.CreateDirectory(fillOut);
                    string fillPath = Path.Combine(fillOut, "rowfill.csv");
                    File.WriteAllText(fillPath, RowFillLab.Csv(fillRows));
                    Console.WriteLine("csv -> " + fillPath);
                    return 0;
                }

                case "wattsclear":
                {

                    List<int> clearLadder = new List<int>();
                    foreach (int rung in WattsClearLab.DefaultSizes)
                    {
                        if (rung <= max) clearLadder.Add(rung);
                    }
                    if (clearLadder.Count == 0) clearLadder.Add(max);

                    int clearSteps = ticks > 0 ? ticks : 20;
                    string clearOut = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== watts clear ==");
                    Console.WriteLine("  A substep zeroed the watts row and then had the"
                        + " environment pass add into it.");
                    Console.WriteLine("  That pass visits every node before anything reads the row,"
                        + " so it can write the row outright.");
                    Console.WriteLine("  The ladder says whether removing the memset is worth"
                        + " instructions, bandwidth, or nothing.");
                    Console.WriteLine();

                    List<WattsClearLab.Row> clearRows = WattsClearLab.Run(clearLadder, clearSteps,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(WattsClearLab.Table(clearRows));

                    Directory.CreateDirectory(clearOut);
                    string clearPath = Path.Combine(clearOut, "wattsclear.csv");
                    File.WriteAllText(clearPath, WattsClearLab.Csv(clearRows));
                    Console.WriteLine("csv -> " + clearPath);
                    return 0;
                }

                case "steppath":
                {

                    List<int> ladder = new List<int>();
                    foreach (int rung in LoadBenchmarks.DefaultSizes)
                    {
                        if (rung <= max) ladder.Add(rung);
                    }
                    if (ladder.Count == 0) ladder.Add(max);

                    int pathSteps = ticks > 0 ? ticks : 20;
                    string outPath = csvDirectory ?? "out";

                    Console.WriteLine();
                    Console.WriteLine("== step path, " + shape + " ==");
                    Console.WriteLine("  A step driven straight at the solver, against the same step"
                        + " through the host's entry point.");
                    Console.WriteLine("  The difference is the walk over every node that answers"
                        + " how long a step the grid can afford.");
                    Console.WriteLine();

                    List<StepPathLab.Row> pathRows = StepPathLab.Run(shape, ladder,
                        StepPathLab.DefaultCaps, pathSteps,
                        message => Console.Error.WriteLine("  " + message));

                    Console.WriteLine(StepPathLab.Table(pathRows));

                    Directory.CreateDirectory(outPath);
                    string csvPath = Path.Combine(outPath, "steppath.csv");
                    File.WriteAllText(csvPath, StepPathLab.Csv(pathRows));
                    Console.WriteLine("csv -> " + csvPath);
                    return 0;
                }

                case "stagger":
                {
                    int[] sizes = { 4, 16, 64, 242 };
                    int each = size > 0 ? size : 600;

                    int slices = OptionInt(args, "--slices", 8);

                    Console.WriteLine();
                    Console.WriteLine("== stagger against spread, " + each.ToString("n0")
                        + " blocks a grid ==");
                    Console.WriteLine("  identical arithmetic either way; only the interleaving"
                        + " differs, so the difference is locality");
                    Console.WriteLine("  staggered: whole steps, one grid at a time. spread: one"
                        + " step per grid cut into " + slices + " slices and interleaved");
                    Console.WriteLine("  fastest of " + StaggerLab.Repeats + "; the lump is what one"
                        + " frame carries when a grid is stepped whole");
                    Console.WriteLine();

                    Console.WriteLine(StaggerLab.Table(StaggerLab.Run(sizes, each, slices,
                        message => Console.Error.WriteLine("  " + message))));
                    return 0;
                }

                case "surface":
                {
                    Console.WriteLine();
                    Console.Write(SelectiveSurfaceLab.Report(
                        OptionInt(args, "--radiators", 8)));
                    return 0;
                }

                case "parallel":
                {
                    int[] sizes = { 1, 2, 4, 8, 16, 32, 64, 128, 242 };

                    int threads = OptionInt(args, "--threads", Environment.ProcessorCount);
                    int each = size > 0 ? size : 600;

                    Console.WriteLine();
                    Console.WriteLine("== fleet parallelism, " + each.ToString("n0")
                        + " blocks a grid ==");
                    Console.WriteLine("  One grid per work item, joined every fleet-step, against"
                        + " the same fleet stepped in order.");
                    Console.WriteLine("  " + threads + " threads of " + Environment.ProcessorCount
                        + ". Fastest of " + FleetParallelLab.Repeats + " repeats; the noise column"
                        + " is slowest over fastest for each side.");
                    Console.WriteLine("  Hand-off is the same fan-out with nothing in the body -"
                        + " a lower bound on the engine's own.");
                    Console.WriteLine();

                    Console.WriteLine(FleetParallelLab.Table(FleetParallelLab.Run(
                        sizes, each, threads,
                        message => Console.Error.WriteLine("  " + message))));

                    int[] uneven = { 8000, 4000, 2000, 1000, 600, 600, 400, 400, 300, 300,
                                     200, 200, 200, 150, 150, 150, 100, 100, 100, 100 };
                    Console.WriteLine("  uneven fleet: " + uneven.Length + " grids, "
                        + uneven[0].ToString("n0") + " blocks down to " + uneven[uneven.Length - 1]);
                    Console.WriteLine(FleetParallelLab.Table(new List<FleetParallelLab.Row>
                    {
                        FleetParallelLab.RunUneven(uneven, threads,
                            message => Console.Error.WriteLine("  " + message)),
                    }));
                    return 0;
                }

                case "ceiling":
                {

                    float speed = OptionFloat(args, "--speed", 200f);

                    string fixture = Option(args, "--fixture",
                        LoadBenchmarks.CeilingFixtures.Census);

                    Console.WriteLine();
                    Console.WriteLine("== substep ceiling, " + fixture + " " + shape + " "
                        + size.ToString("n0") + " ==");
                    Console.WriteLine("  MaxSubsteps swept, on a hull built from the measured block"
                        + " census, in thick air at " + speed.ToString("n0") + " m/s.");
                    Console.WriteLine("  " + (ticks > 0 ? ticks : 200) + " steps, "
                        + (Has(args, "--driven")
                            ? "the census share of heat producers run to equilibrium"
                            : "temperatures spread 250-750 K")
                        + ". Error is against the run granted its demand.");
                    Console.WriteLine("  --speed N sets the airflow; air is where the demand is,"
                        + " and in vacuum no ceiling binds.");
                    Console.WriteLine("  --fixture census|plumbed|pressurised|rings chooses which"
                        + " element carries the heat: blocks alone, a census hull with rings beside"
                        + " it, one with air in its compartments, or reactors cooled by rings —"
                        + " which is the only one where the plumbing sets the demand.");
                    Console.WriteLine();

                    Console.WriteLine(LoadBenchmarks.CeilingTable(LoadBenchmarks.SubstepCeiling(
                        shape, size, ticks > 0 ? ticks : 200, null,
                        message => Console.Error.WriteLine("  " + message),
                        Has(args, "--driven"), OptionInt(args, "--frequency", 0), speed, 1f,

                        fixture, OptionFloat(args, "--flow", 0f))));
                    return 0;
                }

                case "heatsourcewalk":
                {
                    int hsBlocks = size > 0 ? size : 126731;
                    Console.WriteLine();
                    Console.WriteLine("== heat source walk, ship " + hsBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  What a registered source costs a step, per-substep as shipped -");
                    Console.WriteLine("  a fold into the source row removes all but one (redesign.md, fourth sweep).");
                    Console.WriteLine();
                    Console.WriteLine(HeatSourceWalkLab.Report(shape, hsBlocks,
                        message => Console.Error.WriteLine("  " + message)));
                    return 0;
                }

                case "stepwalks":
                {
                    int walkBlocks = size > 0 ? size : 126731;
                    Console.WriteLine();
                    Console.WriteLine("== step walks, ship " + walkBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  Fusing apply(n) with env(n+1) - three sweeps a substep to two -");
                    Console.WriteLine("  and the real per-step mirror's cost (redesign.md, third sweep).");
                    Console.WriteLine();
                    Console.WriteLine(StepWalksLab.Report(shape, walkBlocks,
                        message => Console.Error.WriteLine("  " + message)));
                    return 0;
                }

                case "envwalk":
                {
                    int envBlocks = size > 0 ? size : 126731;
                    Console.WriteLine();
                    Console.WriteLine("== environment walk shapes, ship " + envBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  The shipped buried-branch walk against a bulk clear plus a compacted");
                    Console.WriteLine("  exposed index, on the real hull's sparsity (redesign.md, second sweep).");
                    Console.WriteLine();
                    Console.WriteLine(EnvironmentWalkLab.Report(shape, envBlocks,
                        message => Console.Error.WriteLine("  " + message)));
                    return 0;
                }

                case "remaplocality":
                {
                    int localityBlocks = size > 0 ? size : 32000;
                    Console.WriteLine();
                    Console.WriteLine("== remap locality, ship " + localityBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  One block changes; the whole box refloods. The changed column is the");
                    Console.WriteLine("  upper bound of what a change-local remap could skip (redesign.md).");
                    Console.WriteLine();
                    Console.WriteLine(RemapLocalityLab.Report(localityBlocks,
                        message => Console.Error.WriteLine("  " + message)));
                    return 0;
                }

                case "activity":
                {
                    int activityBlocks = size > 0 ? size : 32000;
                    Console.WriteLine();
                    Console.WriteLine("== node activity, ship " + activityBlocks.ToString("n0") + " blocks ==");
                    Console.WriteLine("  What share of the grid is thermally quiet, by steps since the last");
                    Console.WriteLine("  event - the ceiling on any sleeping scheme (redesign.md).");
                    Console.WriteLine();
                    Console.WriteLine(NodeActivityLab.Report(activityBlocks,
                        message => Console.Error.WriteLine("  " + message)));
                    return 0;
                }

                case "firststep":
                {
                    int firstBlocks = size > 0 ? size : 125000;
                    int firstSteps = ticks > 0 ? ticks : 6;

                    bool sunlit = Has(args, "--sunlit");

                    Console.WriteLine();
                    Console.WriteLine("== first steps, " + shape + " " + firstBlocks.ToString("n0")
                        + " blocks, " + (sunlit ? "sunlit vacuum" : "shadow") + " ==");
                    Console.WriteLine("  Each of the first " + firstSteps + " full steps on its own"

                        + " clock, on a freshly built grid (`D4`).");
                    Console.WriteLine("  A step whose extra time arrives with the faults is paying"
                        + " first touch; one whose faults match a later step's is paying work.");
                    Console.WriteLine();

                    List<FirstStepLab.Row> firstRows = FirstStepLab.Run(
                        shape, firstBlocks, firstSteps,
                        sunlit ? Worlds.Ab.SunlitVacuum() : Worlds.Shadow(),
                        Has(args, "--warm"));

                    Console.WriteLine(FirstStepLab.Table(firstRows));

                    if (csvDirectory != null)
                    {
                        Directory.CreateDirectory(csvDirectory);
                        string firstPath = Path.Combine(csvDirectory, "firststep.csv");
                        File.WriteAllText(firstPath, FirstStepLab.Csv(firstRows));
                        Console.WriteLine("csv -> " + firstPath);
                    }
                    return 0;
                }

                case "floor":
                {
                    int[] caps = { 0, 32, 16, 8, 6, 4, 3, 2, 1 };

                    Console.WriteLine();
                    Console.WriteLine("== substep floor, " + shape + " " + size.ToString("n0") + " ==");
                    Console.WriteLine("  MaxSubstepsPerBlock swept, on a hull built from the measured"
                        + " block census.");
                    Console.WriteLine("  " + (ticks > 0 ? ticks : 200) + " steps, "
                        + (Has(args, "--driven")
                            ? "the census share of heat producers run to equilibrium"
                            : "temperatures spread 250-750 K")
                        + ", vacuum. Error is against the uncapped run.");
                    Console.WriteLine("  --frequency N sets the step length; every substep count"
                        + " below is proportional to it. --speed N flies it through thick air,"
                        + " which is where a floor has most to reach.");
                    Console.WriteLine();

                    Console.WriteLine(LoadBenchmarks.FloorTable(LoadBenchmarks.SubstepFloor(
                        shape, size, ticks > 0 ? ticks : 200, caps,
                        message => Console.Error.WriteLine("  " + message),
                        Has(args, "--driven"), OptionInt(args, "--frequency", 0),
                        OptionFloat(args, "--speed", 0f))));
                    return 0;
                }

                case "spike":
                {
                    LoadBenchmarks.SpikeReport report = LoadBenchmarks.Spike(shape, size);
                    Console.WriteLine();
                    Console.WriteLine("== spike " + shape + " " + size.ToString("n0") + " ==");
                    Console.WriteLine("  " + report.Blocks.ToString("n0") + " blocks, "
                        + report.Links.ToString("n0") + " links, one block placed.");
                    Console.WriteLine("  " + report.Describe());
                    return 0;
                }

                default:
                    Console.Error.WriteLine("Unknown benchmark: " + name);
                    Console.Error.WriteLine("  one of: " + string.Join(", ", LoadBenchmarks.Names));
                    return 1;
            }
        }


        private static void PrintHitch(HitchResult result)
        {
            Console.WriteLine();
            Console.WriteLine("== " + result.Name + " ==");
            Console.WriteLine("  " + result.Blocks.ToString("n0") + " blocks, "
                + result.Links.ToString("n0") + " links, built in "
                + result.BuildMs.ToString("n0") + " ms.");
            if (result.Notes.Length > 0) Console.WriteLine("  " + result.Notes);
            Console.WriteLine("  " + result.Trace.Describe());
            if (result.SolverTick >= 0) Console.WriteLine("  " + result.DescribeStages());
            Console.WriteLine("  " + result.DescribeGc());
        }


        private static bool Has(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == flag) return true;
            }
            return false;
        }


        private static List<StageLab.Row> StageInSeparateProcesses(string[] args,
            IList<string> stages, string outDirectory)
        {

            List<StageLab.Row> rows = new List<StageLab.Row>();

            for (int i = 0; i < stages.Count; i++)
            {
                string stage = stages[i].Trim();
                string childOut = Path.Combine(outDirectory, "isolated", stage);
                Directory.CreateDirectory(childOut);


                List<string> childArgs = new List<string>();
                for (int a = 0; a < args.Length; a++)
                {
                    if (args[a] == "--isolate") continue;
                    if (args[a] == "--stages" || args[a] == "--csv") { a++; continue; }
                    childArgs.Add(args[a]);
                }
                childArgs.Add("--stages");
                childArgs.Add(stage);
                childArgs.Add("--csv");
                childArgs.Add(childOut);

                Console.Error.WriteLine("  isolated: " + stage);


                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = Environment.ProcessPath;
                foreach (string a in childArgs) start.ArgumentList.Add(a);
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;

                using (Process child = Process.Start(start))
                {
                    child.StandardOutput.ReadToEnd();
                    string errors = child.StandardError.ReadToEnd();
                    child.WaitForExit();

                    if (child.ExitCode != 0)
                    {
                        throw new InvalidOperationException("the isolated run of \"" + stage
                            + "\" exited " + child.ExitCode + ", so this table would be short a row"
                            + " and would look like a table over a different set of stages: "
                            + errors.Trim());
                    }
                }

                rows.AddRange(StageLab.ReadCsv(Path.Combine(childOut, "stages.csv")));
            }

            return rows;
        }


        private static string Option(string[] args, string flag, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag) return args[i + 1];
            }
            return fallback;
        }


        private static int OptionInt(string[] args, string flag, int fallback)
        {

            string raw = Option(args, flag, null);
            int value;
            return raw != null && int.TryParse(raw, out value) ? value : fallback;
        }


        private static int[] ParseInts(string list)
        {
            string[] parts = list.Split(',');
            int[] values = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                values[i] = int.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            }
            return values;
        }


        private static float OptionFloat(string[] args, string flag, float fallback)
        {

            string raw = Option(args, flag, null);
            float value;
            return raw != null
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value : fallback;
        }


        private static void PrintTable(ScenarioResult result)
        {
            string[] lines = result.Csv.Split('\n');
            int shown = Math.Min(lines.Length, 14);
            for (int i = 0; i < shown; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                Console.WriteLine("  " + line);
            }
            if (lines.Length > shown) Console.WriteLine("  ... " + (lines.Length - shown) + " more rows");
        }


        private static int DumpCommand(string[] args)
        {

            string path = ValueAfter(args, "--path") ?? DumpAudit.DefaultPath();
            if (path == null)
            {
                Console.WriteLine("No dump given. Point --path at a world's storage folder, or at one");
                Console.WriteLine("Thermodynamics_Environment_*.csv, and set THERMAL_DUMPS to skip saying so.");
                return 2;
            }

            string file = DumpAudit.Newest(path);
            if (file == null)
            {
                Console.WriteLine("No Thermodynamics_Environment_*.csv under " + path);
                return 2;
            }

            DumpAudit.Result result = DumpAudit.Run(file);
            Console.Write(DumpAudit.Report(result));
            return result.Passed ? 0 : 1;
        }


        private static void PrintUsage()
        {
            Console.WriteLine("Thermodynamics simulation harness");
            Console.WriteLine();
            Console.WriteLine("  list                    show available scenarios");
            Console.WriteLine("  run <name|all>          run a scenario");
            Console.WriteLine("  run <name> --csv <dir>  also write full results as CSV");
            Console.WriteLine();
            Console.WriteLine("  balance                 every block costed and measured against vanilla");
            Console.WriteLine("  balance --csv <dir>     also write the block table as CSV");
            Console.WriteLine("  profiles                realism against arcade: what each buys and costs");
            Console.WriteLine("  sweep [--csv <dir>]     every scenario and worst case, every profile");
            Console.WriteLine("  features                mechanism switches in combination, per profile");
            Console.WriteLine("  frequency               where substep cost bottoms out against Frequency");
            Console.WriteLine("  reactors                where a vanilla reactor settles, against its waste fraction");
            Console.WriteLine("  oxygen                  where a vanilla oxygen generator settles, against its waste fraction");
            Console.WriteLine("  triage [--top N] [--census F]  which blocks a balance pass should look at, in order");
            Console.WriteLine("  basevariants [--ships N] [--type T] [--file F]  what the blocks a blueprint spells with no subtype are worth");
            Console.WriteLine("  coolers                 every block that could cool a reactor, stacked against one");
            Console.WriteLine("  designed                a source buried in armour: bolted panels against a loop to the skin");
            Console.WriteLine("                          --sweep --watts --critical --panels for the pickup ladder");
            Console.WriteLine("  conductance             what real units did to the mod's own pipes and radiators");
            Console.WriteLine("  blocks                  every block in the game, derived from its build components");
            Console.WriteLine("  corpus [--path <dir>]   real ships read from blueprints, and what they are made of");
            Console.WriteLine("  corpus-fetch            build a corpus from the workshop; --key, --user, --top, --out");
            Console.WriteLine("    --list-only               list to a manifest without downloading anything");
            Console.WriteLine("  retrofit [--ships N]    fit cooling to real ships and measure what it buys");
            Console.WriteLine("  stiffness [--path <dir>] what real ships demand of a step, against the census hull");
            Console.WriteLine("    --csv <dir>               one row per ship, so the distribution can be read");
            Console.WriteLine("  screen [--path <dir>]   measure every ship, and cut the corpus to a panel");
            Console.WriteLine("    --panel N                 how many specimens to select");
            Console.WriteLine("  battery [--panel N]     every specimen through every scenario");
            Console.WriteLine("    --linear                  one at a time: for any figure that is a duration");
            Console.WriteLine("    --all                     every usable ship, not just the panel");
            Console.WriteLine("  hotspot --ship <name>   why one block on one ship is the hottest thing on it");
            Console.WriteLine("    --scenario <name> --top N");
            Console.WriteLine("  sealed [--path <dir>]   blocks with nowhere at all to send their heat");
            Console.WriteLine("  dump [--path <dir>]     audit a field telemetry dump against the model's own claims");
            Console.WriteLine("  descent [--csv <dir>]   surface to core: sun, wind, rock damping and planet heat");
            Console.WriteLine("  prefabs [--limit N]     G7: every ship the game spawns, simulated as it arrives");
            Console.WriteLine("    --csv <dir>               one row per prefab");
            Console.WriteLine("    --load                    full load instead of idle: the control, not G7");
            Console.WriteLine("  drift                   how long a client that joined stale stays wrong");
            Console.WriteLine("  inputs                  each input a client drives its sim from, degraded");
            Console.WriteLine("  aero-environment                report installed game content for input fingerprinting");
            Console.WriteLine("  aero-corpus-inputs <blueprint>   select a population candidate and export its inputs");
            Console.WriteLine("  aero-population-drag <blueprint> <thrust.json>   compare profile drag with certified thrust");
            Console.WriteLine("  aero-inputs <blueprint> [--json]   audit saved grid frames and atmosphere-adjusted rated thruster vectors");
            Console.WriteLine("  aero-flight             the step 1 moment rigs: a shuttlecock both ways round, and a spin decaying");
            Console.WriteLine("  aero-profile            profile build, first-tick, settled-tick and centre-change cost on synthetic cubes");
            Console.WriteLine("  aero-calibrate          fit the drag model to White's Table 7.3 and predict what it was not fitted to");
            Console.WriteLine("  thermal-corpus-export <out>  temperature fields of three corpus ships, for the thermal-vision studies");
            Console.WriteLine("  thermal-vision <out> [--require-complete]  shared rendering core, synthetic depth and readiness report");
            Console.WriteLine("  thermal-vision-rays <out>  synthetic sensor resolution and query-workload study (not game rendering)");
            Console.WriteLine("  thermal-vision-reuse <out>  bounded sample reuse, camera motion and new occluder counterexamples");
            Console.WriteLine("  thermal-vision-volumes <out>  temperature ownership counterexamples; exits 2 (incomplete)");
            Console.WriteLine("  occlusion               what a terminator crossing costs at each rung of the shadow ladder");
            Console.WriteLine("  roomsweep               what the room pressure sweep costs as a grid gains compartments");
            Console.WriteLine("    --scenario shadow|sunlit|planet  --watch <s> --size N --csv <dir>");
            Console.WriteLine("  planets                 every shipped world's climate, and where each figure came from");
            Console.WriteLine("    --xml | --write <path>    the generated Planets.xml");
            Console.WriteLine("  wind                    a day of wind over one world; --planet, --weather, --csv");
            Console.WriteLine("    scenarios                 the model on every world, at every size and corner");
            Console.WriteLine();
            Console.WriteLine("  bench scale             cost per stage as the grid grows");
            Console.WriteLine("  bench hitch --size N    per-tick cost, with a block welded mid-run");
            Console.WriteLine("  bench weld  --size N    a block welded on every tick");
            Console.WriteLine("  bench load  --size N    what building the grid costs before tick one");
            Console.WriteLine("  bench floor --size N    what a per-block substep cap buys, and costs");
            Console.WriteLine("  bench ceiling --size N  what refusing a substep demand costs, in air");
            Console.WriteLine("      --fixture census|plumbed|pressurised|rings   which element carries it");
            Console.WriteLine("      --flow N   parcels a second on the rings fixture; above one a"
                + " substep the ring mixes rather than carries");
            Console.WriteLine("  bench parallel --size N one grid per thread: does a fleet pay for it");
            Console.WriteLine("  bench surface           what a selective surface on the radiator is worth");
            Console.WriteLine("  bench stagger --size N  whole steps against spread ones: what locality costs");
            Console.WriteLine("  bench report            full performance report; --baseline <csv> to compare; --repeats N per case");
            Console.WriteLine("  bench spike --size N    one block placed, split by stage");
            Console.WriteLine("  bench firststep --size N  the first steps of a grid's life, with the page faults beside them; --sunlit; --warm (JIT the step path on a throwaway grid first); --ticks N steps");
            Console.WriteLine("  bench heatsourcewalk --size N  what a registered heat source costs a step, per-substep against a fold");
            Console.WriteLine("  bench stepwalks --size N  the fused apply+env pair against the separate walks, and the per-step mirror's cost");
            Console.WriteLine("  bench envwalk --size N    the environment read's buried-branch walk against a compacted exposed index");
            Console.WriteLine("  bench remaplocality --size N  how much of a full room remap one block change actually moves");
            Console.WriteLine("  bench activity --size N   what share of the grid is thermally quiet, by steps since the last event");
            Console.WriteLine("  bench steppath          a step at the solver, against a step through the host");
            Console.WriteLine("  bench stages            one stage of a grid's life on its own clock, fastest of a settled sample; --stages a,b; --repeats N (the floor); --isolate (a process per stage); --trace");
            Console.WriteLine("  bench samplestats       which summary of a stage's repeats two runs agree on; --from dir1,dir2,...");
            Console.WriteLine("  bench stepphases        where a step's own time goes: environment, conduction, coupled, apply, publish");
            Console.WriteLine("  bench se2tax            the same hull on an SE2-fine lattice, stage by stage; --factors 1,2,5,10");
            Console.WriteLine("  bench coarserooms       the supercell room flood against the shipped mapper, verified cell for cell; --refine K --edges a,b");
            Console.WriteLine("  bench condlayout        the conduction kernel in SE2's job shapes: scatter, CSR gather, a thread ladder, Morton order; --threads a,b");
            Console.WriteLine("  bench stepfloor         what those passes would cost touching the same memory and computing nothing");
            Console.WriteLine("  bench smallgrids        what one grid costs before any of its blocks do");
            Console.WriteLine("  bench wattsclear        what zeroing the watts row costs, up a size ladder");
            Console.WriteLine("  bench rowfill           what the first substep of a step pays over a later one");
            Console.WriteLine("  bench allowance         what the element-visit allowance costs, and what it buys");
            Console.WriteLine("  bench franken --size N  a grid of N blocks welded out of real workshop ships");
            Console.WriteLine("  bench pace  --size N    does slowing sim and raising transfer save anything");
            Console.WriteLine("  bench reach --length N  how fast heat crosses a grid, against what it costs");
            Console.WriteLine("  bench memory --size N   where a grid's memory goes, by structure");
            Console.WriteLine("  bench coolant           the segmented fluid model against the well-mixed one");
            Console.WriteLine("  bench elements          what a substep spends per node and per link");
            Console.WriteLine("  bench stability --seconds N   how far the clock can be pushed before a step diverges");
            Console.WriteLine("    --shape ship|cube|truss   --max N   --ticks N   --csv <dir>");
            Console.WriteLine("    --diagnostics             as telemetry runs it: per-node watts on");
        }
    }
}
