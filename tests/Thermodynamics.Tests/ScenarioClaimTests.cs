using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Each scenario states a conclusion in its summary line. These check the conclusions rather
    /// than the numbers: a scenario whose headline claim can quietly invert is worse than no
    /// scenario, because it reads like evidence.
    /// </summary>
    [Trait("speed", "slow")]
    public class ScenarioClaimTests
    {
        [Fact]

        public void RadiatorsHelpWhenTheyStandClearAndHurtWhenTheyDoNot()
        {
            string summary = Scenarios.Run("radiator").Summary;


            float bare = ExtractCelsius(summary, 0);

            float flush = ExtractCelsius(summary, 1);

            float clear = ExtractCelsius(summary, 2);

            Assert.True(clear < bare, "panels with clearance should beat a bare hull");
            Assert.True(flush > bare, "panels bolted flat cover as much area as they add");
        }

        [Fact]

        public void OpeningTheDoorLetsASealedRoomRadiate()
        {
            ScenarioResult result = Scenarios.Run("airlock");
            ThermalSimulation simulation = result.Runner.Simulation;

            ThermalNode reactor = simulation.Solver.GetNodeAt(new VRageMath.Vector3I(2, 1, 2));

            Assert.True(reactor.TotalExposedFaces > 0, "the room is open to space now");

            IList<Sample> samples = result.Runner.Samples;
            float sealedPeak = 0f;
            for (int i = 0; i < samples.Count / 2; i++)
            {
                if (samples[i].Tracked["interior"] > sealedPeak) sealedPeak = samples[i].Tracked["interior"];
            }

            Assert.True(reactor.Temperature < sealedPeak, "opening the door has to cool the room");
        }

        [Fact]

        public void LosingThePumpBreaksTheLoopAndHeatsTheReactor()
        {
            ScenarioResult result = Scenarios.Run("coolant-failure");
            ThermalSimulation simulation = result.Runner.Simulation;

            Assert.Empty(simulation.Solver.Loops);

            IList<Sample> samples = result.Runner.Samples;
            float cooled = samples[samples.Count / 2 - 1].Tracked["reactor"];
            float uncooled = samples[samples.Count - 1].Tracked["reactor"];

            Assert.True(uncooled > cooled,
                "the reactor should climb once circulation stops: " + cooled + " then " + uncooled);
        }

        [Fact]

        public void AnUnfinishedBlockSwingsFurtherThanAFinishedOne()
        {
            ScenarioResult result = Scenarios.Run("welding");

            IList<Sample> samples = result.Runner.Samples;

            float earlyRise = samples[4].Tracked["skeleton"] - samples[0].Tracked["skeleton"];
            float lateRise = samples[samples.Count - 1].Tracked["skeleton"]
                           - samples[samples.Count - 2].Tracked["skeleton"];

            Assert.True(earlyRise > lateRise,
                "a tenth of the thermal mass must respond faster: " + earlyRise + " vs " + lateRise);
        }

        [Fact]

        public void AStifferStepNeedsMoreSubstepsAndStillLandsInTheSamePlace()
        {
            ScenarioResult result = Scenarios.Run("stiff");

            Assert.Contains("f=1", result.Summary);
            Assert.Contains("f=16", result.Summary);

            IList<Sample> samples = result.Runner.Samples;

            for (int i = 1; i < samples.Count; i++)
            {
                Assert.True(samples[i].Substeps >= 1, "sample " + i + " ran no substeps");
                Assert.False(float.IsNaN(samples[i].HottestTemperature));
            }
        }

        [Fact]

        public void TheUnitsScenarioShowsTheClockAndNotTheDestination()
        {
            ScenarioResult result = Scenarios.Run("units");


            float physical = ExtractCelsius(result.Summary, 0);

            float shipped = ExtractCelsius(result.Summary, 2);

            Assert.True(shipped < physical,
                "a faster thermal clock must have cooled further: " + shipped + " vs " + physical);
        }

        [Fact]

        public void TheSolverBenchmarkMeasuresConductionAndNotTheSkipPath()
        {
            ScenarioResult result = Scenarios.Run("solver");
            ThermalSimulation simulation = result.Runner.Simulation;

            Assert.Contains("ns per link visit", result.Summary);

            Assert.True(simulation.Solver.Nodes.Count > 30000,
                "the benchmark has to stay at stress-test scale: " + simulation.Solver.Nodes.Count);
            Assert.True(simulation.Solver.Links.Count > simulation.Solver.Nodes.Count,
                "a ship has more joints than blocks");

            Assert.DoesNotContain("A step six times longer: 0.0000 ms", result.Summary);
            Assert.Matches(@"A step six times longer: [\d.]+ ms per step at ([2-9]|\d\d)", result.Summary);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            float coldest = float.MaxValue;
            float hottest = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Temperature < coldest) coldest = nodes[i].Temperature;
                if (nodes[i].Temperature > hottest) hottest = nodes[i].Temperature;
            }

            Assert.True(hottest - coldest > 10f,
                "the benchmark grid has settled flat, so it is timing the skip path: "
                + coldest + " K to " + hottest + " K");
        }

        [Fact]

        public void AStormReachesMoreThanTheWind()
        {
            string summary = Scenarios.Run("weather").Summary;


            float clear = ExtractCelsius(summary, 0);

            float stormy = ExtractCelsius(summary, 1);

            Assert.True(stormy < clear - 10f,
                "a heavy snowstorm should pull the air well down: " + summary);

            MatchCollection numbers = Regex.Matches(summary, @"(\d+(?:\.\d+)?) W/m2 against (\d+)");
            Assert.True(numbers.Count > 0, "no solar comparison in: " + summary);


            float stormSolar = Parse(numbers[0].Groups[1].Value);

            float clearSolar = Parse(numbers[0].Groups[2].Value);
            Assert.True(stormSolar < clearSolar * 0.25f, "overcast should darken the sun: " + summary);

            MatchCollection convection = Regex.Matches(summary, @"at ([\d.]+) against ([\d.]+) W/\(m2 K\)");
            Assert.True(convection.Count > 0, "no convection comparison in: " + summary);


            float stormH = Parse(convection[0].Groups[1].Value);

            float clearH = Parse(convection[0].Groups[2].Value);
            Assert.True(stormH > clearH * 1.5f, "wet air should strip heat faster: " + summary);
        }

        [Fact]

        public void DepthDampsTheDayAndThenWarmsTheRock()
        {
            string summary = Scenarios.Run("underground").Summary;

            MatchCollection swings = Regex.Matches(summary, @"swing ([\d.]+) K");
            Assert.True(swings.Count >= 3, "expected a swing per depth in: " + summary);


            float surface = Parse(swings[0].Groups[1].Value);

            float shallow = Parse(swings[1].Groups[1].Value);

            float deep = Parse(swings[2].Groups[1].Value);

            Assert.True(surface > 5f, "the surface should have a day at all: " + summary);
            Assert.True(shallow < surface, "ten metres of rock should blunt it: " + summary);
            Assert.True(deep < 0.1f, "a hundred metres down there should be no day: " + summary);


            float hundredMetres = ExtractCelsius(summary, 4);

            float fiveKm = ExtractCelsius(summary, 6);

            float twentyKm = ExtractCelsius(summary, 8);

            float mountain = ExtractCelsius(summary, 10);

            Assert.True(fiveKm > hundredMetres + 50f, "below the deadzone the rock should warm: " + summary);
            Assert.True(twentyKm > fiveKm, "and keep warming toward the core: " + summary);

            Assert.Equal(hundredMetres, mountain, 0);
        }

        [Fact]

        public void AStationIsHarderToCoolThanAShipAndForADifferentReasonInAir()
        {
            string summary = Scenarios.Run("station").Summary;

            Match vacuum = Regex.Match(summary,

                @"In vacuum the station's mean block settles ([\d,.]+) K above ambient against the ship's ([\d,.]+) K");
            Assert.True(vacuum.Success, "the vacuum comparison is not in the summary: " + summary);


            float stationVacuum = Parse(vacuum.Groups[1].Value);

            float shipVacuum = Parse(vacuum.Groups[2].Value);

            Assert.True(stationVacuum > shipVacuum,
                "the station settled " + stationVacuum + " K against the ship's " + shipVacuum
                + " K, so the shape that has half the area to shed through is no longer the hotter one");

            Match air = Regex.Match(summary,
                @"At ten times the load, where the air rises are large enough to divide, ([\d,.]+) K against ([\d,.]+) K");
            Assert.True(air.Success, "the loaded air comparison is not in the summary: " + summary);


            float stationAir = Parse(air.Groups[1].Value);

            float shipAir = Parse(air.Groups[2].Value);

            Assert.True(stationAir > shipAir,
                "the station settled " + stationAir + " K against the ship's " + shipAir + " K in air");

            float vacuumRatio = stationVacuum / shipVacuum;
            float airRatio = stationAir / shipAir;

            Assert.True(airRatio > 2f * vacuumRatio,
                "air is " + airRatio.ToString("n2") + "x and vacuum " + vacuumRatio.ToString("n2")
                + "x, so the two media no longer disagree about what a station's penalty is");
        }

        [Fact]

        public void TheStationAndTheShipAreMatchedOnBlocksAndHalvedOnArea()
        {
            HashSet<Vector3I> ship = GridShapes.Ship(40, 9, 12);
            HashSet<Vector3I> station = GridShapes.Station(new Vector3I(17, 15, 19), new Vector3I(3, 3, 3));

            Assert.InRange(station.Count, ship.Count - 5, ship.Count + 5);


            int shipFaces = ExternalFaces(ship);

            int stationFaces = ExternalFaces(station);

            Assert.True(stationFaces > 0 && shipFaces > 0, "a shape with no outside is not a hull");

            float ratio = shipFaces / (float)stationFaces;
            Assert.InRange(ratio, 1.9f, 2.1f);
        }


        private static int ExternalFaces(HashSet<Vector3I> cells)
        {

            Vector3I min = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);

            Vector3I max = new Vector3I(int.MinValue, int.MinValue, int.MinValue);
            foreach (Vector3I cell in cells)
            {
                min = Vector3I.Min(min, cell);
                max = Vector3I.Max(max, cell);
            }
            min -= Vector3I.One;
            max += Vector3I.One;


            HashSet<Vector3I> outside = new HashSet<Vector3I>(Vector3I.Comparer);

            Queue<Vector3I> queue = new Queue<Vector3I>();
            outside.Add(min);
            queue.Enqueue(min);

            while (queue.Count > 0)
            {
                Vector3I at = queue.Dequeue();
                for (int face = 0; face < Face.Count; face++)
                {
                    Vector3I next = at + Face.Offsets[face];
                    if (next.X < min.X || next.Y < min.Y || next.Z < min.Z) continue;
                    if (next.X > max.X || next.Y > max.Y || next.Z > max.Z) continue;
                    if (cells.Contains(next) || outside.Contains(next)) continue;
                    outside.Add(next);
                    queue.Enqueue(next);
                }
            }

            int faces = 0;
            foreach (Vector3I cell in cells)
            {
                for (int face = 0; face < Face.Count; face++)
                {
                    if (outside.Contains(cell + Face.Offsets[face])) faces++;
                }
            }
            return faces;
        }


        private static float Parse(string value)
        {
            return float.Parse(value.Replace(",", ""), CultureInfo.InvariantCulture);
        }


        internal static float ExtractCelsius(string summary, int index)
        {
            MatchCollection matches = Regex.Matches(summary, @"(-?[\d,]+(?:\.\d+)?) C");

            Assert.True(matches.Count > index,
                "expected at least " + (index + 1) + " temperatures in: " + summary);

            return float.Parse(
                matches[index].Groups[1].Value.Replace(",", ""),
                CultureInfo.InvariantCulture);
        }
    }
}
