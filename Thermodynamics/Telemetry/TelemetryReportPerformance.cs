using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thermodynamics.Core;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static partial class TelemetryReport
    {
        private static int costRecords;
        private static long costCalls;
        private static double costMilliseconds;
        private static long costSteps;


        private static void WritePerformance(StringBuilder sb)
        {
            Section(sb, "Cost");


            TimingStat simulation = new TimingStat("  of which grid simulation");

            TimingStat topology = new TimingStat("    of which topology rebuild");

            TimingStat mapping = new TimingStat("    of which room mapping");

            TimingStat exposure = new TimingStat("    of which exposure refresh");

            TimingStat sample = new TimingStat("    of which environment sample");

            TimingStat solar = new TimingStat("      of which solar occlusion");

            TimingStat solver = new TimingStat("    of which solver");

            TimingStat after = new TimingStat("    of which after step");

            TimingStat pressure = new TimingStat("      of which room pressure");

            TimingStat damage = new TimingStat("      of which overheat damage");

            TimingStat massSweep = new TimingStat("      of which mass sweep");

            TimingStat lostRooms = new TimingStat("      of which lost-room scan");

            TimingStat health = new TimingStat("      of which hottest and health");

            TimingStat sampling = new TimingStat("      of which telemetry sampling");

            TimingStat save = new TimingStat("save");

            TimingStat load = new TimingStat("load");

            TimingStat build = new TimingStat("build");

            TimingStat blockEvents = new TimingStat("block events");

            costRecords = 0;
            costCalls = 0;
            costMilliseconds = 0;
            costSteps = 0;

            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                GridTelemetry g = Telemetry.Grids[i];

                costRecords++;
                costCalls += g.SimulationTime.Calls;
                costMilliseconds += g.SimulationTime.TotalMilliseconds;
                costSteps += g.SimulationSteps;

                simulation.Merge(g.SimulationTime);
                topology.Merge(g.Profiler.Topology);
                mapping.Merge(g.Profiler.RoomMapping);
                exposure.Merge(g.Profiler.Exposure);
                pressure.Merge(g.Profiler.RoomPressure);
                solver.Merge(g.Profiler.Solver);
                sample.Merge(g.Profiler.EnvironmentSample);
                after.Merge(g.Profiler.AfterStep);
                damage.Merge(g.Profiler.Damage);
                massSweep.Merge(g.Profiler.MassSweep);
                lostRooms.Merge(g.Profiler.LostRooms);
                health.Merge(g.Profiler.Health);
                sampling.Merge(g.Profiler.Sampling);
                solar.Merge(g.SolarTime);
                save.Merge(g.SaveTime);
                load.Merge(g.LoadTime);
                build.Merge(g.BuildTime);
                blockEvents.Merge(g.BlockEventTime);
            }

            sb.Append("  Indented rows are nested inside the row above them and are not added\n");
            sb.Append("  into the total below. Only the unindented rows are summed.\n\n");

            TimingStat.WriteHeader(sb, "path (all grids)");
            Telemetry.SessionFrameTime.WriteRow(sb);
            simulation.WriteRow(sb);
            sample.WriteRow(sb);
            solar.WriteRow(sb);
            topology.WriteRow(sb);
            mapping.WriteRow(sb);
            exposure.WriteRow(sb);
            solver.WriteRow(sb);
            after.WriteRow(sb);
            damage.WriteRow(sb);
            massSweep.WriteRow(sb);
            pressure.WriteRow(sb);
            lostRooms.WriteRow(sb);
            health.WriteRow(sb);
            sampling.WriteRow(sb);

            double unattributed = CostRollup.Unattributed(
                simulation.TotalMilliseconds,
                sample.TotalMilliseconds + topology.TotalMilliseconds + mapping.TotalMilliseconds
                    + exposure.TotalMilliseconds + solver.TotalMilliseconds + after.TotalMilliseconds);

            sb.Append("    unattributed").Append(new string(' ', 20))
              .Append("-".PadLeft(12))
              .Append(unattributed.ToString("n2").PadLeft(14))
              .Append("-".PadLeft(14))
              .Append("-".PadLeft(12))
              .Append('\n');

            save.WriteRow(sb);
            load.WriteRow(sb);
            build.WriteRow(sb);
            blockEvents.WriteRow(sb);
            Telemetry.DrawTime.WriteRow(sb);
            for (int i = 0; i < Telemetry.DrawParts.Length; i++) Telemetry.DrawParts[i].WriteRow(sb);

            double total = CostRollup.MeasuredMilliseconds(
                Telemetry.SessionFrameTime.TotalMilliseconds,
                save.TotalMilliseconds,
                load.TotalMilliseconds,
                build.TotalMilliseconds,
                blockEvents.TotalMilliseconds) + Telemetry.DrawTime.TotalMilliseconds;

            double share = CostRollup.ShareOfRealTime(total, Telemetry.SessionSeconds);

            sb.Append('\n');
            Field(sb, "total measured", total.ToString("n1") + " ms");
            Field(sb, "share of real time", share < 0.0
                ? "-"
                : share.ToString("n3") + " %");

            WriteFrames(sb);

            sb.Append("\n  grid simulation, per call:\n");
            simulation.WriteDistribution(sb, "    ");
            sb.Append("\n  solver, per call:\n");
            solver.WriteDistribution(sb, "    ");
            sb.Append("\n  room mapping, per call:\n");
            mapping.WriteDistribution(sb, "    ");
            sb.Append("\n  topology rebuild, per call:\n");
            topology.WriteDistribution(sb, "    ");
            sb.Append("\n  solar occlusion, per call:\n");
            solar.WriteDistribution(sb, "    ");

            Section(sb, "Frame pacing");
            Telemetry.Pacing.Write(sb);
        }


        private static void WriteFrames(StringBuilder sb)
        {
            int throttled = 0;
            double worstRate = 1d;
            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                GridTelemetry g = Telemetry.Grids[i];
                if (g.SimulationRate.Count == 0) continue;

                double rate = g.SimulationRate.Mean;
                if (rate >= 0.999d) continue;

                throttled++;
                if (rate < worstRate) worstRate = rate;
            }

            if (throttled > 0)
            {
                sb.Append('\n');
                Field(sb, "grids below real time", throttled.ToString("n0")
                    + ", slowest at " + (100.0 * worstRate).ToString("n1") + " %");
                sb.Append("  A grid below real time is too large to simulate at full rate and is\n");
                sb.Append("  taking shorter steps rather than coarser ones — MaxElementVisitsPerStep.\n");
                sb.Append("  Heat moves more slowly on it; nothing else about it is different.\n");
            }

            FrameCostTracker frames = Telemetry.FrameCost;

            sb.Append("\n  per frame, all grids together:\n");

            if (frames.Frame.Calls == 0)
            {
                sb.Append("    (no frame did any work)\n");
                return;
            }

            Field(sb, "frames with work", frames.FramesWithWork.ToString("n0"));
            Field(sb, "mean", frames.Frame.MeanMilliseconds.ToString("n3") + " ms");
            Field(sb, "worst", frames.Frame.MaxMilliseconds.ToString("n3") + " ms");
            Field(sb, "over a 60 fps frame", frames.FramesOverBudget.ToString("n0") + " frames ("
                + (frames.FramesWithWork == 0
                    ? "-"
                    : (100.0 * frames.FramesOverBudget / frames.FramesWithWork).ToString("n2") + " %")
                + ")");

            Field(sb, "worst over mean", frames.SpikeRatio.ToString("n1") + "x");

            sb.Append("\n    distribution:\n");
            frames.Frame.WriteDistribution(sb, "      ");

            IList<FrameSample> worst = frames.Worst;
            if (worst.Count == 0) return;

            sb.Append("\n    worst frames:\n");
            for (int i = 0; i < worst.Count; i++)
            {
                sb.Append("      ").Append(worst[i].Describe()).Append('\n');
            }
        }


        private static List<GridTelemetry> SortedGrids()
        {

            List<GridTelemetry> grids = new List<GridTelemetry>(Telemetry.Grids);
            grids.Sort(delegate (GridTelemetry a, GridTelemetry b) { return b.PeakCellCount.CompareTo(a.PeakCellCount); });
            return grids;
        }


        private static void WriteGridTable(StringBuilder sb)
        {
            Section(sb, "Grids");

            if (Telemetry.Grids.Count == 0)
            {
                sb.Append("  none\n");
                return;
            }

            sb.Append("  ")
              .Append("name".PadRight(28))
              .Append("size".PadRight(7))
              .Append("cells".PadLeft(8))
              .Append("links".PadLeft(9))
              .Append("rooms".PadLeft(7))
              .Append("peak T".PadLeft(10))
              .Append("crit".PadLeft(8))
              .Append("life s".PadLeft(9))
              .Append("  state\n");


            List<GridTelemetry> grids = SortedGrids();
            for (int i = 0; i < grids.Count; i++)
            {
                GridTelemetry g = grids[i];
                sb.Append("  ")
                  .Append(Truncate(g.Name, 27).PadRight(28))
                  .Append((g.GridSize ?? "-").PadRight(7))
                  .Append(g.PeakCellCount.ToString("n0").PadLeft(8))
                  .Append(g.NeighborLinks.SafeMax.ToString("n0").PadLeft(9))
                  .Append(g.RoomCount.SafeMax.ToString("n0").PadLeft(7))
                  .Append(FormatPeak(g.PeakTemperature).PadLeft(10))
                  .Append(g.CriticalBlocks.SafeMax.ToString("n0").PadLeft(8))
                  .Append(g.LifetimeSeconds.ToString("n0").PadLeft(9))
                  .Append("  ")
                  .Append(g.IsClosed ? "closed" : "alive")
                  .Append('\n');
            }
        }


        private static void WriteOverlay(StringBuilder sb)
        {
            if (Telemetry.Overlay.Frames == 0) return;

            Section(sb, "Block overlay");
            Telemetry.Overlay.Write(sb);
        }


        private static void WritePlanetProperties(StringBuilder sb)
        {
            if (Telemetry.PlanetProperties.Count == 0) return;

            sb.Append("\n  climate in force\n");
            foreach (KeyValuePair<string, string> planet in Telemetry.PlanetProperties)
            {
                sb.Append("    ").Append(planet.Key).Append('\n');
                sb.Append("      ").Append(planet.Value).Append('\n');
            }
        }


        private static void WriteSubsteps(StringBuilder sb)
        {
            Section(sb, "Substeps");

            sb.Append("  A step is cut into as many substeps as the stiffest element needs, and every\n");
            sb.Append("  other element pays for all of them. Demand figures are computed from real heat\n");
            sb.Append("  capacities, so they describe the grids rather than the settings in force.\n");


            List<GridTelemetry> grids = SortedGrids();

            double passes = 0;
            double solverMs = 0;
            double elementPasses = 0;
            long stepping = 0;


            Histogram granted = new Histogram(new float[] { 1.5f, 2.5f, 4.5f, 6.5f, 8.5f, 12.5f, 16.5f, 32.5f, 64.5f });
            long[] grantedCells = new long[granted.Counts.Length];

            for (int i = 0; i < grids.Count; i++)
            {
                GridTelemetry g = grids[i];
                if (g.SimulationSteps <= 0 || g.Substeps.Count == 0) continue;

                stepping++;

                double substeps = g.Substeps.Mean;
                double gridPasses = g.SimulationSteps * substeps;

                passes += gridPasses;
                solverMs += g.Profiler.Solver.TotalMilliseconds;
                elementPasses += gridPasses * (g.PeakCellCount + g.NeighborLinks.SafeMax);

                granted.Add((float)substeps);

                float value = (float)substeps;
                int bucket = granted.Edges.Length;
                for (int e = 0; e < granted.Edges.Length; e++)
                {
                    if (value < granted.Edges[e]) { bucket = e; break; }
                }
                grantedCells[bucket] += g.PeakCellCount;
            }

            sb.Append('\n');
            Field(sb, "grids stepping", stepping.ToString("n0"));
            Field(sb, "substep passes", passes.ToString("n0"));
            Field(sb, "solver ms", solverMs.ToString("n1"));
            Field(sb, "ms per substep pass", passes <= 0 ? "-" : (solverMs / passes).ToString("n4"));
            Field(sb, "ns per element visit", elementPasses <= 0
                ? "-"
                : (solverMs * 1e6 / elementPasses).ToString("n1"));

            sb.Append("\n  substeps granted, by grid and by how many cells are in them:\n");
            for (int i = 0; i < granted.Counts.Length; i++)
            {
                if (granted.Counts[i] == 0) continue;

                string label = TelemetryFormat.BucketLabel(granted.Edges, i, "");
                sb.Append("      ").Append(label.PadRight(20))
                  .Append(granted.Counts[i].ToString("n0").PadLeft(8)).Append(" grids")
                  .Append(grantedCells[i].ToString("n0").PadLeft(12)).Append(" cells\n");
            }

            WriteSubstepDrivers(sb, grids);
            WriteStiffestBlockTypes(sb);
            WriteSubstepProjection(sb, grids);
        }


        private static void WriteStiffestBlockTypes(StringBuilder sb)
        {

            List<BlockTypeTelemetry> types = new List<BlockTypeTelemetry>();
            foreach (BlockTypeTelemetry type in Telemetry.BlockTypes.Values)
            {
                if (type.SubstepDemand.Count > 0) types.Add(type);
            }

            if (types.Count == 0) return;

            types.Sort(delegate (BlockTypeTelemetry a, BlockTypeTelemetry b)
            {
                return b.PeakSubstepDemand.CompareTo(a.PeakSubstepDemand);
            });

            sb.Append("\n  block types by the substeps they demand (20 stiffest of ")
              .Append(types.Count).Append("):\n");

            sb.Append("    ").Append("subtype".PadRight(34))
              .Append("live".PadLeft(9)).Append("J/K".PadLeft(11))
              .Append("worst".PadLeft(9)).Append("mean".PadLeft(9)).Append("  where\n");

            int limit = Math.Min(types.Count, 20);
            for (int i = 0; i < limit; i++)
            {
                BlockTypeTelemetry t = types[i];

                sb.Append("    ").Append(Truncate(t.Name, 33).PadRight(34))
                  .Append(t.Live.ToString("n0").PadLeft(9))
                  .Append(t.ThermalMass.Mean.ToString("n0").PadLeft(11))
                  .Append(t.PeakSubstepDemand.ToString("n1").PadLeft(9))
                  .Append(t.SubstepDemand.Mean.ToString("n1").PadLeft(9))
                  .Append("  ").Append(t.PeakSubstepDemandPosition).Append('\n');
            }
        }


        private static void WriteSubstepDrivers(StringBuilder sb, IList<GridTelemetry> grids)
        {

            List<GridTelemetry> profiled = new List<GridTelemetry>();
            for (int i = 0; i < grids.Count; i++)
            {
                if (grids[i].Profile != null && grids[i].Profile.WorstNodeIndex >= 0) profiled.Add(grids[i]);
            }

            profiled.Sort(delegate (GridTelemetry a, GridTelemetry b)
            {
                return b.Profile.RequiredSubsteps.CompareTo(a.Profile.RequiredSubsteps);
            });

            if (profiled.Count == 0) return;

            sb.Append("\n  what sets each grid's substep count (");
            sb.Append(Math.Min(profiled.Count, DetailedGridLimit)).Append(" stiffest of ")
              .Append(profiled.Count).Append("):\n");

            sb.Append("    ").Append("grid".PadRight(26))
              .Append("needs".PadLeft(8)).Append("got".PadLeft(6))
              .Append("cond%".PadLeft(7)).Append("air".PadLeft(8)).Append("loop".PadLeft(8))
              .Append("  set by\n");

            int limit = Math.Min(profiled.Count, DetailedGridLimit);
            for (int i = 0; i < limit; i++)
            {
                GridTelemetry g = profiled[i];
                ThermalSolver.SubstepProfile p = g.Profile;

                sb.Append("    ").Append(Truncate(g.Name, 25).PadRight(26))
                  .Append(p.RequiredSubsteps.ToString("n1").PadLeft(8))
                  .Append(g.Substeps.SafeMax.ToString("n0").PadLeft(6))
                  .Append((100f * p.WorstNodeConductionShare).ToString("n0").PadLeft(7))
                  .Append(p.WorstRoomAirDemand.ToString("n1").PadLeft(8))
                  .Append(p.WorstLoopDemand.ToString("n1").PadLeft(8))
                  .Append("  ").Append(g.WorstSubstepBlock).Append('\n');
            }
        }


        private static void WriteSubstepProjection(StringBuilder sb, IList<GridTelemetry> grids)
        {
            int[] caps = ThermalSolver.SubstepProfile.ProjectedCaps;

            double[] visitsAfter = new double[caps.Length];
            long[] flooredNodes = new long[caps.Length];
            double visitsNow = 0;
            long nodes = 0;
            long profiled = 0;
            long environmentDominated = 0;

            for (int i = 0; i < grids.Count; i++)
            {
                GridTelemetry g = grids[i];
                ThermalSolver.SubstepProfile p = g.Profile;
                if (p == null || g.SimulationSteps <= 0 || g.Substeps.Count == 0) continue;

                profiled++;
                nodes += p.Nodes;
                environmentDominated += p.EnvironmentDominatedNodes;

                double elements = p.Nodes + p.Links;
                double perStepSecond = p.StepSeconds <= 0 ? 1 : elements / p.StepSeconds;

                visitsNow += perStepSecond * Math.Max(1.0, p.RequiredSubsteps);

                for (int c = 0; c < caps.Length; c++)
                {
                    visitsAfter[c] += perStepSecond * Math.Max(1.0, p.CapRequiredSubsteps[c]);
                    flooredNodes[c] += p.CapNodesFloored[c];
                }
            }

            if (profiled == 0) return;

            sb.Append("\n  what MaxSubstepsPerBlock would do, over ").Append(profiled.ToString("n0"))
              .Append(" profiled grids and ").Append(nodes.ToString("n0")).Append(" blocks:\n");

            sb.Append("  Cost is element visits per simulated second, which is what a cap changes:\n");
            sb.Append("  a shortened step trades the same arithmetic for less heat, so per step it\n");
            sb.Append("  would look free.\n\n");

            sb.Append("    ").Append("cap".PadLeft(5))
              .Append("blocks raised".PadLeft(16)).Append("share".PadLeft(9))
              .Append("visits/sim second".PadLeft(20)).Append("saving".PadLeft(9))
              .Append("speedup".PadLeft(10)).Append('\n');

            sb.Append("    ").Append("off".PadLeft(5))
              .Append("0".PadLeft(16)).Append("-".PadLeft(9))
              .Append(visitsNow.ToString("n0").PadLeft(20)).Append("-".PadLeft(9))
              .Append("-".PadLeft(10)).Append('\n');

            for (int c = 0; c < caps.Length; c++)
            {
                double saving = visitsNow <= 0 ? 0 : 1.0 - (visitsAfter[c] / visitsNow);
                double speedup = visitsAfter[c] <= 0 ? 0 : visitsNow / visitsAfter[c];

                sb.Append("    ").Append(caps[c].ToString().PadLeft(5))
                  .Append(flooredNodes[c].ToString("n0").PadLeft(16))
                  .Append((nodes <= 0 ? 0 : 100.0 * flooredNodes[c] / nodes).ToString("n2").PadLeft(8)).Append('%')
                  .Append(visitsAfter[c].ToString("n0").PadLeft(20))
                  .Append((100.0 * saving).ToString("n1").PadLeft(8)).Append('%')
                  .Append(speedup.ToString("n2").PadLeft(9)).Append('x').Append('\n');
            }

            if (environmentDominated > 0)
            {
                sb.Append("\n  ").Append(environmentDominated.ToString("n0"))
                  .Append(" blocks are stiff mostly through radiation and convection rather than\n");
                sb.Append("  conduction. The cap covers those too, which changes how they exchange with\n");
                sb.Append("  the sky rather than with what they are bolted to — a more visible trade.\n");
            }
        }


        private static void WriteConsistency(StringBuilder sb)
        {
            Section(sb, "Consistency");

            int records = 0;
            long calls = 0;
            long steps = 0;
            double milliseconds = 0;
            long mostTicks = 0;
            long firstFrame = long.MaxValue;
            long lastFrame = -1;
            int overFramed = 0;

            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                GridTelemetry g = Telemetry.Grids[i];

                records++;
                calls += g.SimulationTime.Calls;
                steps += g.SimulationSteps;
                milliseconds += g.SimulationTime.TotalMilliseconds;

                if (g.SimulationTime.Calls > mostTicks) mostTicks = g.SimulationTime.Calls;
                if (g.SimulationTime.Calls > Telemetry.FramesObserved) overFramed++;
                if (g.FirstTickFrame >= 0 && g.FirstTickFrame < firstFrame) firstFrame = g.FirstTickFrame;
                if (g.LastTickFrame > lastFrame) lastFrame = g.LastTickFrame;
            }

            if (firstFrame == long.MaxValue) firstFrame = -1;

            sb.Append("  The same list, summed at the Cost table and again here. These must agree;\n");
            sb.Append("  a line marked !! is a defect in the telemetry rather than in the mod.\n\n");

            Field(sb, "grid records", costRecords + " -> " + records);
            Field(sb, "grid ticks", costCalls.ToString("n0") + " -> " + calls.ToString("n0"));
            Field(sb, "grid simulation ms", costMilliseconds.ToString("n2") + " -> " + milliseconds.ToString("n2"));
            Field(sb, "grid simulation steps", costSteps.ToString("n0") + " -> " + steps.ToString("n0"));

            if (records != costRecords || calls != costCalls || steps != costSteps)
            {
                sb.Append("  !! the record list changed while the report was being written;\n");
                sb.Append("  !! every aggregate above the grid detail is short by the difference.\n");
            }

            double frame = Telemetry.SessionFrameTime.TotalMilliseconds;
            if (frame > 0d && milliseconds > frame)
            {
                sb.Append("  !! grid simulation totals ")
                    .Append((milliseconds / frame).ToString("n2"))
                    .Append("x the session frame it runs inside, so one of the two clocks is\n");
                sb.Append("  !! wrong: the frame is ")
                    .Append(frame.ToString("n0"))
                    .Append(" ms and the grids inside it sum to ")
                    .Append(milliseconds.ToString("n0"))
                    .Append(" ms.\n");
            }

            sb.Append('\n');
            Field(sb, "frames observed", Telemetry.FramesObserved.ToString("n0"));
            Field(sb, "most ticks on one grid", mostTicks.ToString("n0"));
            Field(sb, "tick frames spanned", firstFrame < 0
                ? "-"
                : firstFrame.ToString("n0") + " .. " + lastFrame.ToString("n0"));

            if (overFramed > 0)
            {
                sb.Append("  !! ").Append(overFramed).Append(" grid(s) ticked more often than the session was framed.\n");
                sb.Append("  !! A grid ticks once per Simulate call and so does the frame counter, so this\n");
                sb.Append("  !! means the session counters were reset under records that survived it —\n");
                sb.Append("  !! see Telemetry.Reset and Telemetry.Start. Every per-session figure in this\n");
                sb.Append("  !! report covers a shorter window than the per-grid rows do.\n");
            }

            double longestLife = 0;
            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                double life = Telemetry.Grids[i].LifetimeSeconds;
                if (life > longestLife) longestLife = life;
            }

            sb.Append('\n');
            Field(sb, "session seconds", Telemetry.SessionSeconds.ToString("n1"));
            Field(sb, "longest grid lifetime", longestLife.ToString("n1") + " s");

            if (longestLife > Telemetry.SessionSeconds + 1.0)
            {
                sb.Append("  !! a grid outlived the session clock, which is only possible if the\n");
                sb.Append("  !! clock was reset while the record survived.\n");
            }
        }


        private static void WriteGridSubsteps(StringBuilder sb, GridTelemetry g)
        {
            ThermalSolver.SubstepProfile p = g.Profile;
            if (p == null) return;

            sb.Append("\n    substeps\n");
            Field(sb, "  demanded (uncapped)", p.RequiredSubsteps.ToString("n2"));
            Field(sb, "  demanded as configured", p.RequiredSubstepsInForce.ToString("n2"));
            Field(sb, "  set by", g.WorstSubstepBlock);
            Field(sb, "  of which conduction", (100f * p.WorstNodeConductionShare).ToString("n0") + " %");

            if (p.WorstRoomAirDemand > 0f)
            {
                Field(sb, "  stiffest room air", p.WorstRoomAirDemand.ToString("n2")

                    + " substeps (room " + p.WorstRoomAirIndex + ")");
            }

            if (p.WorstLoopDemand > 0f)
            {
                Field(sb, "  stiffest coolant loop", p.WorstLoopDemand.ToString("n2")

                    + " substeps (loop " + p.WorstLoopIndex + ")");
            }

            if (p.EnvironmentDominatedNodes > 0)
            {
                Field(sb, "  stiff through the sky", p.EnvironmentDominatedNodes.ToString("n0") + " blocks");
            }

            sb.Append("\n      blocks by the substeps they demand\n");
            float[] edges = ThermalSolver.SubstepProfile.DemandEdges;
            for (int i = 0; i < p.Buckets.Length; i++)
            {
                if (p.Buckets[i] == 0) continue;

                string label = TelemetryFormat.BucketLabel(edges, i, "");
                double share = p.Nodes <= 0 ? 0 : 100.0 * p.Buckets[i] / p.Nodes;

                sb.Append("        ").Append(label.PadRight(22))
                  .Append(p.Buckets[i].ToString("n0").PadLeft(10))
                  .Append("  ").Append(share.ToString("n2")).Append("%\n");
            }

            sb.Append("\n      what a cap would leave\n");
            sb.Append("        ").Append("cap".PadLeft(5))
              .Append("blocks raised".PadLeft(16)).Append("substeps".PadLeft(11))
              .Append("cost per sim s".PadLeft(16)).Append('\n');

            int[] caps = ThermalSolver.SubstepProfile.ProjectedCaps;
            double now = Math.Ceiling(Math.Max(1.0, p.RequiredSubsteps));

            for (int c = 0; c < caps.Length; c++)
            {
                double after = Math.Ceiling(Math.Max(1.0, p.CapRequiredSubsteps[c]));
                double share = now <= 0 ? 1 : after / now;

                sb.Append("        ").Append(caps[c].ToString().PadLeft(5))
                  .Append(p.CapNodesFloored[c].ToString("n0").PadLeft(16))
                  .Append(after.ToString("n0").PadLeft(11))
                  .Append((100.0 * share).ToString("n0").PadLeft(12)).Append("%").Append('\n');
            }
        }
    }
}
