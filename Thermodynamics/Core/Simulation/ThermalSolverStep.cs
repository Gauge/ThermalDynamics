using System;
using System.Diagnostics;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private enum StepStage
        {
            Idle,

            Begin,

            Environment,

            Conduction,

            Coupled,

            Apply,

            Publish
        }

        private StepStage stage = StepStage.Idle;
        private int stageCursor;
        private int stepSourceCursor;

        public class StepPhaseProfile
        {
            public readonly long[] Ticks = new long[PhaseCount];
            public readonly long[] Visits = new long[PhaseCount];
            public readonly long[] Slices = new long[PhaseCount];

            public const int PhaseCount = 7;

            public static readonly string[] Names =
                { "begin", "environment", "conduction", "coupled", "apply", "publish", "env fill" };

            public const int EnvironmentFill = 6;


            public void Reset()
            {
                for (int i = 0; i < PhaseCount; i++)
                {
                    Ticks[i] = 0;
                    Visits[i] = 0;
                    Slices[i] = 0;
                }
            }


            public double MillisecondsOf(int phase)
            {
                return Ticks[phase] * 1000d / Stopwatch.Frequency;
            }
        }

        public bool ProfileStepPhases;


        public readonly StepPhaseProfile StepPhases = new StepPhaseProfile();

        private EnvironmentState stepEnvironment;
        private HeatSourceState[] stepHeatSources;
        private EnvironmentPlan stepPlan;
        private float stepDeltaSeconds;
        private float substepSeconds;
        private int substepsLeft;

        private int publishHottest;
        private float publishPeak;

        private int stepNodeCount;
        private int stepLinkCount;

        public bool ConductionClampLive { get; private set; }

        public bool GateConductionClamp = true;

        private bool diagnosticsSubstep;
        private bool stepCollectDiagnostics, stepDiagnosticsOnEverySubstep;

        public bool DiagnosticsOnEverySubstep;

        public bool FuseWattsClear = true;

        public bool StepInFlight
        {
            get { return stage != StepStage.Idle; }
        }

        public long StepWorkUnits { get; private set; }

        public long StepWorkRemaining { get; private set; }

        public long LastAdvanceWork { get; private set; }


        private long SubstepWork(int nodeCount, int linkCount, int sources)
        {
            return (nodeCount / 8)
                + nodeCount
                + ((long)sources * nodeCount)
                + linkCount
                + stepCoupledWork
                + nodeCount;
        }


        public void Step(float deltaSeconds, EnvironmentState environment)
        {
            Step(deltaSeconds, environment, -1f);
        }


        public void Step(float deltaSeconds, EnvironmentState environment, float knownRequired)
        {
            if (!BeginStep(deltaSeconds, environment, knownRequired)) return;
            while (!AdvanceStep(long.MaxValue)) { }
        }


        public bool BeginStep(float deltaSeconds, EnvironmentState environment)
        {

            return BeginStep(deltaSeconds, environment, -1f);
        }


        public bool BeginStep(float deltaSeconds, EnvironmentState environment, float knownRequired)
        {
            if (deltaSeconds <= 0f) return false;

            if (StepInFlight) return true;

            Environment = environment;

            float required;
            if (knownRequired >= 0f)
            {
                required = knownRequired;
            }
            else
            {
                float worstNodeRate = PrepareStepState(true);

                required = RequiredSubstepsFromState(deltaSeconds, worstNodeRate);
            }

            RefreshLinkMassFactors();

            stepEnvironment = environment;
            // Hosts may reuse their sample arrays between slices. Keep the
            // active source values private; Environment still exposes the
            // caller's supplied state, not this retained work buffer.
            int sourceCount=environment.HeatSources==null ? 0
                : Math.Max(0,Math.Min(environment.HeatSourceCount,environment.HeatSources.Length));
            if(sourceCount>0)
            {
                if(stepHeatSources==null || stepHeatSources.Length<sourceCount)
                {
                    int capacity=stepHeatSources==null ? 0 : stepHeatSources.Length;
                    int doubled=capacity<=int.MaxValue/2 ? capacity*2 : int.MaxValue;
                    stepHeatSources=new HeatSourceState[Math.Max(4,Math.Max(sourceCount,doubled))];
                }
                Array.Copy(environment.HeatSources,stepHeatSources,sourceCount);
            }
            stepEnvironment.HeatSources=sourceCount>0 ? stepHeatSources : null;
            stepEnvironment.HeatSourceCount=sourceCount;
            stepDeltaSeconds = deltaSeconds;
            stepCollectDiagnostics = CollectDiagnostics;
            stepDiagnosticsOnEverySubstep = DiagnosticsOnEverySubstep;

            InvalidateEnvironmentRows();

            overheats.Clear();
            ClearOverheatAccumulator();
            crossings.Clear();


            int substeps = ClampSubsteps(required);
            LastSubsteps = substeps;
            LastStepWasClamped = required > MaxSubsteps;

            LastRequiredSubsteps = deltaSeconds > 0f
                ? required * (settings.StepSeconds / deltaSeconds)
                : required;

            for (int p = 0; p < heatPumps.Count; p++)
            {
                heatPumps[p].BeginStep();
            }

            for (int l = 0; l < loops.Count; l++)
            {
                loops[l].BeginStep();
            }

            Work.SolverSteps++;
            Work.SolverSubsteps += substeps;

            substepSeconds = deltaSeconds / substeps;
            substepsLeft = substeps;

            ConductionClampLive = settings.ClampConductionOvershoot
                && (settings.EnableConduction || settings.EnableCoolantLoops || settings.EnableRoomAir)
                && (!GateConductionClamp || ClampCanBind(substepSeconds));

            stepNodeCount = nodes.Count;
            stepLinkCount = links.Count;

            // A refresh during the previous step could only publish its original node set.
            // Retry once now that appended nodes have joined the prepared state.
            if (shadowNodesDeferred)
            {
                sunLitDirty = true;
                windLitDirty = true;
                shadowNodesDeferred = false;
            }

            stepCoupledWork = CoupledSubstepWork();
            int sources = settings.EnableHeatSources ? stepEnvironment.HeatSourceCount : 0;
            StepWorkUnits = (SubstepWork(stepNodeCount, stepLinkCount, sources) * substeps)
                + stepNodeCount;
            StepWorkRemaining = StepWorkUnits;

            stage = StepStage.Begin;
            stageCursor = 0;
            return true;
        }


        public bool AdvanceStep(long workBudget)
        {
            LastAdvanceWork = 0;

            if (stage == StepStage.Idle) return true;
            if (workBudget <= 0) return false;

            Work.StepAdvances++;

            long spent = 0;

            while (stage != StepStage.Idle && spent < workBudget)
            {
                long remaining = workBudget - spent;

                int phase = (int)stage - 1;

                if (stage == StepStage.Environment && (!environmentRowsValid || !PrecomputeEnvironment))
                {
                    phase = StepPhaseProfile.EnvironmentFill;
                }

                long started = ProfileStepPhases ? Stopwatch.GetTimestamp() : 0L;
                long before = spent;

                switch (stage)
                {
                    case StepStage.Begin:

                        spent += BeginSubstep();
                        break;

                    case StepStage.Environment:

                        spent += AdvanceEnvironment(remaining);
                        break;

                    case StepStage.Conduction:

                        spent += AdvanceConduction(remaining);
                        break;

                    case StepStage.Coupled:

                        spent += AdvanceCoupled(remaining);
                        break;

                    case StepStage.Apply:

                        spent += AdvanceApply(remaining);
                        break;

                    default:

                        spent += AdvancePublish(remaining);
                        break;
                }

                if (ProfileStepPhases && phase >= 0 && phase < StepPhaseProfile.PhaseCount)
                {
                    StepPhases.Ticks[phase] += Stopwatch.GetTimestamp() - started;
                    StepPhases.Visits[phase] += spent - before;
                    StepPhases.Slices[phase]++;
                }
            }

            LastAdvanceWork = spent;

            StepWorkRemaining -= spent;
            if (StepWorkRemaining < 0) StepWorkRemaining = 0;

            return stage == StepStage.Idle;
        }


        private long BeginSubstep()
        {
            int nodeCount = stepNodeCount;

            diagnosticsSubstep = stepCollectDiagnostics && (stepDiagnosticsOnEverySubstep || substepsLeft <= 1);

            if (!FuseWattsClear) Array.Clear(nodeWatts, 0, nodeCount);

            ResetEnvironmentTotals();


            stepPlan = PlanEnvironment(ref stepEnvironment);

            stage = StepStage.Environment;
            stageCursor = 0;
            stepSourceCursor = -1;

            return nodeCount / 8;
        }


        private long AdvanceEnvironment(long budget)
        {
            int count = stepNodeCount;
            long spent = 0;

            if (stepSourceCursor < 0)
            {
                int end = Advance(count, budget);
                AccumulateEnvironmentRange(ref stepEnvironment, ref stepPlan, substepSeconds,
                    stageCursor, end);
                spent = end - stageCursor;
                stageCursor = end;
                if (stageCursor < count) return spent;

                stepSourceCursor = 0;
                stageCursor = 0;
            }

            // Preserve source-major summation order across slices, including diagnostic totals.
            if (stepPlan.SourcesEnabled && count > 0)
            {
                while (stepSourceCursor < stepEnvironment.HeatSourceCount && spent < budget)
                {
                    int end = Advance(count, budget - spent);
                    AccumulateHeatSourceRange(stepEnvironment.HeatSources[stepSourceCursor],
                        stepPlan.Diagnostics, stageCursor, end);
                    spent += end - stageCursor;
                    stageCursor = end;
                    if (stageCursor < count) return spent;
                    stepSourceCursor++;
                    stageCursor = 0;
                }
                if (stepSourceCursor < stepEnvironment.HeatSourceCount) return spent;
            }

            PublishEnvironmentTotals();
            stage = StepStage.Conduction;
            stageCursor = 0;
            return spent;
        }


        private long AdvanceConduction(long budget)
        {
            int count = stepLinkCount;

            int end = Advance(count, budget);

            AccumulateConductionRange(substepSeconds, stageCursor, end);

            long spent = end - stageCursor;
            stageCursor = end;

            if (stageCursor >= count)
            {
                stage = StepStage.Coupled;
                stageCursor = 0;
                ResetCoupledCursor();
            }

            return spent;
        }


        private long AdvanceApply(long budget)
        {
            int count = stepNodeCount;

            int end = Advance(count, budget);

            ApplyNodeWattsRange(substepSeconds, stageCursor, end);

            long spent = end - stageCursor;
            stageCursor = end;

            if (stageCursor < count) return spent;

            ApplyCoupledWatts(substepSeconds);

            substepsLeft--;
            if (substepsLeft > 0)
            {
                stage = StepStage.Begin;
                stageCursor = 0;
                return spent;
            }

            for (int p = 0; p < heatPumps.Count; p++)
            {
                heatPumps[p].EndStep(stepDeltaSeconds);
            }

            for (int l = 0; l < loops.Count; l++)
            {
                loops[l].EndStep(stepDeltaSeconds);
            }

            PublishOverheats();

            stage = StepStage.Publish;
            stageCursor = 0;
            publishHottest = -1;
            publishPeak = float.MinValue;
            return spent;
        }


        private long AdvancePublish(long budget)
        {
            int count = stepNodeCount;

            int end = Advance(count, budget);

            bool watching = thresholds.Count > 0;

            for (int i = stageCursor; i < end; i++)
            {
                float updated = nodeTemperatures[i];
                ThermalNode node = nodes[i];
                float previous = nodeStepStart[i];

                if (node.Temperature == previous)
                {
                    node.Temperature = updated;
                }
                else
                {
                    // Preserve an external write while applying the pending step's delta. Its
                    // earlier floor was relative to the captured temperature, not this new one.
                    float published = node.Temperature + (updated - previous);
                    node.Temperature = published < ThermalConstants.MinimumTemperature
                        ? ThermalConstants.MinimumTemperature : published;
                }

                node.LastDeltaTemperature = updated - previous;

                if (updated > publishPeak)
                {
                    publishPeak = updated;
                    publishHottest = i;
                }

                if (watching) thresholds.Collect(node.Block, previous, updated, crossings);
            }

            long spent = end - stageCursor;
            stageCursor = end;

            if (stageCursor >= count)
            {
                hottestNode = publishHottest;
                StepCount++;
                stage = StepStage.Idle;
                stageCursor = 0;
                StepWorkRemaining = 0;
            }

            return spent;
        }


        private int Advance(int count, long budget)
        {
            if (budget <= 0) return stageCursor;

            long room = count - stageCursor;
            if (room <= 0) return stageCursor;

            return stageCursor + (int)(budget < room ? budget : room);
        }


        // Stops pending work; it is not a rollback. Earlier substeps may already have applied
        // coolant/room temperatures, and a partial Publish may already have written nodes.
        // The next preparation resynchronizes node scratch from the public node state.
        public void AbandonStep()
        {
            if (stage == StepStage.Idle) return;

            ClearOverheatAccumulator();

            for (int p = 0; p < heatPumps.Count; p++)
            {
                heatPumps[p].EndStep(stepDeltaSeconds);
            }

            for (int l = 0; l < loops.Count; l++)
            {
                loops[l].EndStep(stepDeltaSeconds);
            }

            stage = StepStage.Idle;
            stageCursor = 0;
            substepsLeft = 0;
            StepWorkRemaining = 0;
        }
    }
}
