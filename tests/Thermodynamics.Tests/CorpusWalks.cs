using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{

    /// <summary>Every block in every blueprint is placed or counted as unknown.</summary>

    /// <summary>Every block in every blueprint is placed or counted as unknown.</summary>
    public class BlockAccountingWalk
    {
        [Fact]

        public void EveryBlockInABlueprintIsEitherPlacedOrCountedAsUnknown()
        {
            LabInvariantTests.EveryBlockInABlueprintIsEitherPlacedOrCountedAsUnknown();
        }
    }

    /// <summary>The same ship in the same state twice gives the same answer.</summary>

    /// <summary>The same ship in the same state twice gives the same answer.</summary>
    public class DeterminismWalk
    {
        private const int Sample = 200;

        [Fact]

        public void RunningTheSameThingTwiceGivesTheSameAnswer()
        {
            List<Blueprints.Ship> ships = CorpusFixture.Spread(Sample);
            if (ships.Count == 0) return;


            List<string> drifted = new List<string>();
            foreach (List<string> report in LabRun.Map(ships, LabInvariantTests.Compare, LabMode.Parallel))
            {
                drifted.AddRange(report);
            }

            Assert.Empty(drifted);
        }
    }
}
