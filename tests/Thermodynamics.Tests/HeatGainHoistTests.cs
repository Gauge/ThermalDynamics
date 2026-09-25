using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The heat a grid puts into itself cannot change between the substeps of one step**, so it
    /// is summed once and read twenty-three times rather than summed twenty-four.
    ///
    /// <para>
    /// Waste heat, solar gain and aerodynamic friction are read out of `nodeSourceRow`, which is
    /// filled on a step's first substep and read by the rest. Summing it every substep computed the
    /// same float over and over — and not for free: a running float sum is a dependency the loop
    /// carries from one node to the next, and the two accumulators in that loop measured **35 %**
    /// of the environment stage between them (performance.md, Pass 5, Iteration 3).
    /// </para>
    ///
    /// <para>
    /// The sum is over the same values in the same order, so the published figure is unchanged **to
    /// the bit** — which is what these check, by running the same grid both ways (`D3`, `D8`).
    /// </para>
    /// </summary>
    public class HeatGainHoistTests
    {

        private static ThermalSimulation Hull(bool hoist)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 3000));

            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
            simulation.Solver.HoistHeatGainTotal = hoist;
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);
            return simulation;
        }

        [Fact]

        public void TheHoistedFigureIsTheSummedFigureToTheBit()
        {

            ThermalSimulation summed = Hull(false);

            ThermalSimulation hoisted = Hull(true);

            EnvironmentState state = EnvironmentSolver.Solve(
                summed.Settings, summed.Planet, Worlds.Flight(1f, 300f));
            float step = summed.Settings.StepSeconds;

            int judged = 0;
            for (int i = 0; i < 8; i++)
            {
                summed.Solver.Step(step, state);
                hoisted.Solver.Step(step, state);

                Assert.True(summed.Solver.LastHeatGainWatts > 0f,
                    "the hull put no heat into itself on step " + i + ", so nothing is compared");

                Assert.True(Bits(summed.Solver.LastHeatGainWatts) == Bits(hoisted.Solver.LastHeatGainWatts),
                    "step " + i + ": " + summed.Solver.LastHeatGainWatts.ToString("R")
                    + " summed every substep and " + hoisted.Solver.LastHeatGainWatts.ToString("R")
                    + " summed once");

                Assert.Equal(Bits(summed.Solver.LastEnvironmentWatts), Bits(hoisted.Solver.LastEnvironmentWatts));
                judged++;
            }

            Assert.Equal(8, judged);

            IList<ThermalNode> a = summed.Solver.Nodes;
            IList<ThermalNode> b = hoisted.Solver.Nodes;
            Assert.True(a.Count > 1000, "only " + a.Count + " nodes");

            for (int i = 0; i < a.Count; i++)
            {
                Assert.True(Bits(a[i].Temperature) == Bits(b[i].Temperature),
                    "node " + i + " is " + a[i].Temperature.ToString("R") + " one way and "
                    + b[i].Temperature.ToString("R") + " the other");
            }
        }

        [Fact]

        public void ANewStepDoesNotCarryTheLastStepsTotal()
        {

            ThermalSimulation simulation = Hull(true);
            float step = simulation.Settings.StepSeconds;

            EnvironmentState cold = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 200f));
            EnvironmentState hot = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 400f));

            simulation.Solver.Step(step, cold);
            float atCold = simulation.Solver.LastHeatGainWatts;

            simulation.Solver.Step(step, hot);
            float atHot = simulation.Solver.LastHeatGainWatts;

            Assert.True(atCold > 0f && atHot > 0f, "one of the two steps gained no heat");
            Assert.True(atHot != atCold,
                "the grid reported the same " + atCold.ToString("R")
                + " W of self-heating at 200 m/s and at 400 m/s, so a step is carrying the last"
                + " step's total rather than taking its own");
        }


        private static int Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
