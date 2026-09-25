using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using System.Xml.Linq;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Thermal properties derived from a planet's own generator definition, and the shipped file
    /// they generate.
    ///
    /// <para>Every world in this mod used to run one entry — an earthlike climate — so the Moon,
    /// Titan and Pertam were all 294 K by day. These hold the derivation that replaced it, the
    /// judgement calls layered on top of it, and the fact that <c>Data/Planets.xml</c> is what this
    /// code produces rather than something that has drifted away from it.</para>
    /// </summary>
    public class PlanetThermalTests
    {

        private static PlanetLab.World Find(string subtype)
        {
            List<PlanetLab.World> worlds = PlanetLab.Vanilla();
            for (int i = 0; i < worlds.Count; i++)
            {
                if (worlds[i].Subtype == subtype) return worlds[i];
            }
            throw new ArgumentException("no world " + subtype);
        }


        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        [Fact]

        public void TheFiveEngineLevelsMapToTheirAnchoredTemperatures()
        {
            Assert.Equal(100f, PlanetThermalDerivation.MeanTemperature(0f), 2);
            Assert.Equal(215f, PlanetThermalDerivation.MeanTemperature(0.25f), 2);
            Assert.Equal(288f, PlanetThermalDerivation.MeanTemperature(0.5f), 2);
            Assert.Equal(325f, PlanetThermalDerivation.MeanTemperature(0.75f), 2);
            Assert.Equal(450f, PlanetThermalDerivation.MeanTemperature(1f), 2);
        }

        [Fact]

        public void TheAnchorsAreNotEvenlySpacedAndInterpolatingThemLinearlyWouldFreezeEarth()
        {
            float evenlySpaced = 100f + ((450f - 100f) * 0.5f);

            Assert.True(Math.Abs(evenlySpaced - 288f) > 10f);
            Assert.Equal(288f, PlanetThermalDerivation.MeanTemperature(0.5f), 1);
        }

        [Fact]

        public void ALevelBetweenTwoAnchorsLandsBetweenThem()
        {
            float between = PlanetThermalDerivation.MeanTemperature(0.375f);
            Assert.InRange(between, 215f, 288f);
        }


        [Fact]

        public void TheLapseRateIsGravityOverSpecificHeatAndComesOutAtEarthsMeasuredValue()
        {
            float earthlike = PlanetThermalDerivation.LapseRate(1f, true, 1f);
            Assert.InRange(earthlike, 6.2f, 6.7f);
        }

        [Fact]

        public void LowGravityAndHeavierAirBothFlattenTheLapseRate()
        {
            float titan = PlanetThermalDerivation.LapseRate(0.25f, true, 1f);
            float earth = PlanetThermalDerivation.LapseRate(1f, true, 1f);

            Assert.InRange(titan / earth, 0.2f, 0.3f);

            Assert.True(
                PlanetThermalDerivation.LapseRate(1f, false, 1f)
                > PlanetThermalDerivation.LapseRate(1f, true, 1f));
        }

        [Fact]

        public void AWorldWithNoAirHasNoLapseRateAndNoConvectionAndAlmostNoLag()
        {
            Assert.Equal(0f, PlanetThermalDerivation.LapseRate(1f, true, 0f), 4);
            Assert.Equal(0f, PlanetThermalDerivation.ConvectionCoefficient(0f), 4);
            Assert.Equal(0f, PlanetThermalDerivation.SolarDecay(1.8f, 0f), 4);
            Assert.InRange(PlanetThermalDerivation.LagSeconds(0f), 0f, 10f);
        }

        [Fact]

        public void TakingTheAirAwayOpensTheDayNightSwingEnormously()
        {
            Assert.Equal(PlanetThermalDerivation.ThickAirSwing, PlanetThermalDerivation.Swing(1f), 2);
            Assert.Equal(PlanetThermalDerivation.AirlessSwing, PlanetThermalDerivation.Swing(0f), 2);

            Assert.True(PlanetThermalDerivation.Swing(0f) > PlanetThermalDerivation.Swing(1f) * 15f);
        }

        [Fact]

        public void TheFirstBreathOfAirDoesMostOfTheDamping()
        {
            float half = PlanetThermalDerivation.Swing(0.5f);
            float airless = PlanetThermalDerivation.Swing(0f);

            Assert.True(half < airless * 0.4f, "half an atmosphere still swung " + half + " K");
        }

        [Fact]

        public void TheEnginesSolarProtectionBecomesAPlausibleShareOfSunlight()
        {
            Assert.InRange(PlanetThermalDerivation.SolarDecay(1.8f, 1f), 0.25f, 0.35f);
            Assert.InRange(PlanetThermalDerivation.SolarDecay(0.2f, 1f), 0f, 0.06f);
            Assert.Equal(0f, PlanetThermalDerivation.SolarDecay(0f, 1f), 4);
        }

        [Fact]

        public void NothingDerivedIsEverNonsense()
        {
            float[] levels = { 0f, 0.25f, 0.5f, 0.75f, 1f, 0.37f };
            float[] gravities = { 0f, 0.05f, 0.25f, 1f, 2f, 10f };
            float[] airs = { 0f, 0.01f, 0.5f, 1f, 2f };

            foreach (float level in levels)
                foreach (float gravity in gravities)
                    foreach (float air in airs)
                        foreach (bool breathable in new[] { true, false })
                        {
                            PlanetThermalProperties p = PlanetThermalDerivation.Derive(
                                new PlanetThermalDerivation.Engine
                                {
                                    SurfaceTemperatureLevel = level,
                                    SurfaceGravity = gravity,
                                    HasAtmosphere = air > 0f,
                                    AtmosphereDensity = air,
                                    Breathable = breathable,
                                    SolarRadiationProtection = 1.8f,
                                });

                            Assert.True(p.NightTemperature >= 0f, "negative night temperature");
                            Assert.True(p.DayTemperature >= p.NightTemperature, "night warmer than day");
                            Assert.False(float.IsNaN(p.AmbientLapseRate));
                            Assert.InRange(p.SolarDecay, 0f, 0.9f);
                            Assert.True(p.ConvectionCoefficient >= 0f);
                            Assert.True(p.AmbientLagSeconds > 0f);
                            Assert.InRange(p.PoleTemperatureDrop, 0f, 200f);
                        }
        }


        [Fact]

        public void EveryShippedWorldGetsItsOwnClimateRatherThanOneSharedOne()
        {
            List<PlanetLab.World> worlds = PlanetLab.Vanilla();
            Assert.Equal(8, worlds.Count);


            HashSet<string> distinct = new HashSet<string>();
            for (int i = 0; i < worlds.Count; i++)
            {
                PlanetThermalProperties p = worlds[i].Shipped;
                distinct.Add(p.DayTemperature.ToString("n1") + "/" + p.NightTemperature.ToString("n1"));
            }

            Assert.True(distinct.Count >= 5,
                "only " + distinct.Count + " distinct climates across eight worlds");
        }

        [Fact]

        public void TheMoonIsAirlessAndBehavesLikeIt()
        {

            PlanetThermalProperties moon = Find("Moon").Shipped;

            Assert.Equal(0f, moon.ConvectionCoefficient, 3);
            Assert.Equal(0f, moon.AmbientLapseRate, 3);
            Assert.Equal(0f, moon.SolarDecay, 3);

            Assert.True(moon.DayTemperature - moon.NightTemperature > 200f,
                "an airless world should swing enormously, got "
                + (moon.DayTemperature - moon.NightTemperature));

            Assert.True(moon.PoleTemperatureDrop > Find("EarthLike").Shipped.PoleTemperatureDrop);
        }

        [Fact]

        public void AnAuthoredTemperatureLevelIsFollowedEvenWhereTheRealBodyDisagrees()
        {

            PlanetLab.World triton = Find("Triton");

            Assert.Equal("ExtremeFreeze", triton.AuthoredTemperatureLevel);
            Assert.Null(triton.OverrideReason);
            Assert.InRange(triton.Shipped.NightTemperature, 80f, 110f);
        }

        [Fact]

        public void AnUnauthoredLevelOnAWorldNamedForARealPlaceIsNotTreatedAsIntent()
        {
            foreach (string name in new[] { "Titan", "Mars", "Moon" })
            {

                PlanetLab.World world = Find(name);

                Assert.Null(world.AuthoredTemperatureLevel);
                Assert.NotNull(world.OverrideReason);

                float derived = (world.Derived.DayTemperature + world.Derived.NightTemperature) * 0.5f;
                float shipped = (world.Shipped.DayTemperature + world.Shipped.NightTemperature) * 0.5f;

                Assert.True(Math.Abs(derived - shipped) > 20f,
                    name + " override changed nothing: " + derived + " vs " + shipped);
            }

            Assert.InRange(Find("Titan").Shipped.DayTemperature, 80f, 110f);
            Assert.InRange(Find("Mars").Shipped.DayTemperature, 230f, 260f);
        }

        [Fact]

        public void AWorldTheGameIsSilentAboutAndThatIsNotARealPlaceKeepsTheDerivation()
        {
            foreach (string name in new[] { "EarthLike", "Alien", "Pertam" })
            {
                Assert.Null(Find(name).OverrideReason);
            }
        }

        [Fact]

        public void ThePlanetsWithLowGravityHaveTheFlattestLapseRates()
        {
            Assert.True(Find("Titan").Shipped.AmbientLapseRate < 2.5f);
            Assert.True(Find("Europa").Shipped.AmbientLapseRate < 2.5f);
            Assert.True(Find("Pertam").Shipped.AmbientLapseRate > 7f);
        }


        [Fact]

        public void TheShippedPlanetsFileIsWhatThisCodeGenerates()
        {
            string path = Path.Combine(ShippedBlocks.DataRoot(), "Planets.xml");
            string onDisk = File.ReadAllText(path);
            string generated = PlanetLab.Xml();

            if (onDisk == generated) return;

            string[] a = onDisk.Replace("\r\n", "\n").Split('\n');
            string[] b = generated.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] == b[i]) continue;

                Assert.Fail("Thermodynamics/Content/Data/Planets.xml has drifted from PlanetLab.Xml() at line " + (i + 1)
                    + ".\n  on disk:    " + a[i]
                    + "\n  generated:  " + b[i]
                    + "\nRegenerate with: dotnet run --project Thermodynamics.Sim -- planets"
                    + " --write ../Thermodynamics/Content/Data/Planets.xml");
            }

            Assert.Fail("Thermodynamics/Content/Data/Planets.xml has " + a.Length + " lines against the generator's "
                + b.Length + "; regenerate it.");
        }

        [Fact]

        public void TheShippedPlanetsFileIsCompleteAndReadable()
        {
            string path = Path.Combine(ShippedBlocks.DataRoot(), "Planets.xml");
            string onDisk = File.ReadAllText(path);

            Assert.Contains("<SubtypeId>DefaultThermodynamics</SubtypeId>", onDisk);

            List<PlanetLab.World> worlds = PlanetLab.Vanilla();
            for (int i = 0; i < worlds.Count; i++)
            {
                Assert.Contains("<SubtypeId>" + worlds[i].Subtype + "</SubtypeId>", onDisk);
            }

            foreach (XElement definition in XDocument.Parse(onDisk).Descendants("Definition"))
            {
                string subtype = (string)definition.Element("Id").Element("SubtypeId");

                HashSet<string> declared = new HashSet<string>();
                foreach (XElement value in definition.Descendants("Decimal"))
                {
                    XAttribute key = value.Attribute("Name");
                    if (key != null) declared.Add(key.Value);
                }

                foreach (string required in PlanetProperties)
                {
                    Assert.True(declared.Contains(required), subtype + " omits " + required);
                }
            }
        }

        private static readonly string[] PlanetProperties =
        {
            "DayTemperature",
            "NightTemperature",
            "PoleTemperatureDrop",
            "AmbientLagSeconds",
            "AmbientLapseRate",
            "UndergroundTemperature",
            "UndergroundDampingDepth",
            "CoreTemperature",
            "SealevelDeadzone",
            "SolarDecay",
            "ConvectionCoefficient",
        };

        [Fact]

        public void TheFileCarriesAnEntryForEveryShippedWorldPlusTheFallback()
        {
            string xml = PlanetLab.Xml();

            Assert.Contains("<SubtypeId>DefaultThermodynamics</SubtypeId>", xml);

            List<PlanetLab.World> worlds = PlanetLab.Vanilla();
            for (int i = 0; i < worlds.Count; i++)
            {
                Assert.Contains("<SubtypeId>" + worlds[i].Subtype + "</SubtypeId>", xml);
            }

            Assert.Equal(9, Regex.Matches(xml, "<SubtypeId>").Count);
        }

        [Fact]

        public void TheFallbackEntryIsUnchangedFromWhatTheModAlwaysShipped()
        {

            PlanetThermalProperties defaults = new PlanetThermalProperties();
            string xml = PlanetLab.Xml();

            int start = xml.IndexOf("DefaultThermodynamics", StringComparison.Ordinal);
            int end = xml.IndexOf("</Definition>", start, StringComparison.Ordinal);
            string entry = xml.Substring(start, end - start);

            Assert.Contains("\"DayTemperature\" Value=\"" + defaults.DayTemperature.ToString("0.####"), entry);
            Assert.Contains("\"NightTemperature\" Value=\"" + defaults.NightTemperature.ToString("0.####"), entry);
            Assert.Contains("\"ConvectionCoefficient\" Value=\"" + defaults.ConvectionCoefficient.ToString("0.####"), entry);
        }

        [Fact]

        public void EveryEntryCarriesEveryPropertyTheModReads()
        {
            string[] required =
            {
                "NightTemperature", "DayTemperature", "PoleTemperatureDrop", "AmbientLagSeconds",
                "AmbientLapseRate", "UndergroundTemperature", "UndergroundDampingDepth",
                "CoreTemperature", "SealevelDeadzone", "SolarDecay", "ConvectionCoefficient",
            };

            string xml = PlanetLab.Xml();

            for (int i = 0; i < required.Length; i++)
            {
                int count = Regex.Matches(xml, "Name=\"" + required[i] + "\"").Count;
                Assert.Equal(9, count);
            }
        }

        [Fact]

        public void EveryDepartureFromTheDerivationSaysWhyInTheFileItself()
        {
            string xml = PlanetLab.Xml();
            List<PlanetLab.World> worlds = PlanetLab.Vanilla();

            int departures = 0;
            for (int i = 0; i < worlds.Count; i++)
            {
                if (worlds[i].OverrideReason == null) continue;
                departures++;

                Assert.Contains(worlds[i].OverrideReason, xml);
            }

            Assert.True(departures >= 3, "expected the unauthored worlds to carry a reason each");
            Assert.Contains("DEPARTS FROM THE DERIVATION", xml);
        }
    }
}
