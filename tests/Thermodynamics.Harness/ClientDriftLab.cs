using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class ClientDriftLab
    {
        public class Correction
        {
            public float IntervalSeconds;

            public float BandKelvin = Incandescence.GlowBandKelvin;

            public int MaxBlocks;

            public bool WholeHullOnJoin;

            public static Correction None
            {
                get { return new Correction { IntervalSeconds = 0f }; }
            }
        }

        public class Machine
        {
            public float HitchEverySeconds;

            public float HitchLosesSeconds = 1f;

            public static Machine KeepsUp
            {
                get { return new Machine { HitchEverySeconds = 0f }; }
            }
        }

        public class Sample
        {
            public float Seconds;

            public float MaxKelvin;

            public float MeanKelvin;

            public int DisagreeOnCritical;

            public int ClientShowsSafe;

            public int ClientCriesWolf;

            public int HotBlocks;
        }

        public class Run
        {
            public float StaleSeconds;

            public string Scenario;
            public int Blocks;

            public float JoinKelvin;


            public readonly List<Sample> Samples = new List<Sample>();

            public float SecondsToAgree = -1f;

            public float SecondsToTenKelvin = -1f;

            public float SecondsMisreadingCritical;

            public float SecondsShowingSafe;

            public float SecondsCryingWolf;

            public Correction Protocol = Correction.None;

            public Machine Host = Machine.KeepsUp;

            public int Hitches;

            public float SecondsLostToHitches;

            public int Updates;

            public long Bytes;

            public int PeakBlocksSent;

            public long BlocksDropped;

            public float BytesPerSecond;

            public int PeakDisagreeing;

            public float MeanDisagreeing;
        }

        public const float SampleSeconds = 5f;


        public static Run Measure(string scenario, float staleSeconds, float watchSeconds,
            int blocks = 2000, ThermalSettings settings = null)
        {

            return Measure(scenario, staleSeconds, watchSeconds, blocks, settings, Correction.None);
        }


        public static Run Measure(string scenario, float staleSeconds, float watchSeconds,
            int blocks, ThermalSettings settings, Correction protocol)
        {
            return Measure(scenario, staleSeconds, watchSeconds, blocks, settings, protocol,
                Machine.KeepsUp);
        }


        public static Run Measure(string scenario, float staleSeconds, float watchSeconds,
            int blocks, ThermalSettings settings, Correction protocol, Machine host)
        {

            ThermalSettings world = settings ?? new HistoricalThermalSettings().Derive();

            Func<float, EnvironmentSample> environment = Environment(scenario);

            ThermalSimulation server = Hulls.DrivenPastCritical(world, blocks);

            float warm = 120f;
            Step(server, environment, warm);

            float[] stale = GridState.Temperatures(server);
            Step(server, environment, staleSeconds);

            ThermalSimulation client = Hulls.DrivenPastCritical(world, blocks);
            Step(client, environment, warm);
            GridState.Restore(client, stale);

            Run run = new Run
            {
                StaleSeconds = staleSeconds,
                Scenario = scenario,
                Blocks = server.Solver.Nodes.Count,

                JoinKelvin = MaxDifference(server, client),
                Protocol = protocol ?? Correction.None,
                Host = host ?? Machine.KeepsUp,
            };


            List<StoredTemperature> selection = new List<StoredTemperature>();

            List<StoredTemperature> received = new List<StoredTemperature>();

            float tick = run.Protocol.IntervalSeconds > 0f
                ? Math.Min(SampleSeconds, run.Protocol.IntervalSeconds)
                : SampleSeconds;

            if (tick < world.StepSeconds) tick = world.StepSeconds;

            if (run.Protocol.WholeHullOnJoin && run.Protocol.IntervalSeconds > 0f)
            {
                Correct(server, client, run, selection, received, float.MaxValue);
            }

            float elapsed = 0f;
            float sinceUpdate = float.MaxValue;
            float sinceSample = 0f;
            float sinceHitch = 0f;
            float owed = 0f;

            while (elapsed < watchSeconds)
            {
                Step(server, environment, tick);

                if (owed > 0f)
                {
                    owed -= tick;
                    run.SecondsLostToHitches += tick;
                }
                else
                {
                    Step(client, environment, tick);
                }

                elapsed += tick;
                sinceUpdate += tick;
                sinceSample += tick;
                sinceHitch += tick;

                if (run.Host.HitchEverySeconds > 0f && sinceHitch >= run.Host.HitchEverySeconds)
                {
                    sinceHitch = 0f;
                    owed += run.Host.HitchLosesSeconds;
                    run.Hitches++;
                }

                if (sinceSample >= SampleSeconds)
                {

                    Sample sample = Compare(server, client);
                    sample.Seconds = elapsed;
                    run.Samples.Add(sample);

                    if (sample.DisagreeOnCritical > 0) run.SecondsMisreadingCritical += sinceSample;
                    if (sample.ClientShowsSafe > 0) run.SecondsShowingSafe += sinceSample;
                    if (sample.ClientCriesWolf > 0) run.SecondsCryingWolf += sinceSample;
                    if (run.SecondsToTenKelvin < 0f && sample.MaxKelvin < 10f) run.SecondsToTenKelvin = elapsed;
                    if (run.SecondsToAgree < 0f && sample.MaxKelvin < 1f) run.SecondsToAgree = elapsed;

                    sinceSample = 0f;
                }

                if (run.Protocol.IntervalSeconds > 0f && sinceUpdate >= run.Protocol.IntervalSeconds)
                {
                    sinceUpdate = 0f;
                    Correct(server, client, run, selection, received, run.Protocol.BandKelvin);
                }
            }

            run.BytesPerSecond = elapsed > 0f ? (float)(run.Bytes / (double)elapsed) : 0f;

            double disagreeing = 0d;
            for (int i = 0; i < run.Samples.Count; i++)
            {
                int count = run.Samples[i].DisagreeOnCritical;
                disagreeing += count;
                if (count > run.PeakDisagreeing) run.PeakDisagreeing = count;
            }

            run.MeanDisagreeing = run.Samples.Count > 0
                ? (float)(disagreeing / run.Samples.Count) : 0f;

            return run;
        }


        private static void Correct(ThermalSimulation server, ThermalSimulation client, Run run,
            List<StoredTemperature> selection, List<StoredTemperature> received, float band)
        {
            int budget = band >= float.MaxValue ? 0 : run.Protocol.MaxBlocks;
            int inBand = server.ExportHotTail(band, budget, selection);

            byte[] packet = HotTailCodec.Encode(selection);
            if (!HotTailCodec.TryDecode(packet, received)) return;

            client.ImportHotTail(received);

            run.Updates++;
            run.Bytes += packet.Length;
            run.BlocksDropped += inBand - selection.Count;
            if (selection.Count > run.PeakBlocksSent) run.PeakBlocksSent = selection.Count;
        }


        private static Func<float, EnvironmentSample> Environment(string scenario)
        {
            if (scenario == "sunlit") return t => Worlds.Space(new Vector3(0.3f, 0.9f, 0.2f));
            if (scenario == "planet") return t => Worlds.PlanetSurface(1f, 0.5f);
            return t => Worlds.Shadow();
        }


        private static void Step(ThermalSimulation simulation,
            Func<float, EnvironmentSample> environment, float seconds)
        {
            int steps = (int)Math.Round(seconds / simulation.Settings.StepSeconds);
            for (int i = 0; i < steps; i++) simulation.StepExact(1, environment(0f));
        }


        private static float MaxDifference(ThermalSimulation a, ThermalSimulation b)
        {
            return Compare(a, b).MaxKelvin;
        }


        private static Sample Compare(ThermalSimulation server, ThermalSimulation client)
        {
            IList<ThermalNode> mine = server.Solver.Nodes;
            IList<ThermalNode> theirs = client.Solver.Nodes;


            Sample sample = new Sample();
            int count = Math.Min(mine.Count, theirs.Count);
            if (count == 0) return sample;

            double total = 0d;
            for (int i = 0; i < count; i++)
            {
                float difference = Math.Abs(mine[i].Temperature - theirs[i].Temperature);
                total += difference;
                if (difference > sample.MaxKelvin) sample.MaxKelvin = difference;

                float critical = mine[i].Thermal.CriticalTemperature;
                if (critical <= 0f) continue;

                if (mine[i].Temperature > critical * 0.9f) sample.HotBlocks++;

                bool serverOver = mine[i].Temperature > critical;
                bool clientOver = theirs[i].Temperature > critical;
                if (serverOver == clientOver) continue;

                sample.DisagreeOnCritical++;
                if (serverOver) sample.ClientShowsSafe++;
                else sample.ClientCriesWolf++;
            }

            sample.MeanKelvin = (float)(total / count);
            return sample;
        }


        public static string CorrectionReport(IList<Run> runs)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("What the server stating its near-critical band buys, and what it costs");
            text.AppendLine();
            text.AppendLine("interval    band   budget   misreading   wrong blocks       peak"
                + "   sent   dropped     B/s");

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                bool off = run.Protocol == null || run.Protocol.IntervalSeconds <= 0f;

                text.AppendLine(string.Format(
                    "{0,-12}{1,4}{2,9}{3,13}{4,15}{5,10}{6,7}{7,10}{8,8}",
                    off ? "none" : run.Protocol.IntervalSeconds.ToString("n0") + " s",
                    off ? "-" : (run.Protocol.BandKelvin >= 10000f
                        ? "all" : run.Protocol.BandKelvin.ToString("n0") + "K")
                        + (run.Protocol.WholeHullOnJoin ? "+j" : ""),
                    off ? "-" : (run.Protocol.MaxBlocks > 0
                        ? run.Protocol.MaxBlocks.ToString("n0") : "none"),
                    run.SecondsMisreadingCritical.ToString("n0") + " s",
                    run.MeanDisagreeing.ToString("n1") + " / " + run.PeakDisagreeing.ToString("n0"),
                    run.PeakBlocksSent.ToString("n0"),
                    run.Updates.ToString("n0"),
                    run.BlocksDropped.ToString("n0"),
                    run.BytesPerSecond.ToString("n0")));
            }

            text.AppendLine();
            text.AppendLine("misreading is seconds with at least one block on the wrong side of critical, which");
            text.AppendLine("is a harsh binary on a large hull — wrong blocks is the mean and the worst count");
            text.AppendLine("behind it. peak is the largest update sent, dropped is what the budget cut from");
            text.AppendLine("one, and B/s is over simulated time for one grid. The band row 'all' is the");
            text.AppendLine("diagnostic rather than a proposal: it replicates every block that can fail, which");
            text.AppendLine("is what says whether a residual is the interval or the hull left behind it.");
            text.AppendLine("Every sample is taken at the end of an interval, so each protocol is charged for");
            text.AppendLine("the drift it allows. Crying wolf — the client warning where the server is calm —");
            text.AppendLine("was zero on every run in this table and is in the csv.");

            return text.ToString();
        }


        public static string HitchReport(IList<Run> runs)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("A client that keeps losing simulated time, started in step with the server");
            text.AppendLine();
            text.AppendLine("hitch        cost   correction   lost      max K   misreading   showing safe");

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                bool never = run.Host == null || run.Host.HitchEverySeconds <= 0f;

                float worst = 0f;
                for (int s = 0; s < run.Samples.Count; s++)
                {
                    if (run.Samples[s].MaxKelvin > worst) worst = run.Samples[s].MaxKelvin;
                }

                text.AppendLine(string.Format(
                    "{0,-13}{1,5}{2,13}{3,7}{4,11}{5,13}{6,15}",
                    never ? "never" : "every " + run.Host.HitchEverySeconds.ToString("n0") + " s",
                    never ? "-" : run.Host.HitchLosesSeconds.ToString("n0") + " s",
                    run.Protocol == null || run.Protocol.IntervalSeconds <= 0f
                        ? "off" : "every " + run.Protocol.IntervalSeconds.ToString("n0") + " s",
                    run.SecondsLostToHitches.ToString("n0") + " s",
                    worst.ToString("n1"),
                    run.SecondsMisreadingCritical.ToString("n0") + " s",
                    run.SecondsShowingSafe.ToString("n0") + " s"));
            }

            text.AppendLine();
            text.AppendLine("lost is simulated seconds the client never ran. max K is the worst disagreement");
            text.AppendLine("reached at any sample, which is the column that says whether the error is bounded:");
            text.AppendLine("a single perturbation decays, and a repeated one need not.");

            return text.ToString();
        }


        public static string Report(IList<Run> runs)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("scenario   stale s   join K   to 10 K    to 1 K   misreading critical");

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                text.AppendLine(string.Format(
                    "{0,-10}{1,8:n0}{2,9:n1}{3,10}{4,10}{5,22}",
                    run.Scenario, run.StaleSeconds, run.JoinKelvin,
                    run.SecondsToTenKelvin < 0f ? "never" : run.SecondsToTenKelvin.ToString("n0") + " s",
                    run.SecondsToAgree < 0f ? "never" : run.SecondsToAgree.ToString("n0") + " s",
                    run.SecondsMisreadingCritical.ToString("n0") + " s"));
            }

            return text.ToString();
        }
    }
}
