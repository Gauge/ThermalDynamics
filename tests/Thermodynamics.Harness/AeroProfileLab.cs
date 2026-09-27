using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>
    /// Measures profile construction, first evaluation, settled evaluation and centre-change cost
    /// separately on synthetic cubes. See aerodynamics.md, Profile construction and tick cost.
    /// </summary>
    public static class AeroProfileLab
    {
        public const int EvaluationsPerRepeat = 1000;

        /// <summary>One geometry and resolution, repeated until each minimum reproduces or the cap is reached.</summary>
        public static List<StageLab.Row> Measure(int side, int directions, int minimumRepeats = 7, int maximumRepeats = 40)
        {
            if (side < 2 || (directions != AeroProfile.CoarseDirections && directions != AeroProfile.FineDirections)
                || minimumRepeats < 2 || maximumRepeats < minimumRepeats)
                throw new ArgumentException("Use a cube side of at least two cells, 26 or 98 directions, and at least two repeats.");

            GridModel grid = AeroCalibrationLab.Box(side, side, side);
            grid.Occupancy();
            List<StageLab.Row> rows = new List<StageLab.Row>();
            string[] stages = { "build", "first", "settled", "shifted" };
            foreach (string stage in stages)
                rows.Add(new StageLab.Row
                {
                    Stage = stage + "-" + directions,
                    Blocks = grid.BlockCount,
                    BestMs = double.MaxValue,
                    Work = stage == "settled" ? EvaluationsPerRepeat : 1,
                    WorkUnit = stage == "build" ? "profiles" : "evaluations",
                    Note = "cube; warm occupancy; " + directions + " directions"
                });

            Evaluate(AeroProfile.Build(grid, directions));
            Stopwatch clock = new Stopwatch();
            for (int repeat = 0; repeat < maximumRepeats; repeat++)
            {
                StageLab.Settle();
                long before = StageLab.Allocated();
                clock.Restart();
                AeroProfile profile = AeroProfile.Build(grid, directions);
                clock.Stop();
                Record(rows[0], clock.Elapsed.TotalMilliseconds, StageLab.Allocated() - before);

                before = StageLab.Allocated();
                clock.Restart();
                Evaluate(profile);
                clock.Stop();
                Record(rows[1], clock.Elapsed.TotalMilliseconds, StageLab.Allocated() - before);

                before = StageLab.Allocated();
                clock.Restart();
                for (int i = 0; i < EvaluationsPerRepeat; i++) Evaluate(profile);
                clock.Stop();
                Record(rows[2], clock.Elapsed.TotalMilliseconds, StageLab.Allocated() - before);

                before = StageLab.Allocated();
                clock.Restart();
                profile.SetCentreOfMass(profile.CentreOfMass + new Vector3(0.125f, -0.25f, 0.5f));
                Evaluate(profile);
                clock.Stop();
                Record(rows[3], clock.Elapsed.TotalMilliseconds, StageLab.Allocated() - before);

                bool confirmed = repeat + 1 >= minimumRepeats;
                foreach (StageLab.Row row in rows) confirmed &= row.ConfirmedBest >= StageLab.ConfirmingRepeats;
                if (confirmed) break;
            }
            foreach (StageLab.Row row in rows)
            {
                row.Stop = row.ConfirmedBest >= StageLab.ConfirmingRepeats ? StageLab.Row.Confirmed : StageLab.Row.Capped;
                StageLab.Summarise(row);
            }
            return rows;
        }

        /// <summary>Each repeat records its slowest construction or centre-update call, without pooling cheap steps.</summary>
        public static StageLab.Row MeasureSlices(int side, int directions, int budget = 256,
            int minimumRepeats = 7, int maximumRepeats = 40, bool centreUpdate = false)
        {
            if (side < 2 || (directions != AeroProfile.CoarseDirections && directions != AeroProfile.FineDirections)
                || budget < 1 || minimumRepeats < 2 || maximumRepeats < minimumRepeats)
                throw new ArgumentException("Use a nonempty cube, 26 or 98 directions, a positive budget and at least two repeats.");
            GridModel grid = AeroCalibrationLab.Box(side, side, side);
            AeroProfile warm = AeroProfile.Build(grid, directions);
            Evaluate(warm);
            if (centreUpdate)
            {
                Vector3 target = warm.CentreOfMass + new Vector3(0.125f, -0.25f, 0.5f);
                int used;
                while (!warm.PrepareCentreOfMass(target, budget, out used)) { }
                Evaluate(warm);
            }
            StageLab.Row row = new StageLab.Row
            {
                Stage = (centreUpdate ? "centre-slice-peak-" : "slice-peak-") + directions,
                Blocks = grid.BlockCount, BestMs = double.MaxValue,
                Work = 1, WorkUnit = "maximum-slice"
            };
            Stopwatch clock = new Stopwatch();
            long expectedWork = 0;
            int expectedSlices = 0;
            for (int repeat = 0; repeat < maximumRepeats; repeat++)
            {
                StageLab.Settle();
                AeroProfile profile = centreUpdate ? AeroProfile.Build(grid, directions) : null;
                AeroProfileBuilder job = centreUpdate ? null : new AeroProfileBuilder(grid, directions, 0);
                Vector3 target = centreUpdate ? profile.CentreOfMass + new Vector3(0.125f, -0.25f, 0.5f) : Vector3.Zero;
                double peak = 0d;
                long peakAllocated = 0;
                int slices = 0;
                long work = 0;
                bool complete = false;
                while (!complete)
                {
                    long before = StageLab.Allocated();
                    clock.Restart();
                    int used;
                    if (centreUpdate) complete = profile.PrepareCentreOfMass(target, budget, out used);
                    else used = job.Advance(budget, 0);
                    clock.Stop();
                    long allocated = StageLab.Allocated() - before;
                    if (!centreUpdate) complete = job.Complete;
                    work += used;
                    if (used <= 0 || used > budget) throw new InvalidOperationException("Invalid slice work count.");
                    peak = Math.Max(peak, clock.Elapsed.TotalMilliseconds);
                    peakAllocated = Math.Max(peakAllocated, allocated);
                    slices++;
                }
                if (!centreUpdate && !job.TryGetResult(0, out profile)) throw new InvalidOperationException("No complete profile.");
                Evaluate(profile);
                if (repeat == 0) { expectedWork = work; expectedSlices = slices; }
                if (work != expectedWork || slices != expectedSlices)
                    throw new InvalidOperationException("The repeated slices did different work.");
                Record(row, peak, peakAllocated);
                if (repeat + 1 >= minimumRepeats && row.ConfirmedBest >= StageLab.ConfirmingRepeats) break;
            }
            row.Note = "cube; budget=" + budget + "; work=" + expectedWork + "; slices=" + expectedSlices
                + (centreUpdate ? "; maximum centre-preparation time per update; budget counts exposed faces"
                    : "; maximum Advance time per build; constructor excluded");
            row.Stop = row.ConfirmedBest >= StageLab.ConfirmingRepeats ? StageLab.Row.Confirmed : StageLab.Row.Capped;
            StageLab.Summarise(row);
            return row;
        }

        private static void Record(StageLab.Row row, double milliseconds, long allocated)
        {
            StageLab.Take(row, milliseconds);
            row.AllocatedBytes = Math.Max(row.AllocatedBytes, allocated);
        }

        private static void Evaluate(AeroProfile profile)
        {
            Vector3 force, torque, damping, centre;
            profile.Evaluate(new Vector3(-12f, 3f, -50f), 1.225f, new Vector3(0.1f, -0.2f, 0.3f),
                AeroCoefficients.Fitted, AeroFlightLab.Viscosity, out force, out torque, out damping, out centre);
            if (!(force.LengthSquared() > 0f) || float.IsInfinity(force.LengthSquared())
                || !(damping.LengthSquared() > 0f) || float.IsInfinity(damping.LengthSquared()))
                throw new InvalidOperationException("The cost rig did not exercise finite drag and rotational damping.");
        }

        /// <summary>The synthetic scale ladder, including machine, binary identity, sample shape and allocations.</summary>
        public static string Report()
        {
            List<StageLab.Row> rows = new List<StageLab.Row>();
            foreach (int side in new[] { 4, 8, 16 })
                foreach (int directions in new[] { AeroProfile.CoarseDirections, AeroProfile.FineDirections })
                {
                    rows.AddRange(Measure(side, directions));
                    rows.Add(MeasureSlices(side, directions));
                    rows.Add(MeasureSlices(side, directions, centreUpdate: true));
                }
            StringBuilder text = new StringBuilder();
            text.AppendLine("Aerodynamic profile cost; synthetic solid cubes, not a corpus or live-game measurement");
            text.AppendLine("Host: " + StageLab.Host + "; UTC: " + StageLab.TakenUtc);
            text.AppendLine("Core binary MVID: " + typeof(AeroProfile).Assembly.ManifestModule.ModuleVersionId);
            text.AppendLine("Runtime: " + Environment.Version + "; DOTNET_TieredCompilation: "
                + (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime default"));
            text.AppendLine("Times are per row's work count; settled rows contain "
                + EvaluationsPerRepeat.ToString(CultureInfo.InvariantCulture) + " evaluations. Allocations are maximum bytes per repeat.");
            text.AppendLine("Spread is (slowest - fastest)/fastest; first and shifted are single-call samples and may be noisy.");
            text.AppendLine("Slice-peak samples are the maximum Advance time within each complete build at 256 work units per call.");
            text.AppendLine("Their allocation count is the maximum per Advance call; constructor allocations are excluded. These are observed peaks, not deadlines.");
            text.AppendLine("Centre-slice-peak samples are the maximum preparation call in one centre update at 256 exposed faces per call; profile construction is excluded. The preparation path is warmed before measurement.");
            text.AppendLine(StageLab.Table(rows));
            text.AppendLine(StageLab.Csv(rows));
            return text.ToString();
        }
    }
}
