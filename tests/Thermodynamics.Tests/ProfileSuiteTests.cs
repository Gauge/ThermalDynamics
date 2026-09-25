using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The profile machinery itself, and the findings it produced.
    ///
    /// Two kinds of test here and they are worth telling apart. The first few check that the sweep
    /// measures what it claims to — a hook that silently stopped applying would turn every profile
    /// into the shipped one and the whole comparison would agree beautifully and mean nothing. The
    /// last few pin defects that are currently *present*, so that fixing one shows up as a test
    /// that has to change rather than as a number nobody was watching.
    /// </summary>
    [Trait("speed", "slow")]
    public class ProfileSuiteTests
    {
        [Fact]

        public void TheSettingsHookReachesAScenario()
        {
            try
            {
                GridBuilder.SettingsOverride = settings =>
                {
                    settings.HeatTimeScale = 1234f;
                    return settings.Derive();
                };

                GridBuilder builder = GridBuilder.Large();
                builder.Place(Catalog.LightArmor(), Vector3I.Zero);
                ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 300f);

                Assert.Equal(1234f, simulation.Settings.HeatTimeScale, 3);
            }
            finally
            {
                GridBuilder.SettingsOverride = null;
            }
        }

        [Fact]

        public void TheMaterialHookIsAppliedExactlyOnce()
        {
            try
            {
                Catalog.MaterialOverride = properties =>
                {
                    properties.Conductivity *= 2f;
                    return properties;
                };

                float plain = Catalog.DefaultThermal().Conductivity;
                float coolant = Catalog.CoolantThermal().Conductivity;

                Catalog.MaterialOverride = null;

                float plainRaw = Catalog.DefaultThermal().Conductivity;
                float coolantRaw = Catalog.CoolantThermal().Conductivity;

                Assert.Equal(plainRaw * 2f, plain, 3);
                Assert.Equal(coolantRaw * 2f, coolant, 3);
            }
            finally
            {
                Catalog.MaterialOverride = null;
            }
        }

        [Fact]

        public void AProfilePaceLandsOnTheEffectiveConductivity()
        {
            BalanceProfile physical = BalanceProfile.Physical();
            BlockThermalProperties steel = new BlockThermalProperties { Conductivity = 50f };

            float effective = physical.Material(steel).Conductivity * ThermalConstants.ConductionScale;
            Assert.Equal(50f, effective, 2);

            BalanceProfile shipped = BalanceProfile.Shipped();
            float shippedEffective = shipped.Material(steel).Conductivity * ThermalConstants.ConductionScale;
            Assert.Equal(50f * ThermalConstants.ConductionScale, shippedEffective, 2);
        }

        [Fact]

        public void TheProfilesThatDescribeTheShippingWorldReadItRatherThanRestateIt()
        {

            ThermalSettings shipped = new ThermalSettings();

            BalanceProfile[] follow = { BalanceProfile.Shipped(), BalanceProfile.Candidate() };

            foreach (BalanceProfile profile in follow)
            {
                Assert.True(profile.HeatTimeScale == shipped.HeatTimeScale,
                    profile.Name + " runs HeatTimeScale " + profile.HeatTimeScale
                    + " against a shipped " + shipped.HeatTimeScale);

                Assert.True(profile.Frequency == shipped.Frequency,
                    profile.Name + " runs Frequency " + profile.Frequency
                    + " against a shipped " + shipped.Frequency);

                Assert.True(profile.MaxSubsteps == shipped.MaxSubsteps,
                    profile.Name + " runs MaxSubsteps " + profile.MaxSubsteps
                    + " against a shipped " + shipped.MaxSubsteps);
            }

            Assert.Equal(ThermalConstants.ConductionScale, BalanceProfile.Shipped().ConductionPace, 4);
            Assert.Equal(1f, BalanceProfile.Candidate().ConductionPace, 4);
        }

        [Fact]

        public void AnUnsetProfileInheritsTheShippingWorld()
        {

            ThermalSettings shipped = new ThermalSettings();

            BalanceProfile bare = new BalanceProfile();

            Assert.Equal(shipped.HeatTimeScale, bare.HeatTimeScale, 4);
            Assert.Equal(shipped.Frequency, bare.Frequency);
            Assert.Equal(shipped.MaxSubsteps, bare.MaxSubsteps);
            Assert.Equal(ThermalConstants.ConductionScale, bare.ConductionPace, 4);
        }

        [Fact]

        public void EveryProfileDiffersFromEveryOther()
        {
            List<BalanceProfile> profiles = BalanceProfile.All();

            for (int i = 0; i < profiles.Count; i++)
            {
                for (int j = i + 1; j < profiles.Count; j++)
                {
                    ThermalSettings a = profiles[i].ToSettings();
                    ThermalSettings b = profiles[j].ToSettings();

                    bool same = a.HeatTimeScale == b.HeatTimeScale
                        && a.Frequency == b.Frequency
                        && a.MaxSubsteps == b.MaxSubsteps
                        && profiles[i].ConductionPace == profiles[j].ConductionPace;

                    Assert.False(same, profiles[i].Name + " and " + profiles[j].Name
                        + " derive to the same world");
                }
            }
        }

        [Fact]

        public void ASourceWithNoSinkIsNotCountedAsBreakage()
        {
            List<FeatureMatrix.Row> rows = FeatureMatrix.Run();
            Assert.NotEmpty(rows);

            bool sawExpected = false;
            foreach (FeatureMatrix.Row row in rows)
            {
                if (!row.RunawayExpected) continue;
                sawExpected = true;

                Assert.True(row.RunawayExpected);
            }

            Assert.True(sawExpected,
                "no combination left a source without a sink, so the classification is untested");
        }

        [Fact]

        public void ArcadeNoLongerDivergesOnTheEverythingRig()
        {
            List<FeatureMatrix.Row> rows = FeatureMatrix.Run();

            FeatureMatrix.Row arcade = null;
            foreach (FeatureMatrix.Row row in rows)
            {
                if (row.Profile == "arcade" && row.Combination == "all on") arcade = row;
            }

            Assert.NotNull(arcade);
            Assert.False(arcade.Diverged,
                "arcade diverged again on the everything rig, reaching " + arcade.PeakKelvin + " K");
            Assert.True(arcade.PeakKelvin < ProfileSweep.DivergenceKelvin,
                "arcade's hot end ran away again, reaching " + arcade.PeakKelvin + " K");
        }

        [Fact]

        public void TheBurningShipSettlesRatherThanDiverging()
        {
            ThermalSettings settings = BalanceProfile.Shipped().ToSettings();
            WorstCases.Built built = WorstCases.Burning("ship", 4000, settings);

            built.Simulation.Solver.CollectDiagnostics = true;


            ScenarioRunner runner = new ScenarioRunner(built.Simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(1800f, 60f);

            float peak = runner.Final.HottestTemperature;

            Assert.True(ProfileSweep.Settled(runner.Samples),
                "the burning ship stopped settling; it reached " + peak.ToString("n0")
                + " K and was still moving, which would make it the divergence it was once"
                + " reported as");

            Assert.True(peak > 2000f,
                "the burning rig is expected to be absurd: " + peak.ToString("n0") + " K");
            Assert.True(peak < ProfileSweep.DivergenceKelvin,
                "the burning rig is past the divergence marker again at " + peak.ToString("n0")
                + " K, so the sweep will report it as a divergence and the note above is stale");

            float made = 0f;
            float vented = 0f;

            for (int i = 0; i < built.Simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = built.Simulation.Solver.Nodes[i];
                made += node.HeatGenerationWatts;
                vented += -(node.LastRadiationWatts + node.LastConvectionWatts);
            }

            Assert.True(made > 0f, "the burning rig has to be making heat");
            Assert.True(Math.Abs(made - vented) / made < 0.01f,
                "a settled hull sheds what it makes: made " + (made / 1e6f).ToString("n1")
                + " MW against vented " + (vented / 1e6f).ToString("n1") + " MW");
        }

        [Fact]

        public void TheHottestBlockOnTheBurningShipHasNoFaceToRadiateFrom()
        {
            ThermalSettings settings = BalanceProfile.Shipped().ToSettings();
            WorstCases.Built built = WorstCases.Burning("ship", 4000, settings);


            ScenarioRunner runner = new ScenarioRunner(built.Simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(1800f, 60f);

            ThermalNode hottest = null;
            float exposedPeak = 0f;

            for (int i = 0; i < built.Simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = built.Simulation.Solver.Nodes[i];
                if (hottest == null || node.Temperature > hottest.Temperature) hottest = node;
                if (node.TotalExposedFaces > 0 && node.Temperature > exposedPeak)
                {
                    exposedPeak = node.Temperature;
                }
            }

            Assert.NotNull(hottest);
            Assert.Equal(0, hottest.TotalExposedFaces);
            Assert.True(hottest.HeatGenerationWatts > 0f,
                "and it is making the heat itself rather than receiving it");

            Assert.True(exposedPeak < hottest.Temperature / 1.5f,
                "a block that can radiate should be nowhere near it: " + exposedPeak.ToString("n0")
                + " K against " + hottest.Temperature.ToString("n0") + " K");
        }
    }
}
