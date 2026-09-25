using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The promises <see cref="WindSolver"/> makes to everything that calls it.
    ///
    /// <para>Three callers now run this one function — the grids in game, the planet probe sweep, and
    /// the offline model — which is what stops them drifting apart. The price of that is that the
    /// function's contract is load-bearing for all three, and none of the tests around it were
    /// checking the contract itself: they check what the model *says*, not what the call
    /// guarantees.</para>
    ///
    /// <para>These check the guarantees. A unit direction, a decomposition that adds back up,
    /// determinism, no allocation, and no way to make it produce a number that poisons a solver
    /// downstream.</para>
    /// </summary>
    public class WindSolverContractTests
    {

        private static WindSolver.Inputs Reasonable()
        {
            WindSolver.Inputs inputs = new WindSolver.Inputs();
            inputs.Ceiling = 74f;
            inputs.Up = Vector3.Normalize(new Vector3(1f, 0.4f, 0.2f));

            inputs.Axis = new Vector3(0f, 1f, 0f);
            inputs.WeatherIntensity = 0.3f;
            inputs.WeatherWind = 1f;
            inputs.Variation = 0.5f;
            inputs.HeightAboveGround = 10f;
            inputs.Heating = 0.5f;
            inputs.Roughness = 0.03f;
            inputs.GradientHeight = 600f;
            inputs.DiurnalAmplitude = 0.35f;
            inputs.DiurnalCrossover = 80f;
            inputs.TerrainInfluence = 1f;
            inputs.TerrainRadius = 300f;
            inputs.SlopeStrength = 1f;

            inputs.Terrain = Hillside(120f);
            return inputs;
        }

        [Fact]

        public void ANoughtCeilingIsAStillDirectionlessWindWithEveryModulationNeutral()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Ceiling = 0f;

            WindSolver.Result result = WindSolver.Solve(ref inputs);

            Assert.Equal(0f, result.Speed);
            Assert.Equal(Vector3.Zero, result.Direction);
            Assert.Equal(0f, result.BandShare);
            Assert.Equal(0f, result.SlopeSpeed);
            Assert.Equal(0f, result.Gradient);
            Assert.Equal(0f, result.ChannelDegrees);

            Assert.Equal(1f, result.Profile);
            Assert.Equal(1f, result.Burial);
            Assert.Equal(1f, result.SpeedUp);
            Assert.Equal(1f, result.Shelter);


            WindSolver.Inputs blowing = Reasonable();
            Assert.True(WindSolver.Solve(ref blowing).Speed > 0f);
        }


        private static float[] Hillside(float rise)
        {
            float[] heights = new float[WindTerrain.SampleCount];
            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                double angle = i * (2d * Math.PI / WindTerrain.Bearings);
                float along = (float)Math.Cos(angle);
                heights[WindTerrain.Index(0, i)] = rise * 0.5f * along;
                heights[WindTerrain.Index(1, i)] = rise * along;
            }
            return heights;
        }


        [Fact]

        public void TheDirectionIsAlwaysAUnitVectorOrExactlyZero()
        {

            WindSolver.Inputs inputs = Reasonable();

            for (float heating = 0f; heating <= 1f; heating += 0.1f)
            {
                for (float height = 0f; height < 2000f; height += 137f)
                {
                    inputs.Heating = heating;
                    inputs.HeightAboveGround = height;

                    WindSolver.Result r = WindSolver.Solve(ref inputs);
                    float length = r.Direction.Length();

                    Assert.True(length < 1e-4f || Math.Abs(length - 1f) < 1e-3f,
                        "direction length " + length + " at " + height + " m, heating " + heating);
                }
            }
        }

        [Fact]

        public void TheReportedFactorsMultiplyBackIntoTheReportedSpeed()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.SlopeStrength = 0f;

            for (float height = 1f; height < 500f; height += 61f)
            {
                inputs.HeightAboveGround = height;
                WindSolver.Result r = WindSolver.Solve(ref inputs);

                float rebuilt = inputs.Ceiling * r.BandShare * r.Profile * r.SpeedUp * r.Shelter;

                Assert.Equal(r.Speed, rebuilt, 2);
            }
        }

        [Fact]

        public void WhereSlopeWindBlowsTheDecompositionIsTheOnlyThingThatChanges()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Heating = 0f;
            inputs.HeightAboveGround = 2f;

            WindSolver.Result r = WindSolver.Solve(ref inputs);
            Assert.True(r.SlopeSpeed > 0f, "this fixture is meant to have a slope wind");

            float product = inputs.Ceiling * r.BandShare * r.Profile * r.SpeedUp * r.Shelter;

            Assert.True(Math.Abs(r.Speed - product) <= r.SlopeSpeed + 0.01f,
                "speed " + r.Speed + " against ambient " + product + " and slope " + r.SlopeSpeed);
        }

        [Fact]

        public void TheSameInputsAlwaysGiveTheSameAnswer()
        {

            WindSolver.Inputs inputs = Reasonable();

            WindSolver.Result first = WindSolver.Solve(ref inputs);
            for (int i = 0; i < 200; i++)
            {
                WindSolver.Result again = WindSolver.Solve(ref inputs);

                Assert.Equal(first.Speed, again.Speed, 6);
                Assert.Equal(first.Direction.X, again.Direction.X, 6);
                Assert.Equal(first.Direction.Y, again.Direction.Y, 6);
                Assert.Equal(first.Direction.Z, again.Direction.Z, 6);
                Assert.Equal(first.SlopeSpeed, again.SlopeSpeed, 6);
            }
        }

        [Fact]

        public void SolvingAllocatesNothing()
        {

            WindSolver.Inputs inputs = Reasonable();

            for (int i = 0; i < 1000; i++) WindSolver.Solve(ref inputs);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) WindSolver.Solve(ref inputs);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.True(after - before == 0,
                "10,000 solves allocated " + (after - before) + " bytes");
        }


        [Fact]

        public void ANullTerrainRingIsTreatedAsNoTerrainRatherThanCrashing()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Terrain = null;

            WindSolver.Result r = WindSolver.Solve(ref inputs);

            Assert.Equal(1f, r.SpeedUp, 4);
            Assert.Equal(1f, r.Shelter, 4);
            Assert.Equal(0f, r.SlopeSpeed, 4);
            Assert.True(r.Speed > 0f, "there should still be a band wind");
        }

        [Fact]

        public void AShortTerrainRingIsRefusedRatherThanReadPastItsEnd()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Terrain = new float[WindTerrain.SampleCount - 1];

            WindSolver.Result r = WindSolver.Solve(ref inputs);

            Assert.Equal(1f, r.SpeedUp, 4);
            Assert.Equal(1f, r.Shelter, 4);
        }

        [Fact]

        public void NoCeilingMeansNoWindAndEveryFactorLeftAtRest()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Ceiling = 0f;

            WindSolver.Result r = WindSolver.Solve(ref inputs);

            Assert.Equal(0f, r.Speed, 5);
            Assert.Equal(Vector3.Zero, r.Direction);
            Assert.Equal(0f, r.BandShare, 5);
            Assert.Equal(1f, r.SpeedUp, 5);
            Assert.Equal(0f, r.SlopeSpeed, 5);
        }

        [Fact]

        public void ANegativeCeilingIsTreatedAsNone()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Ceiling = -50f;

            Assert.Equal(0f, WindSolver.Solve(ref inputs).Speed, 5);
        }

        [Fact]

        public void AZeroLengthUpOrAxisProducesNoWindRatherThanANaN()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.Up = Vector3.Zero;
            Assert.False(float.IsNaN(WindSolver.Solve(ref inputs).Speed));


            inputs = Reasonable();
            inputs.Axis = Vector3.Zero;
            Assert.False(float.IsNaN(WindSolver.Solve(ref inputs).Speed));
        }

        [Fact]

        public void UpParallelToTheAxisIsAPoleAndHasNoWindDirection()
        {

            WindSolver.Inputs inputs = Reasonable();

            inputs.Up = new Vector3(0f, 1f, 0f);

            inputs.Axis = new Vector3(0f, 1f, 0f);

            WindSolver.Result r = WindSolver.Solve(ref inputs);

            Assert.Equal(0f, r.Speed, 5);
            Assert.Equal(Vector3.Zero, r.Direction);
        }

        [Fact]

        public void NonsenseNumbersDoNotEscapeAsNonsenseWind()
        {
            float[] ceilings = { 0f, 0.001f, 74f, 1e6f };
            float[] heights = { -100f, 0f, 2f, 1e7f };
            float[] heatings = { -1f, 0f, 0.5f, 1f, 5f };
            float[] roughness = { 0f, -1f, 0.03f, 1e5f };
            float[] gradients = { 0f, -50f, 600f, 1e9f };
            float[] radii = { 0f, -300f, 300f, 1e6f };
            float[] influences = { -1f, 0f, 1f, 99f };

            int checks = 0;

            foreach (float ceiling in ceilings)
                foreach (float height in heights)
                    foreach (float heating in heatings)
                        foreach (float rough in roughness)
                            foreach (float gradient in gradients)
                                foreach (float radius in radii)
                                    foreach (float influence in influences)
                                    {

                                        WindSolver.Inputs inputs = Reasonable();
                                        inputs.Ceiling = ceiling;
                                        inputs.HeightAboveGround = height;
                                        inputs.Heating = heating;
                                        inputs.Roughness = rough;
                                        inputs.GradientHeight = gradient;
                                        inputs.TerrainRadius = radius;
                                        inputs.TerrainInfluence = influence;

                                        WindSolver.Result r = WindSolver.Solve(ref inputs);
                                        checks++;

                                        Assert.False(float.IsNaN(r.Speed), "NaN speed");
                                        Assert.False(float.IsInfinity(r.Speed), "infinite speed");
                                        Assert.True(r.Speed >= 0f, "negative speed " + r.Speed);

                                        Assert.False(float.IsNaN(r.SpeedUp));
                                        Assert.False(float.IsNaN(r.Shelter));
                                        Assert.False(float.IsNaN(r.SlopeSpeed));
                                        Assert.False(float.IsNaN(r.ChannelDegrees));
                                        Assert.False(float.IsNaN(r.Direction.X));
                                    }

            Assert.True(checks > 5000, "only " + checks + " combinations swept");
        }

        [Fact]

        public void SlopeWindNeverCarriesTheWindPastThePlanetsOwnFigure()
        {

            WindSolver.Inputs inputs = Reasonable();
            inputs.WeatherIntensity = 0f;

            inputs.Terrain = Hillside(400f);

            for (float ceiling = 0.5f; ceiling < 80f; ceiling *= 1.7f)
            {
                inputs.Ceiling = ceiling;

                for (float heating = 0f; heating <= 1f; heating += 0.25f)
                {
                    inputs.Heating = heating;
                    inputs.HeightAboveGround = 2f;

                    WindSolver.Result r = WindSolver.Solve(ref inputs);

                    Assert.True(r.Speed <= ceiling + 1e-3f,
                        "ceiling " + ceiling + " but wind " + r.Speed + " at heating " + heating);
                }
            }
        }


        [Fact]

        public void TheAnimationExportIsSelfConsistentAndFreeOfNonsense()
        {
            string json = WindAnimation.Json();

            Assert.Contains("\"planets\"", json);
            Assert.DoesNotContain("NaN", json);
            Assert.DoesNotContain("Infinity", json);
            Assert.DoesNotContain("∞", json);

            int expected = WindAnimation.Times
                * WindAnimation.Heights.Length
                * WindAnimation.Latitudes.Length
                * WindAnimation.Longitudes;


            List<int> lengths = ArrayLengths(json, "\"speed\":");
            Assert.Equal(WindLab.Planet.VanillaNames.Length, lengths.Count);

            for (int i = 0; i < lengths.Count; i++)
            {
                Assert.Equal(expected, lengths[i]);
            }


            List<int> bearings = ArrayLengths(json, "\"bearing\":");
            for (int i = 0; i < bearings.Count; i++) Assert.Equal(expected, bearings[i]);

            int heatingExpected = WindAnimation.Times
                * WindAnimation.Latitudes.Length
                * WindAnimation.Longitudes;


            List<int> heating = ArrayLengths(json, "\"heating\":");
            for (int i = 0; i < heating.Count; i++) Assert.Equal(heatingExpected, heating[i]);
        }

        [Fact]

        public void TheAnimationExportCoversAWholeDayAndBothPolesOfHeating()
        {
            string json = WindAnimation.Json();


            List<int> starts = Offsets(json, "\"heating\":");
            Assert.NotEmpty(starts);

            float low = 1f, high = 0f;
            foreach (float value in Values(json, starts[0]))
            {
                if (value < low) low = value;
                if (value > high) high = value;
            }

            Assert.True(low < 0.05f, "the exported day has no night in it: " + low);
            Assert.True(high > 0.8f, "the exported day has no afternoon in it: " + high);
        }


        private static List<int> Offsets(string json, string key)
        {

            List<int> found = new List<int>();
            int at = 0;
            while (true)
            {
                at = json.IndexOf(key, at, StringComparison.Ordinal);
                if (at < 0) break;
                found.Add(at + key.Length);
                at += key.Length;
            }
            return found;
        }


        private static List<int> ArrayLengths(string json, string key)
        {

            List<int> lengths = new List<int>();
            foreach (int start in Offsets(json, key))
            {
                int open = json.IndexOf('[', start);
                int close = json.IndexOf(']', open);

                string body = json.Substring(open + 1, close - open - 1);
                lengths.Add(body.Length == 0 ? 0 : body.Split(',').Length);
            }
            return lengths;
        }


        private static IEnumerable<float> Values(string json, int start)
        {
            int open = json.IndexOf('[', start);
            int close = json.IndexOf(']', open);

            foreach (string part in json.Substring(open + 1, close - open - 1).Split(','))
            {
                yield return float.Parse(part, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
