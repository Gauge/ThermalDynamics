using System;

namespace Thermodynamics.Core
{
    /// <summary>
    /// Manages simulation scheduling and work budget allocation.
    /// Calculates appropriate work budgets for different simulation phases
    /// based on grid size or node count to maintain consistent performance.
    /// </summary>
    public class SimulationScheduler
    {
        /// <summary>
        /// Creates a new SimulationScheduler.
        /// </summary>
        /// <param name="settings">Thermal settings (currently unused, kept for API compatibility).</param>
        public SimulationScheduler(ThermalSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
        }

        /// <summary>
        /// Total number of simulation steps that have been run.
        /// Monotonic counter starting from 0.
        /// </summary>
        public long StepsRun { get; private set; }


        /// <summary>
        /// Calculates the work budget for room mapping operations.
        /// Budget scales with grid cell volume but has minimum and maximum caps.
        /// </summary>
        /// <param name="gridCellVolume">Total volume of the grid in cells (width * height * depth).</param>
        /// <returns>Budget of cells to process per step.</returns>
        /// <remarks>
        /// Budget calculation:
        ///   budget = gridCellVolume / 60
        ///   min = 64 cells
        ///   max = 4096 cells
        ///
        /// This provides approximately 60 work units per simulation step for
        /// room mapping, scaled by grid size but capped for performance.
        /// </remarks>
        public static int RoomMappingBudget(int gridCellVolume)
        {
            int budget = gridCellVolume / 60;
            if (budget < 64) budget = 64;
            if (budget > 4096) budget = 4096;
            return budget;
        }


        /// <summary>
        /// Calculates the work budget for exposure calculations.
        /// Budget scales with node count but has minimum and maximum caps.
        /// </summary>
        /// <param name="nodeCount">Number of thermal nodes in the simulation.</param>
        /// <returns>Budget of nodes to process per step.</returns>
        /// <remarks>
        /// Budget calculation:
        ///   budget = nodeCount / 40
        ///   min = 256 nodes
        ///   max = 4096 nodes
        ///
        /// This provides approximately 40 work units per simulation step for
        /// exposure calculations, scaled by simulation complexity but capped.
        /// </remarks>
        public static int ExposureBudget(int nodeCount)
        {
            int budget = nodeCount / 40;
            if (budget < 256) budget = 256;
            if (budget > 4096) budget = 4096;
            return budget;
        }


        /// <summary>
        /// Calculates the number of items to process in a single sweep slice.
        /// Used for incremental processing of large workloads across multiple steps.
        /// </summary>
        /// <param name="count">Total number of items to process.</param>
        /// <param name="steps">Number of steps to spread the work across.</param>
        /// <param name="interval">Work interval in steps.</param>
        /// <param name="cap">Maximum items to process per interval.</param>
        /// <returns>Number of items to process in this interval.</returns>
        /// <remarks>
        /// Calculation:
        ///   share = (count * steps) / interval
        ///   share = max(1, min(share, cap, count))
        ///
        /// Ensures at least 1 item is processed, and never more than the cap
        /// or total count.
        /// </remarks>
        public static int SweepSlice(int count, int steps, int interval, int cap)
        {
            if (count <= 0 || steps <= 0 || cap <= 0) return 0;
            if (interval < 1) interval = 1;

            long share = ((long)count * steps) / interval;

            if (share < 1) share = 1;
            if (share > cap) share = cap;
            if (share > count) share = count;

            return (int)share;
        }


        /// <summary>
        /// Increments the step counter by one.
        /// Called at the end of each simulation step to track progress.
        /// </summary>
        public void CountStep()
        {
            StepsRun++;
        }


        /// <summary>
        /// Resets the step counter to zero.
        /// Called when restarting a simulation.
        /// </summary>
        public void Reset()
        {
            StepsRun = 0;
        }
    }
}
