using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Every setting the solver carries reaches the solver.**
    ///
    /// <para>
    /// <see cref="DialReachTests"/> makes this check for the eighteen dials `KnobLab` sweeps, and
    /// thirteen of those are *material* dials rather than world settings — so of the settings a
    /// player can move, five had a reach check and the rest had none. `C33` counts the measurement
    /// gap and this closes the silent half of it: a curve that comes back flat reads as *this dial
    /// does not matter* and is indistinguishable from *this dial is not wired to anything*, and
    /// this repository has been caught by that shape four times.
    /// </para>
    ///
    /// <para>
    /// **Enumerated rather than listed**, exactly as <see cref="LoopDialReachTests"/> is, because a
    /// list is what fails: a field added to <see cref="ThermalSettings"/> tomorrow is checked
    /// tomorrow without anyone remembering this file exists. <see cref="SettingsWiringTests"/>
    /// covers the plumbing either side of it — a proto number, a name, a getter, a setter, a clamp,
    /// a documentation row — and every one of those can be right while the value reaches nothing.
    /// </para>
    ///
    /// <para>
    /// **It is a reach test.** It asserts a setting changes an outcome and says nothing about the
    /// direction or the size, which is what lets it survive a retune untouched. What it cannot do
    /// is speak for a setting whose whole effect is outside the core — a debug overlay, the HUD, the
    /// telemetry stride — and those are not on <see cref="ThermalSettings"/> to begin with.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class SettingsDialReachTests
    {
        private readonly ITestOutputHelper output;


        public SettingsDialReachTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static readonly HashSet<string> Caps = new HashSet<string>(StringComparer.Ordinal)
        {
            "MaxSubsteps", "MaxElementVisitsPerStep", "MaxSubstepsPerBlock",
        };

        private static readonly Dictionary<string, string> Exempt =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Version", "the config's own version number, not a dial" },

            { "StepSeconds", "derived from Frequency and SimulationSpeed" },
            { "StepsPerSecond", "derived from Frequency and SimulationSpeed" },
            { "Revision", "a change counter the host reads, not an input" },
        };


        private static List<float> Fingerprint(Action<ThermalSettings> apply)
        {

            List<float> readings = new List<float>();

            BuriedSource(apply, readings);
            OnAPlanet(apply, readings);
            InTheSun(apply, readings);
            NearAStar(apply, readings);
            Flying(apply, readings);
            FlyingInTrail(apply, readings);
            Plumbed(apply, readings);
            Pumped(apply, readings);
            Sealed(apply, readings);
            Cooking(apply, readings);
            Starved(apply, readings);
            OverBudget(apply, readings);
            Paced(apply, readings);
            Suit(apply, readings);

            return readings;
        }


        private static ThermalSettings Settings(Action<ThermalSettings> apply)
        {

            ThermalSettings settings = new ThermalSettings();
            if (apply != null) apply(settings);
            return settings.Derive();
        }


        private static void Read(ThermalSimulation simulation, List<float> into)
        {
            float hottest = 0f;
            float coldest = float.MaxValue;
            float total = 0f;
            int over = 0;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                float t = nodes[i].Temperature;
                if (t > hottest) hottest = t;
                if (t < coldest) coldest = t;
                total += t;
                if (t > nodes[i].Thermal.CriticalTemperature) over++;
            }

            into.Add(hottest);
            into.Add(coldest == float.MaxValue ? 0f : coldest);
            into.Add(total);
            into.Add(over);
            into.Add(simulation.Solver.RequiredSubsteps(0.25f));
            into.Add(simulation.Solver.LastSubsteps);

            IList<OverheatEvent> overheats = simulation.Solver.Overheats;
            float damage = 0f;
            for (int i = 0; overheats != null && i < overheats.Count; i++)
            {
                damage += overheats[i].Damage;
            }
            into.Add(damage);
            into.Add(overheats == null ? 0f : overheats.Count);
        }


        private static void BuriedSource(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));
            builder.Remove(new Vector3I(2, 2, 2));
            builder.Place(Catalog.Reactor(), new Vector3I(2, 2, 2)).Wasting(400000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);
            simulation.StepExact(LabClock.Steps(400), Worlds.Shadow());
            Read(simulation, into);
        }


        private static void OnAPlanet(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));
            builder.Remove(new Vector3I(0, 2, 2));
            builder.Place(Catalog.Reactor(), new Vector3I(0, 2, 2)).Wasting(400000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);
            simulation.StepExact(LabClock.Steps(400), Worlds.PlanetSurface(1f, 0.5f, 22f));
            Read(simulation, into);
        }


        private static void InTheSun(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(6, 1, 2));
            builder.Fill(Catalog.LightArmor(), new Vector3I(0, 0, 2), new Vector3I(2, 5, 6));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 200f);
            simulation.StepExact(LabClock.Steps(300),
                Worlds.Space(new Vector3(0.7f, 0.5f, 0.5f)));

            Read(simulation, into);
        }


        private static void NearAStar(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            EnvironmentSample sample = Worlds.Shadow();
            sample.HeatSources = new HeatSourceState[]
            {

                new HeatSourceState(Vector3.Normalize(new Vector3(1f, 0.2f, 0.1f)), 4000f),
            };
            sample.HeatSourceCount = 1;

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 200f);
            simulation.StepExact(LabClock.Steps(300), sample);

            Read(simulation, into);
        }


        private static void Cooking(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));
            builder.Remove(new Vector3I(1, 1, 1));
            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1)).Wasting(40000000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);
            simulation.StepExact(LabClock.Steps(200), Worlds.Shadow());

            Read(simulation, into);
        }


        private static void Paced(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));
            builder.Remove(new Vector3I(1, 1, 1));
            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1)).Wasting(400000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);

            EnvironmentSample sample = Worlds.Shadow();
            for (int frame = 0; frame < 1800; frame++) simulation.Update(1f / 60f, sample);

            Read(simulation, into);
            into.Add(simulation.StepsCompleted);
        }


        private static void Starved(Action<ThermalSettings> apply, List<float> into)
        {

            ThermalSettings settings = new ThermalSettings();
            if (apply != null) apply(settings);

            settings.MaxSubsteps = 2;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            ThermalSimulation simulation = Hulls.Driven(settings, 300);
            simulation.StepExact(40, Worlds.Ab.MildAtmosphere());

            Read(simulation, into);
            into.Add(simulation.Solver.ConductionClampLive ? 1f : 0f);
        }


        private static void OverBudget(Action<ThermalSettings> apply, List<float> into)
        {

            ThermalSettings settings = new ThermalSettings();
            if (apply != null) apply(settings);

            settings.MaxElementVisitsPerStep = 512;
            settings.Derive();

            ThermalSimulation simulation = Hulls.Driven(settings, 300);
            simulation.StepExact(40, Worlds.Ab.MildAtmosphere());

            Read(simulation, into);
            into.Add(simulation.Solver.AdaptiveSubstepFloor);
            into.Add(simulation.Solver.LastSubsteps);
        }


        private static void Flying(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);
            simulation.StepExact(LabClock.Steps(200), Worlds.Flight(1f, 400f));
            Read(simulation, into);
        }


        private static void FlyingInTrail(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Fill(Catalog.LightArmor(), new Vector3I(0, 0, 4), new Vector3I(4, 4, 6));
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 2));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);
            simulation.StepExact(LabClock.Steps(200), Worlds.Flight(1f, 400f));
            Read(simulation, into);
        }


        private static void Plumbed(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();

            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5);
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;
            PipeFitter.BuildRing(builder, cells, 5, sinks);
            builder.Place(Catalog.Reactor(), cells[1] + Vector3I.Down).Wasting(125000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 300f);
            simulation.StepExact(LabClock.Steps(400), Worlds.Shadow());

            Read(simulation, into);

            IList<CoolantLoop> loops = simulation.Solver.Loops;
            if (loops == null || loops.Count == 0) { into.Add(0f); into.Add(0f); return; }

            into.Add(loops[0].HottestSegment);
            into.Add(loops[0].ColdestSegment);
        }


        private static void Pumped(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), new Vector3I(0, 0, -1));
            builder.Place(Catalog.HeatPump(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(0, 0, 1));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 300f);

            HeatPumpDevice pump = simulation.HeatPumps.Count == 0 ? null : simulation.HeatPumps[0];
            if (pump == null) { into.Add(0f); into.Add(0f); into.Add(0f); return; }

            pump.Enabled = true;

            simulation.Solver.GetNodeAt(new Vector3I(0, 0, 1)).Temperature = 302f;

            simulation.StepExact(LabClock.Steps(20), Worlds.Shadow());
            into.Add(pump.LastLiftedWatts);
            into.Add(pump.LastPowerWatts);

            simulation.StepExact(LabClock.Steps(180), Worlds.Shadow());
            into.Add(pump.LastLiftedWatts);
            into.Add(pump.LastPowerWatts);
            Read(simulation, into);
        }


        private static void Sealed(Action<ThermalSettings> apply, List<float> into)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));
            builder.Remove(new Vector3I(0, 2, 2));
            builder.Place(Catalog.Reactor(), new Vector3I(0, 2, 2)).Wasting(120000f);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(apply), 293.15f);

            simulation.SetRoomPressure(new Vector3I(2, 2, 2), 1f);

            simulation.StepExact(LabClock.Steps(400), Worlds.Shadow());

            Read(simulation, into);

            IList<RoomAirNode> air = simulation.RoomAir;
            into.Add(air.Count == 0 ? 0f : air[0].Temperature);
            into.Add(air.Count == 0 ? 0f : air[0].ThermalMass);
        }


        private static void Suit(Action<ThermalSettings> apply, List<float> into)
        {

            ThermalSettings settings = Settings(apply);

            float interior = SuitThermal.ComfortKelvin;
            float damage = 0f;

            for (int i = 0; i < 400; i++)
            {
                SuitStepResult step = SuitThermal.Step(settings, interior, 900f, false, true, 1f);
                interior = step.InteriorKelvin;
                damage += step.Damage;

                if (i % 40 == 0)
                {
                    into.Add(interior);
                    into.Add(step.RegulatedWatts);
                    into.Add(damage);
                }
            }

            into.Add(interior);
            into.Add(damage);
            into.Add(SuitThermal.SurvivableKelvin(settings));
            into.Add(SuitThermal.SurvivableKelvin(settings, true));
        }


        private static object[] Levels(FieldInfo field, ThermalSettings shipped)
        {
            if (field.FieldType == typeof(bool))
            {
                return new object[] { !(bool)field.GetValue(shipped) };
            }

            if (field.FieldType == typeof(int))
            {
                if (Caps.Contains(field.Name)) return new object[] { 1, 0 };

                int value = (int)field.GetValue(shipped);
                if (value == 0) return new object[] { 1, 8 };
                return new object[] { Math.Max(1, value / 4), value * 4 };
            }

            if (field.Name == "VacuumTemperature") return new object[] { 1f, 250f };

            if (field.Name == "FrictionAtSpeedsAbove") return new object[] { 1000f };

            float number = (float)field.GetValue(shipped);
            if (number == 0f) return new object[] { 1f, 10f };
            return new object[] { number * 0.25f, number * 4f };
        }


        private static bool Same(List<float> a, List<float> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                float scale = Math.Max(Math.Abs(a[i]), Math.Abs(b[i]));
                float tolerance = scale < 1f ? 1e-4f : scale * 1e-5f;
                if (Math.Abs(a[i] - b[i]) > tolerance) return false;
            }

            return true;
        }

        [Fact]

        public void EverySettingReachesTheSimulation()
        {

            List<FieldInfo> fields = new List<FieldInfo>();

            foreach (FieldInfo field in typeof(ThermalSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type type = field.FieldType;
                if (type != typeof(bool) && type != typeof(int) && type != typeof(float)) continue;
                fields.Add(field);
            }

            Assert.True(fields.Count >= 30,
                "only " + fields.Count + " settings were found on ThermalSettings, so this test"
                + " would pass on a class that had lost most of them");


            List<float> shipped = Fingerprint(null);

            Assert.True(shipped.Count > 40,
                "the rigs produced only " + shipped.Count + " readings between them");


            List<string> inert = new List<string>();

            List<string> unexpectedlyLive = new List<string>();
            int judged = 0;

            foreach (FieldInfo field in fields)
            {
                bool moved = false;

                foreach (object level in Levels(field, new ThermalSettings()))
                {
                    FieldInfo captured = field;
                    object value = level;


                    List<float> readings = Fingerprint(s => captured.SetValue(s, value));
                    judged++;

                    if (Same(shipped, readings)) continue;
                    moved = true;
                    break;
                }

                bool exempt = Exempt.ContainsKey(field.Name);

                if (!moved && !exempt) inert.Add(field.Name);
                if (moved && exempt) unexpectedlyLive.Add(field.Name + " — " + Exempt[field.Name]);

                output.WriteLine("{0,-32} {1}", field.Name,
                    moved ? "reaches" : (exempt ? "inert, and named as inert" : "REACHES NOTHING"));
            }

            output.WriteLine("{0} settings, {1} runs of {2} rigs", fields.Count, judged, 14);

            Assert.True(inert.Count == 0,
                "settings that changed nothing any rig here reads:\n  "
                + string.Join("\n  ", inert)
                + "\nEither the setting is wired to nothing — which is the defect this exists for —"
                + " or no rig here can see it, in which case add a rig or name it in Exempt with"
                + " the reason.");

            Assert.True(unexpectedlyLive.Count == 0,
                "settings named as unreachable that a rig here now moves:\n  "
                + string.Join("\n  ", unexpectedlyLive)
                + "\nTake them out of Exempt.");
        }
    }
}
