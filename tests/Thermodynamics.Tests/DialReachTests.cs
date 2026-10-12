using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Every dial moves something.**
    ///
    /// <para>
    /// A balance pass turns dials and reads what happened. The failure that wastes a pass is a dial
    /// that reaches nothing: the curve comes back flat, which reads as *this dial does not matter*
    /// and is indistinguishable from *this dial is not wired to anything*. This repository has been
    /// caught by that shape repeatedly — a coolant sink face no test ever exercised, a reactor
    /// making no heat while 1,179 tests stayed green, a panel rule that selected no reactors.
    /// </para>
    ///
    /// <para>
    /// <see cref="KnobBaselineTests"/> is the other half and does not overlap: it checks each
    /// dial's *shipped* value is the one that actually ships, so a curve is read from where the mod
    /// sits. This checks the dial is connected to the simulation at all.
    /// </para>
    ///
    /// <para>
    /// **It is a reach test, not a balance test.** It asserts a dial changes an outcome and says
    /// nothing about whether the change is the right size or the right direction — a direction is a
    /// claim about the model and belongs with the model. Keeping it to reach is what makes it
    /// survive every retune without being touched.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class DialReachTests
    {
        private readonly ITestOutputHelper output;


        public DialReachTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static ThermalSimulation Rig(
            ThermalSettings settings,
            Func<string, string, BlockThermalProperties, BlockThermalProperties> material)
        {
            GridBuilder builder = GridBuilder.Large();

            BlockThermalProperties armour = Catalog.DefaultThermal();
            if (material != null) armour = material("CubeBlock", "LargeBlockArmorBlock", armour);

            builder.Shell(BlockModel.Solid("LargeBlockArmorBlock", Vector3I.One, 500f, armour),

                Vector3I.Zero, new Vector3I(4, 4, 4));

            Vanilla.Block reactor = Vanilla.Find("LargeBlockLargeGenerator");
            BlockThermalProperties reactorThermal = reactor.Thermal;
            if (material != null) reactorThermal = material("Reactor", reactor.Subtype, reactorThermal);

            builder.Place(
                BlockModel.Solid(reactor.Subtype, Vector3I.One, reactor.Mass, reactorThermal),

                new Vector3I(1, 1, 1));
            builder.Producing(reactor.PowerOutputMegawatts * ThermalConstants.MegawattsToWatts);

            Vanilla.Block thruster = Vanilla.Find("LargeBlockLargeThrust");
            BlockThermalProperties thrusterThermal = thruster.Thermal;
            if (material != null)
            {

                thrusterThermal = material("Thrust", thruster.Subtype, thrusterThermal);
            }

            builder.Place(
                BlockModel.Solid(thruster.Subtype, Vector3I.One, thruster.Mass, thrusterThermal),

                new Vector3I(2, 1, 1));
            builder.Thrusting(thruster.PowerDrawMegawatts * ThermalConstants.MegawattsToWatts);

            Charging(builder, material, "LargeJumpDrive", new Vector3I(1, 2, 1));
            Charging(builder, material, "LargeHydrogenEngine", new Vector3I(2, 2, 1));

            Charging(builder, material, "LargeBlockOxygenGeneratorLab", new Vector3I(1, 1, 2));

            return builder.BuildSimulation(settings ?? new ThermalSettings());
        }


        private static void Charging(GridBuilder builder,
            Func<string, string, BlockThermalProperties, BlockThermalProperties> material,
            string subtype, Vector3I at)
        {
            Dictionary<string, GameBlocks.Definition> bySubtype = GameBlocks.BySubtype();

            GameBlocks.Definition definition;
            if (!bySubtype.TryGetValue(subtype, out definition))
            {
                if (bySubtype.Count == 0) return;

                throw new InvalidOperationException("the reach rig asks for \"" + subtype
                    + "\", which is not a subtype the game defines — a block that is not placed"
                    + " makes every dial acting only on it read as inert");
            }

            BlockThermalProperties thermal = ShippedBlocks.DeriveWithFunction(
                definition.Components, definition.TypeId, definition.PowerEfficiency);

            if (material != null) thermal = material(definition.TypeId, definition.SubtypeId, thermal);

            builder.Place(BlockModel.Solid(subtype, Vector3I.One, definition.Mass, thermal), at);

            if (definition.PowerOutputWatts > 0f) builder.Producing(definition.PowerOutputWatts);
            else builder.Consuming(definition.PowerDrawWatts);
        }


        private static void Settled(KnobLab.Configuration configuration, string scenario,
            out float hottest, out int overCritical, out float early)
        {

            ThermalSimulation simulation;
            if (configuration.Knob.Name == "friction-threshold")
            {
                // A powered hot hull is above recovery and correctly receives no aerodynamic heat.
                // The threshold therefore needs an unpowered, initially cold exposed surface.
                GridBuilder cold = GridBuilder.Large();
                cold.Place(Catalog.HeavyArmor(), Vector3I.Zero);
                simulation = cold.BuildSimulation(configuration.Settings(), 200f);
            }
            else simulation = Rig(configuration.Settings(), configuration.Material());


            ScenarioRunner runner = new ScenarioRunner(simulation);

            runner.Environment = t => World(scenario);

            runner.Run(300f, 60f);

            early = Hottest(simulation);

            runner.Run(3300f, 300f);

            hottest = 0f;
            overCritical = 0;

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = simulation.Solver.Nodes[i];
                if (node.Temperature > hottest) hottest = node.Temperature;
                if (node.Temperature > node.Thermal.CriticalTemperature) overCritical++;
            }
        }


        private static float Hottest(ThermalSimulation simulation)
        {
            float hottest = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float t = simulation.Solver.Nodes[i].Temperature;
                if (t > hottest) hottest = t;
            }
            return hottest;
        }


        private static EnvironmentSample World(string scenario)
        {
            switch (scenario)
            {
                case "vacuum-sunlit": return Worlds.Ab.SunlitVacuum();
                case "surface-hot-noon": return Worlds.PlanetSurface(1f, 0.5f);
                case "flight-100": return Worlds.Flight(1f, 100f);
                case "reentry": return Worlds.Flight(1f, 1500f);
                default: return Worlds.Shadow();
            }
        }

        [Fact]

        public void EveryDialMovesAnOutcomeSomewhere()
        {
            List<KnobLab.Knob> knobs = KnobLab.Knobs();

            Assert.True(knobs.Count > 10,
                "only " + knobs.Count + " dials were read, so this test would pass on a table"
                + " that had lost most of them");


            List<string> inert = new List<string>();
            int judged = 0;

            foreach (KnobLab.Knob knob in knobs)
            {
                float widest = 0f;
                string where = "";

                foreach (string scenario in knob.Scenarios)
                {
                    float lowest = float.MaxValue;
                    float highest = float.MinValue;
                    int fewestCritical = int.MaxValue;
                    int mostCritical = int.MinValue;
                    float earlyLow = float.MaxValue;
                    float earlyHigh = float.MinValue;

                    foreach (float level in knob.Levels)
                    {
                        KnobLab.Configuration configuration = new KnobLab.Configuration
                        {
                            Knob = knob,
                            Level = level,
                            Scenarios = knob.Scenarios,
                            IsShipped = level == knob.Shipped,
                        };

                        float settled;
                        int over;
                        float early;
                        Settled(configuration, scenario, out settled, out over, out early);
                        judged++;

                        if (settled < lowest) lowest = settled;
                        if (settled > highest) highest = settled;
                        if (over < fewestCritical) fewestCritical = over;
                        if (over > mostCritical) mostCritical = over;
                        if (early < earlyLow) earlyLow = early;
                        if (early > earlyHigh) earlyHigh = early;
                    }

                    float spread = highest - lowest;
                    if (mostCritical > fewestCritical) spread = Math.Max(spread, 1f);
                    spread = Math.Max(spread, earlyHigh - earlyLow);

                    if (spread > widest)
                    {
                        widest = spread;
                        where = scenario;
                    }
                }

                output.WriteLine("{0,-22} widest spread {1,9:n2} K  in {2}", knob.Name, widest, where);

                if (widest < 0.1f) inert.Add(knob.Name + " moved nothing across its own levels");
            }

            Assert.True(judged > 50,
                "only " + judged + " readings were taken, so this test is not exercising the dials");

            inert.Sort(StringComparer.Ordinal);
            Assert.True(inert.Count == 0,
                "dials that reach nothing they are swept in:\n  " + string.Join("\n  ", inert.ToArray()));
        }
    }
}
