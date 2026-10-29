using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Two same-position block instances compare per-pass adapter lookups with the public
    /// linear query across duplicate, reordered and replaced mutable device lists.
    /// </summary>
    public class HeatPumpDeviceLookupTests
    {
        [Fact]
        public void RepeatedPassesMatchTheMutablePublicQueryWithoutKeepingRemovedDevices()
        {
            var simulation = new ThermalSimulation(new ThermalSettings(), new GridModel(2.5f));
            var model = BlockModel.Solid("pump", Vector3I.One, 10f,
                new BlockThermalProperties { SpecificHeat = 100f });
            var first = new ValueEqualBlock(model);
            var replacement = new ValueEqualBlock(model);
            var firstDevice = new HeatPumpDevice { Block = first };
            var duplicate = new HeatPumpDevice { Block = first };
            var replacementDevice = new HeatPumpDevice { Block = replacement };
            IList<HeatPumpDevice> devices = simulation.HeatPumps;
            var lookup = new Dictionary<BlockInstance, HeatPumpDevice>(HeatPumpDeviceLookup.Comparer);
            devices.Add(firstDevice);
            devices.Add(duplicate);
            devices.Add(replacementDevice);
            devices.Add(new HeatPumpDevice());

            AssertPass(simulation, lookup, first, replacement);
            Assert.Same(firstDevice, lookup[first]);
            Assert.Same(replacementDevice, lookup[replacement]);
            devices.RemoveAt(0);
            AssertPass(simulation, lookup, first, replacement);
            Assert.Same(duplicate, lookup[first]);

            // A publicly mutable device can change its identity between passes too.
            duplicate.Block = replacement;
            AssertPass(simulation, lookup, first, replacement);
            Assert.False(lookup.ContainsKey(first));
            Assert.Same(duplicate, lookup[replacement]);
            devices.Clear();
            devices.Add(firstDevice);
            AssertPass(simulation, lookup, first, replacement);
            Assert.False(lookup.ContainsKey(replacement));
            devices.Clear();
            AssertPass(simulation, lookup, first, replacement);
            Assert.Empty(lookup);
        }

        private sealed class ValueEqualBlock : BlockInstance
        {
            internal ValueEqualBlock(BlockModel model)
                : base(model, Vector3I.Zero, BlockOrientation.Identity) { }

            public override bool Equals(object other) { return other is ValueEqualBlock; }
            public override int GetHashCode() { return 0; }
        }

        private static void AssertPass(ThermalSimulation simulation,
            Dictionary<BlockInstance, HeatPumpDevice> lookup, BlockInstance first, BlockInstance second)
        {
            HeatPumpDeviceLookup.Fill(simulation.HeatPumps, lookup);
            HeatPumpDevice found;
            lookup.TryGetValue(first, out found);
            Assert.Same(simulation.GetHeatPump(first), found);
            lookup.TryGetValue(second, out found);
            Assert.Same(simulation.GetHeatPump(second), found);
        }
    }
}
