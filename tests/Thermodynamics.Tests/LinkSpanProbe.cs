using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The conduction loop gathers two temperatures and scatters two watts per link, and what
    /// that costs depends entirely on how far apart the two nodes are numbered.**
    ///
    /// <para>
    /// It is the loop a step spends the most time in, so "renumber the nodes into a spatially
    /// coherent order" is the obvious next idea — and it is the wrong one here, because the
    /// numbering already is coherent. Measured on the census hull at 126,731 blocks: the mean
    /// index span of a link is **244**, two thirds of links span fewer than 64 nodes and
    /// **97.8 %** span fewer than 1,024 — which is four kilobytes of a float row, so the gather
    /// lands in L1 nearly every time. Reordering would buy the last two per cent of a loop that is
    /// already at 1.8 ns a link. See performance.md, Pass 5, Iteration 2.
    /// </para>
    ///
    /// <para>
    /// This is kept as a check rather than a note because the property is not guaranteed by
    /// anything: nodes are numbered in the order blocks are added, and a change to how a grid is
    /// built or loaded could scatter them. If that happens, conduction gets slower for a reason no
    /// timing would explain, and this says so first.
    /// </para>
    /// </summary>
    public class LinkSpanProbe
    {
        private readonly ITestOutputHelper output;


        public LinkSpanProbe(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void TheNodesAreNumberedLocallyEnoughForConductionToGatherFromCache()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 20000));
            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);

            GridModel grid = simulation.Grid;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;


            List<BlockInstance> scratch = new List<BlockInstance>();
            long total = 0;
            long links = 0;
            long within1024 = 0;
            long biggest = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                scratch.Clear();
                grid.GetNeighbours(nodes[i].Block, scratch);

                for (int n = 0; n < scratch.Count; n++)
                {
                    ThermalNode b = simulation.Solver.GetNode(scratch[n]);
                    if (b == null || b.Index <= i) continue;

                    long span = b.Index - i;
                    total += span;
                    links++;
                    if (span < 1024) within1024++;
                    if (span > biggest) biggest = span;
                }
            }

            Assert.True(links > 10000, "only " + links + " links, so this measured little");

            double mean = total / (double)links;
            double local = 100.0 * within1024 / links;

            output.WriteLine(nodes.Count.ToString("n0") + " nodes, " + links.ToString("n0") + " links: "
                + "mean index span " + mean.ToString("n1") + ", " + local.ToString("n1")
                + " % within 1,024, largest " + biggest.ToString("n0"));

            Assert.True(local > 95.0,
                "only " + local.ToString("n1") + " % of links span fewer than 1,024 node indices, so"
                + " the conduction loop's gathers have stopped landing in cache — the node numbering"
                + " has been scattered by something");
        }
    }
}
