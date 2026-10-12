using Thermodynamics.Fixtures;
using System;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class SoloBlockRig
    {
        public enum Power
        {
            Produced,

            Consumed,
        }

        public const string LargeArmour = "LargeBlockArmorBlock";

        public const string SmallArmour = "SmallBlockArmorBlock";

        public const float Seconds = 14400f;

        public const float StepSeconds = 1200f;


        public static float Settled(Vanilla.Block block, BlockThermalProperties thermal,
            float watts, Power power, bool skinned)
        {
            if (block == null) throw new ArgumentNullException("block");

            GridBuilder builder = block.Large ? GridBuilder.Large() : GridBuilder.Small();
            builder.Place(BlockModel.Solid(block.Subtype, block.Size, block.Mass, thermal), Vector3I.Zero);

            if (power == Power.Produced) builder.Producing(watts);
            else builder.Consuming(watts);

            BlockInstance instance = builder.Last;

            if (skinned)
            {
                Vanilla.Block plate = Vanilla.Find(block.Large ? LargeArmour : SmallArmour);
                BlockModel armour = BlockModel.Solid(plate.Subtype, Vector3I.One, plate.Mass,
                    Catalog.DefaultThermal());
                builder.Shell(armour, -Vector3I.One, block.Size + Vector3I.One);
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("subject", instance);
            runner.Run(Seconds, StepSeconds);

            float value;
            return runner.Final.Tracked.TryGetValue("subject", out value) ? value : 0f;
        }


        public static BlockThermalProperties Clone(BlockThermalProperties source)
        {
            return source.Clone();
        }
    }
}
