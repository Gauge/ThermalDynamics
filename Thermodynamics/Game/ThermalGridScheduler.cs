using System;
using System.Collections.Generic;
using Sandbox.ModAPI;

namespace Thermodynamics
{
    public static class ThermalGridScheduler
    {
        public const float FrameSeconds = 1f / 60f;

        public static void Tick()
        {
            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;

            if (Settings.Instance != null && Settings.Instance.ParallelGrids)
            {
                TickInParallel(grids);
                return;
            }

            for (int i = grids.Count - 1; i >= 0; i--)
            {
                ThermalGrid grid = grids[i];
                if (grid == null) continue;

                grid.Tick(FrameSeconds);
            }
        }

        private static void TickInParallel(IList<ThermalGrid> grids)
        {
            ready.Clear();

            try
            {
                for (int i = grids.Count - 1; i >= 0; i--)
                {
                    ThermalGrid grid = grids[i];
                    if (grid == null) continue;

                    if (grid.PrepareTick(FrameSeconds)) ready.Add(grid);
                }

                if (ready.Count == 0) return;

                if (ready.Count == 1)
                {
                    ready[0].SolveTick();
                }
                else if (MyAPIGateway.Parallel != null)
                {
                    MyAPIGateway.Parallel.ForEach(ready, Solve);
                }
                else
                {
                    for (int i = 0; i < ready.Count; i++) ready[i].SolveTick();
                }

                for (int i = 0; i < ready.Count; i++) ready[i].PublishTick();
            }
            finally
            {
                // Do not retain whole grid simulations after parallel mode is disabled or a
                // world closes. Also release references if preparation or publication throws.
                ready.Clear();
            }
        }

        private static readonly Action<ThermalGrid> Solve = grid => grid.SolveTick();

        private static readonly List<ThermalGrid> ready = new List<ThermalGrid>();
    }
}
