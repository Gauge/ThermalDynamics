using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The one number that says whether a block can survive itself, and the gate on it.
    ///
    /// <para>
    /// The corpus found that every ship carrying a jump drive loses a block, whatever else is true
    /// of it, and the reason turned out not to need a corpus at all: a `LargeJumpDrive` makes
    /// 4.8 MW into 262 m² of its own surface and carries a critical temperature of 689 K, so it
    /// passes its own limit before any question of hull, mounting or cooling arises. That is
    /// checkable from the definition, in milliseconds, and this is the check.
    /// </para>
    ///
    /// <para>
    /// **The known-impossible list is the point of the test.** It is not there to excuse the blocks
    /// on it; it is there so that the day one of them is fixed, or a new block joins them, the suite
    /// says so. Removing an entry should be the last step of fixing a block.
    /// </para>
    /// </summary>
    public class BlockHeatIndexTests
    {
        private readonly ITestOutputHelper output;


        public BlockHeatIndexTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly Dictionary<string, string> KnownImpossible =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "LargePrototechReactor", "index 17.9 — 400 MW at a combustion engine's 0.60, "
                    + "because its TypeId is HydrogenEngine rather than Reactor" },

            };

        [Fact]

        public void NoShippedBlockIsImpossibleToCoolExceptTheOnesAlreadyKnown()
        {
            if (!GameBlocks.IsInstalled) return;

            List<BlockHeatIndex.Reading> readings = BlockHeatIndex.All();
            Assert.True(readings.Count > 0, "no block in the installed game reported any waste heat");


            List<string> unexpected = new List<string>();

            List<string> fixedNow = new List<string>();


            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (BlockHeatIndex.Reading reading in readings)
            {
                bool known = KnownImpossible.ContainsKey(reading.Subtype);
                if (known) seen.Add(reading.Subtype);

                if (reading.Impossible && !known)
                {
                    unexpected.Add(reading.Subtype + " index " + reading.Index.ToString("n2")
                        + " — makes " + (reading.Watts / 1e6f).ToString("n2") + " MW, can shed "
                        + ((reading.RadiatedWatts + reading.ConductedWatts) / 1e6f).ToString("n2")
                        + " MW at its " + reading.CriticalKelvin.ToString("n0") + " K limit");
                }

                if (!reading.Impossible && known)
                {
                    fixedNow.Add(reading.Subtype + " is now at " + reading.Index.ToString("n2")
                        + " — take it off KnownImpossible");
                }
            }

            foreach (KeyValuePair<string, string> entry in KnownImpossible)
            {
                if (!seen.Contains(entry.Key))
                {
                    fixedNow.Add(entry.Key + " no longer makes heat or no longer exists — "
                        + "take it off KnownImpossible");
                }
            }

            Report(readings);


            List<string> problems = new List<string>();
            problems.AddRange(unexpected);
            problems.AddRange(fixedNow);

            Assert.True(problems.Count == 0,
                "the set of blocks that cannot shed their own heat has changed:\n  "
                + string.Join("\n  ", problems));
        }

        [Fact]

        public void TheIndexReadsAsTheFactorTheHeatHasToComeDownBy()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, GameBlocks.Definition> game = GameBlocks.BySubtype();
            GameBlocks.Definition rating;
            if (!game.TryGetValue("LargeJumpDrive", out rating)) return;

            BlockHeatIndex.Reading reading = BlockHeatIndex.Measure(rating);
            Assert.NotNull(reading);

            Assert.InRange(reading.Watts, 6.39e6f, 6.41e6f);

            Assert.Equal(262.5f, reading.AreaSquareMetres, 1);

            Assert.True(reading.Index < 1f,
                "with conduction into an ambient sink the drive is survivable in principle");
            Assert.True(reading.SelfIndex > 5f,
                "on its own skin alone it is nowhere near self-sufficient");

            Assert.True(reading.Watts / reading.Index
                <= reading.RadiatedWatts + reading.ConductedWatts + 1f,
                "waste divided by the index should be what the block can actually shed");
            Assert.True(Math.Abs(reading.Watts / reading.SelfIndex - reading.RadiatedWatts) < 1f,
                "waste divided by the self index should be what its own skin sheds");
        }

        [Fact]

        public void WithNothingRadiatingThePaceIsTheAdiabaticClosedForm()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;
            float target = BlockHeatIndex.AmbientKelvin + 400f;

            float expected = Capacity * 400f / Watts;
            float measured = BlockHeatIndex.SecondsToReach(Capacity, Watts, 0f, target);

            Assert.Equal(expected, measured, 1);

            Assert.Equal(expected * 2f,
                BlockHeatIndex.SecondsToReach(Capacity * 2f, Watts, 0f, target), 1);
            Assert.Equal(expected / 2f,
                BlockHeatIndex.SecondsToReach(Capacity, Watts * 2f, 0f, target), 1);
        }

        [Fact]

        public void APaceIsInfiniteExactlyWhereTheEquilibriumIsBelowTheTarget()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;

            float equilibrium = BlockHeatIndex.AmbientKelvin + 200f;
            float ambient4 = (float)Math.Pow(BlockHeatIndex.AmbientKelvin, 4);
            float coefficient = Watts / ((float)Math.Pow(equilibrium, 4) - ambient4);

            Assert.True(float.IsPositiveInfinity(BlockHeatIndex.SecondsToReach(
                Capacity, Watts, coefficient, BlockHeatIndex.AmbientKelvin + 400f)),
                "a target above equilibrium is never reached");

            float below = BlockHeatIndex.SecondsToReach(
                Capacity, Watts, coefficient, BlockHeatIndex.AmbientKelvin + 150f);
            Assert.True(below > 0f && !float.IsInfinity(below), "below equilibrium is reachable");
            Assert.True(below > Capacity * 150f / Watts,
                "radiating on the way up can only make the climb longer");
        }

        [Fact]

        public void EveryBlocksPaceAgreesWithItsSelfIndex()
        {
            if (!GameBlocks.IsInstalled) return;

            List<BlockHeatIndex.Reading> readings = BlockHeatIndex.All();
            Assert.True(readings.Count > 0, "no block in the installed game reported any waste heat");


            List<string> disagree = new List<string>();

            foreach (BlockHeatIndex.Reading r in readings)
            {
                bool selfSufficient = r.SelfIndex <= 1f;
                bool never = float.IsInfinity(r.SecondsToCritical);

                if (selfSufficient == never) continue;
                if (Math.Abs(r.SelfIndex - 1f) < 0.02f) continue;

                disagree.Add(r.Subtype + ": self index " + r.SelfIndex.ToString("n3")
                    + " but seconds to critical "
                    + (never ? "never" : r.SecondsToCritical.ToString("n1")));
            }

            Assert.True(disagree.Count == 0,
                "pace and self index disagree on:\n  " + string.Join("\n  ", disagree));
        }


        private void Report(List<BlockHeatIndex.Reading> readings)
        {
            output.WriteLine(string.Format("{0,-40}{1,8}{2,8}{3,12}{4,12}{5,10}{6,10}",
                "block", "index", "self", "waste MW", "can shed MW", "crit K", "s to crit"));

            for (int i = 0; i < readings.Count && i < 20; i++)
            {
                BlockHeatIndex.Reading r = readings[i];
                output.WriteLine(string.Format("{0,-40}{1,8:n2}{2,8:n1}{3,12:n2}{4,12:n2}{5,10:n0}{6,10}",
                    r.Subtype, r.Index, r.SelfIndex, r.Watts / 1e6f,
                    (r.RadiatedWatts + r.ConductedWatts) / 1e6f, r.CriticalKelvin,
                    float.IsInfinity(r.SecondsToCritical) ? "never" : r.SecondsToCritical.ToString("n1")));
            }

            string directory = Environment.GetEnvironmentVariable("THERMAL_CORPUS_DATA");
            if (string.IsNullOrEmpty(directory)) return;


            StringBuilder csv = new StringBuilder();
            csv.AppendLine("subtype,type_id,large,source,waste_w,area_m2,emissivity,critical_k,"
                + "radiated_w,conducted_w,index,self_index,hull_area_needed_m2,equilibrium_k,"
                + "capacity_j_per_k,seconds_to_critical,integrity,seconds_critical_to_loss");

            foreach (BlockHeatIndex.Reading r in readings)
            {
                csv.Append(Csv(r.Subtype)).Append(',').Append(Csv(r.TypeId)).Append(',')
                   .Append(r.Large ? 1 : 0).Append(',').Append(Csv(r.Source)).Append(',')
                   .Append(Num(r.Watts)).Append(',').Append(Num(r.AreaSquareMetres)).Append(',')
                   .Append(Num(r.Emissivity)).Append(',').Append(Num(r.CriticalKelvin)).Append(',')
                   .Append(Num(r.RadiatedWatts)).Append(',').Append(Num(r.ConductedWatts)).Append(',')
                   .Append(Num(r.Index)).Append(',').Append(Num(r.SelfIndex)).Append(',')
                   .Append(Num(r.HullAreaNeeded)).Append(',').Append(Num(r.EquilibriumKelvin))
                   .Append(',').Append(Num(r.HeatCapacity)).Append(',')
                   .Append(Num(r.SecondsToCritical)).Append(',')
                   .Append(Num(r.Integrity)).Append(',')
                   .Append(Num(r.SecondsCriticalToLoss))
                   .AppendLine();
            }

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "blockheat.csv"), csv.ToString());
            }
            catch
            {
            }
        }


        private static string Csv(string value)
        {
            return Thermodynamics.Harness.CsvLine.Text(value);
        }


        private static string Num(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "";
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
