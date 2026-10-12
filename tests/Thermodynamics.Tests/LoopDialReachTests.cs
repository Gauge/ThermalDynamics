using Thermodynamics.Fixtures;
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
    /// **Every number describing the coolant reaches the coolant.**
    ///
    /// <para>
    /// <see cref="DialReachTests"/> makes this check for the eighteen dials `KnobLab` sweeps, and
    /// the loop's own definition is not among them: <see cref="LoopThermalProperties"/> is parsed
    /// from `ThermalLoopProperties`, carried through <c>Settings</c>, listed in the in-game menu and
    /// clamped, and until this class was written nothing asked whether the solver ever read it.
    /// One field did not. `StagnantTransferFraction` was authored, documented, exposed to players on
    /// a slider and copied into the running properties, and no line of the simulation multiplied
    /// anything by it — a dial that looked like a decision and was a no-op.
    /// </para>
    ///
    /// <para>
    /// **So the check is by reflection rather than by list**, because a list is what failed. A field
    /// added to <see cref="LoopThermalProperties"/> tomorrow is checked tomorrow, without anyone
    /// remembering to add it here. That is the whole point: this repository's recurring defect is
    /// something built, documented and reached by nothing, and the only checks that catch it are the
    /// ones that enumerate rather than recite.
    /// </para>
    ///
    /// <para>
    /// **It is a reach test.** It asserts a field changes an outcome and says nothing about the
    /// direction or the size of the change, which is what lets it survive every retune untouched.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class LoopDialReachTests
    {
        private readonly ITestOutputHelper output;


        public LoopDialReachTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static HistoricalThermalSettings Isolated()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static void Sample(LoopThermalProperties properties, float gridSize,
            bool pumping, bool vented, List<float> into)
        {
            GridBuilder builder = gridSize > 1f ? GridBuilder.Large() : GridBuilder.Small();

            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 6, 5);
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            PipeFitter.BuildRing(builder, cells, 12, sinks);
            builder.Place(Catalog.Reactor(), cells[2] + Vector3I.Down).Wasting(125000f);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            simulation.LoopProperties = properties;
            simulation.RebuildAll();

            IList<CoolantLoop> loops = simulation.Solver.Loops;
            if (loops == null || loops.Count == 0) { into.Add(0f); return; }

            CoolantLoop loop = loops[0];

            for (int i = 0; i < loop.Pumps.Count; i++) loop.Pumps[i].Enabled = pumping;
            loop.RefreshFlow();

            if (vented) loop.Vent(293.15f);

            simulation.StepExact(LabClock.Steps(40), Worlds.Shadow());
            into.Add(PumpDraw(loop));

            simulation.StepExact(LabClock.Steps(560), Worlds.Shadow());

            into.Add(loop.HottestSegment);
            into.Add(loop.ColdestSegment);
            into.Add(loop.FillFraction);
            into.Add(loop.HeldKilograms);
            into.Add(simulation.Solver.RequiredSubsteps(0.25f));

            ThermalNode source = simulation.Solver.GetNode(builder.Last);
            into.Add(source != null ? source.Temperature : 0f);

            into.Add(PumpDraw(loop));
        }


        private static float PumpDraw(CoolantLoop loop)
        {
            float drawn = 0f;
            for (int i = 0; i < loop.Pumps.Count; i++)
            {
                if (loop.Pumps[i].Block != null) drawn += loop.Pumps[i].Block.PowerConsumedWatts;
            }
            return drawn;
        }


        private static List<float> Fingerprint(LoopThermalProperties properties)
        {

            List<float> readings = new List<float>();

            Sample(properties, 2.5f, true, false, readings);
            Sample(properties, 2.5f, false, false, readings);
            Sample(properties, 2.5f, true, true, readings);
            Sample(properties, 0.5f, true, false, readings);

            return readings;
        }


        private static float[] Levels(string name, float shipped)
        {
            if (name == "StagnantTransferFraction") return new float[] { 0f, 0.25f };
            if (shipped == 0f) return new float[] { 1f, 10f };
            return new float[] { shipped * 0.25f, shipped * 4f };
        }

        [Fact]

        public void EveryCoolantDialReachesTheSimulation()
        {

            FieldInfo[] fields = typeof(LoopThermalProperties)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);


            List<FieldInfo> floats = new List<FieldInfo>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(float)) floats.Add(fields[i]);
            }

            Assert.True(floats.Count >= 8,
                "only " + floats.Count + " coolant dials were found, so this test would pass on a"
                + " definition that had lost most of them");


            List<float> shipped = Fingerprint(LoopThermalProperties.Default());

            List<string> inert = new List<string>();

            foreach (FieldInfo field in floats)
            {
                float baseline = (float)field.GetValue(LoopThermalProperties.Default());
                float widest = 0f;

                foreach (float level in Levels(field.Name, baseline))
                {
                    if (level == baseline) continue;

                    LoopThermalProperties properties = LoopThermalProperties.Default();
                    field.SetValue(properties, level);


                    List<float> moved = Fingerprint(properties.Clamp());

                    for (int i = 0; i < moved.Count && i < shipped.Count; i++)
                    {
                        float difference = Math.Abs(moved[i] - shipped[i]);

                        float scale = Math.Max(1f, Math.Abs(shipped[i]));
                        float relative = difference / scale;

                        if (relative > widest) widest = relative;
                    }
                }

                output.WriteLine("{0,-28} widest relative move {1:n5}", field.Name, widest);

                if (widest < 0.001f) inert.Add(field.Name + " changed nothing the solver computed");
            }

            inert.Sort(StringComparer.Ordinal);
            Assert.True(inert.Count == 0,
                "coolant dials the simulation never reads:\n  " + string.Join("\n  ", inert.ToArray()));
        }
    }
}
