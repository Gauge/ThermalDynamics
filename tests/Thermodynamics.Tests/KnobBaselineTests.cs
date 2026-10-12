using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Every world knob's `Shipped` value is the value that actually ships.**
    ///
    /// <para>
    /// The knob sweep prints each level against `Shipped` so a curve can be read from where the mod
    /// sits, and nothing checked that the two agreed. `C24` moved `HeatTimeScale` from 225 to 90 on
    /// 2026-08-24 and the knob kept 225 — so the column comparing every level to *shipped* compared
    /// it to a level nobody runs, and the ladder of doublings around 225 put the shipped value
    /// between two of its own cells, which means the sweep contained no answer for the world as it
    /// is.
    /// </para>
    ///
    /// <para>
    /// **The check needs no table of expected values**, which is what makes it survive the next
    /// retune: a knob whose `Shipped` is the default is one that changes nothing when applied to
    /// default settings. Applying it and comparing every field is the whole test (`D3` — where one
    /// thing exists twice, a test compares the two).
    /// </para>
    /// </summary>
    public class KnobBaselineTests
    {
        private readonly ITestOutputHelper output;


        public KnobBaselineTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void EveryWorldKnobsShippedValueIsTheShippedValue()
        {
            List<KnobLab.Knob> knobs = KnobLab.Knobs();
            Assert.True(knobs.Count > 5, "only " + knobs.Count + " knobs, so this checked nothing");


            List<string> wrong = new List<string>();
            int checkedKnobs = 0;

            foreach (KnobLab.Knob knob in knobs)
            {
                if (knob.World == null) continue;

                checkedKnobs++;


                HistoricalThermalSettings applied = new HistoricalThermalSettings();
                knob.World(applied, knob.Shipped);


                HistoricalThermalSettings untouched = new HistoricalThermalSettings();

                foreach (string field in Differences(untouched, applied))
                {
                    wrong.Add(knob.Name + ": applying its shipped " + knob.Shipped
                        + " moves " + field);
                }
            }

            output.WriteLine("{0} of {1} knobs act on the world", checkedKnobs, knobs.Count);

            Assert.True(checkedKnobs > 3,
                "only " + checkedKnobs + " world knobs were found, so this test is reading the "
                + "wrong thing and would pass whatever a knob claimed");

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,
                "a knob's shipped value is not what ships, so its sweep is read against a level "
                + "nobody runs:\n  " + string.Join("\n  ", wrong.ToArray()));
        }


        private static List<string> Differences(HistoricalThermalSettings before, HistoricalThermalSettings after)
        {

            List<string> moved = new List<string>();

            foreach (FieldInfo field in typeof(HistoricalThermalSettings).GetFields(
                         BindingFlags.Public | BindingFlags.Instance))
            {
                object a = field.GetValue(before);
                object b = field.GetValue(after);

                if (Equals(a, b)) continue;

                moved.Add(field.Name + " from " + a + " to " + b);
            }

            return moved;
        }
    }
}
