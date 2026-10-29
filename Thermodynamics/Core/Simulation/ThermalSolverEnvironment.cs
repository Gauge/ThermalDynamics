using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private static void ResolveDirection(ref Vector3 direction, float[] weights)
        {
            for (int f = 0; f < Face.Count; f++)
            {
                float dot = Vector3.Dot(Face.Normals[f], direction);
                weights[f] = dot > 0f ? dot : 0f;
            }
        }

        private struct EnvironmentPlan
        {
            public bool EnvironmentEnabled;
            public bool Radiating;
            public bool Convecting;
            public bool Windy;
            public bool SolarEnabled;
            public bool FrictionEnabled;
            public bool SourcesEnabled;
            public bool Generating;
            public bool Diagnostics;

            public bool GenerationOnly;

            public float RadiationShare;
        }


        private float RelaxationFactor(int node, float h)
        {
            if (!ConductionClampLive || h <= 0f) return 1f;

            float conductance = nodeConductanceTotal[node];
            if (conductance <= 0f) return 1f;

            float mass = nodeThermalMass[node];
            if (mass <= 0f) return 1f;

            float stable = mass / (h * conductance);
            return stable >= 1f ? 1f : stable;
        }


        private EnvironmentPlan PlanEnvironment(ref EnvironmentState env)
        {

            EnvironmentPlan plan = new EnvironmentPlan();

            plan.Radiating = settings.EnableEnvironment && settings.EnableRadiation;
            plan.EnvironmentEnabled = plan.Radiating;
            plan.SolarEnabled = settings.EnableSolarHeat && !env.IsSolarOccluded && env.SolarEnergy > 0f;
            plan.SourcesEnabled = settings.EnableHeatSources && env.HeatSourceCount > 0;
            plan.FrictionEnabled = env.FrictionActive;
            plan.Generating = settings.EnableWasteHeat;
            plan.Diagnostics = diagnosticsSubstep;

            plan.Convecting = settings.EnableEnvironment && settings.EnableConvection
                && env.AtmosphereFactor > 0f && env.ConvectionCoefficient > 0f;

            if (plan.Convecting) plan.EnvironmentEnabled = true;

            plan.GenerationOnly = !plan.EnvironmentEnabled && !plan.SolarEnabled
                && !plan.FrictionEnabled && !plan.SourcesEnabled;

            if (plan.GenerationOnly) return plan;

            plan.Windy = plan.Convecting && env.WindSpeed > 0f;
            plan.RadiationShare = 1f - env.AtmosphereFactor;


            if (plan.Windy || plan.FrictionEnabled)
            {
                // Air travels downstream; outward-face exposure and shielding rays point upstream.
                Vector3 wind = -env.WindDirectionLocal;
                ResolveDirection(ref wind, windWeights);

                if (RefreshWindShadow(ref wind)) InvalidateEnvironmentRows();
            }

            if (plan.SolarEnabled)
            {
                Vector3 sun = env.SunDirectionLocal;
                ResolveDirection(ref sun, sunWeights);

                if (RefreshSunShadow(ref sun)) InvalidateEnvironmentRows();
            }

            return plan;
        }

        private void AccumulateEnvironmentRange(ref EnvironmentState env, ref EnvironmentPlan plan,
            float h, int from, int to)
        {
            bool diagnostics = plan.Diagnostics;
            int count = EnvironmentNodeCount;

            if (plan.GenerationOnly)
            {
                if (ConductionClampLive && (!environmentRowsValid || !PrecomputeEnvironment))
                {
                    for (int i = from; i < to; i++)
                    {

                        nodeRelaxation[i] = RelaxationFactor(i, h);
                    }
                }

                bool summing = !HoistHeatGainTotal || !heatGainRowTotalValid;

                if (plan.Generating)
                {
                    float generated = 0f;

                    for (int i = from; i < to; i++)
                    {
                        float generation = nodeGeneration[i];
                        nodeWatts[i] = generation;
                        if (summing) generated += generation;
                    }

                    if (summing) heatGainAccumulator += generated;
                }
                else
                {
                    Array.Clear(nodeWatts, from, to - from);
                }
                if (diagnostics)
                {
                    for (int i = from; i < to; i++) ClearEnvironmentDiagnostics(i);
                }

                if (to >= count && plan.Generating) SettleHeatGainRowTotal(summing);
                if (PrecomputeEnvironment && to >= count) environmentRowsValid = true;
                return;
            }

            bool generating = plan.Generating;
            bool clampRelaxation = settings.ClampEnvironmentOvershoot && h > 0f;
            float inverseH = h > 0f ? 1f / h : 0f;
            bool environmentEnabled = plan.EnvironmentEnabled;
            bool radiating = plan.Radiating;
            bool convecting = plan.Convecting;
            bool windy = plan.Windy;
            bool solarEnabled = plan.SolarEnabled;
            bool frictionEnabled = plan.FrictionEnabled;
            float radiationShare = plan.RadiationShare;

            bool shielded = settings.EnableWindwardShielding
                && nodeWindLit.Length >= count * Face.Count;

            bool fill = !environmentRowsValid || !PrecomputeEnvironment;

            bool fillRelaxation = fill && ConductionClampLive;

            bool summingRows = frictionEnabled
                || !HoistHeatGainTotal || !heatGainRowTotalValid;

            // Rows, environment and running sums in locals: the provider call below is opaque to
            // the JIT, so a field read or sum through `this` would be reloaded every node.
            int[] exposedFaces = nodeExposedFaces;
            float[] temperatures = nodeTemperatures;
            float[] watts = nodeWatts;
            float[] sourceRow = nodeSourceRow;
            float[] solarRow = nodeSolarRow;
            float[] frictionRow = nodeFrictionRow;
            float[] convectionRow = nodeConvectionRow;
            float[] radiationRow = nodeRadiation;
            float[] thermalMass = nodeThermalMass;
            float[] generationRow = nodeGeneration;
            float[] faceWeights = nodeFaceWeights;
            float[] exposedArea = nodeExposedArea;
            float[] absorptivity = nodeAbsorptivity;
            float[] sunLit = nodeSunLit;
            float[] windLit = nodeWindLit;
            float[] windW = windWeights;
            float[] sunW = sunWeights;

            float ambient = env.AmbientTemperature;
            float ambientPow4 = env.AmbientTemperaturePow4;
            float atmosphere = env.AtmosphereFactor;
            float convectionCoefficient = env.ConvectionCoefficient;
            float solarEnergy = env.SolarEnergy;
            float airDensity = env.AirDensity;
            float windSpeed = env.WindSpeed;

            Func<float, float, float, float, float, float, float, float> heating =
                frictionEnabled ? settings.SurfaceHeating : null;

            float environmentSum = environmentWattsAccumulator;
            float heatGainSum = heatGainAccumulator;
            float frictionSum = frictionAccumulator;

            for (int i = from; i < to; i++)
            {
                if (fillRelaxation) nodeRelaxation[i] = RelaxationFactor(i, h);

                if (exposedFaces[i] <= 0)
                {
                    if (fill)
                    {
                        solarRow[i] = 0f;
                        frictionRow[i] = 0f;
                        convectionRow[i] = 0f;
                        sourceRow[i] = generating ? generationRow[i] : 0f;
                    }

                    float buried = sourceRow[i];
                    watts[i] = buried;
                    if (summingRows) heatGainSum += buried;
                    if (diagnostics) ClearEnvironmentDiagnostics(i);
                    continue;
                }

                float temperature = temperatures[i];

                float radiationWatts = 0f;
                float convectionWatts = 0f;

                if (fill)
                {
                    float area = exposedArea[i];

                    int b = i * Face.Count;
                    float f0 = faceWeights[b];
                    float f1 = faceWeights[b + 1];
                    float f2 = faceWeights[b + 2];
                    float f3 = faceWeights[b + 3];
                    float f4 = faceWeights[b + 4];
                    float f5 = faceWeights[b + 5];

                    float wind = 0f;
                    if (windy || frictionEnabled)
                    {
                        DragProfile profile = nodes[i].Drag;
                        wind = shielded
                            ? (f0 * windW[0] * windLit[b] * profile[0])
                                + (f1 * windW[1] * windLit[b + 1] * profile[1])
                                + (f2 * windW[2] * windLit[b + 2] * profile[2])
                                + (f3 * windW[3] * windLit[b + 3] * profile[3])
                                + (f4 * windW[4] * windLit[b + 4] * profile[4])
                                + (f5 * windW[5] * windLit[b + 5] * profile[5])
                            : (f0 * windW[0] * profile[0]) + (f1 * windW[1] * profile[1])
                                + (f2 * windW[2] * profile[2]) + (f3 * windW[3] * profile[3])
                                + (f4 * windW[4] * profile[4]) + (f5 * windW[5] * profile[5]);
                    }

                    float windFactor = windy ? 1f + wind : 1f;

                    convectionRow[i] = convecting
                        ? -convectionCoefficient * area * windFactor
                        : 0f;

                    float solar = 0f;
                    if (solarEnabled)
                    {
                        float lit = (f0 * sunLit[b] * sunW[0])
                            + (f1 * sunLit[b + 1] * sunW[1])
                            + (f2 * sunLit[b + 2] * sunW[2])
                            + (f3 * sunLit[b + 3] * sunW[3])
                            + (f4 * sunLit[b + 4] * sunW[4])
                            + (f5 * sunLit[b + 5] * sunW[5]);

                        solar = solarEnergy * absorptivity[i] * lit * area;
                    }
                    solarRow[i] = solar;

                    float friction = frictionEnabled ? area * wind : 0f;

                    frictionRow[i] = friction;

                    // Keep projected area separate from watts to avoid cancellation when
                    // provider heating is much smaller than the area passed to it.
                    sourceRow[i] = (generating ? generationRow[i] : 0f) + solar;
                }

                float actualFriction = heating != null
                    ? heating(frictionRow[i], airDensity, windSpeed, ambient,
                        temperature, thermalMass[i], h) : 0f;
                if (!(actualFriction >= 0f) || float.IsInfinity(actualFriction)) actualFriction = 0f;
                float source = sourceRow[i] + actualFriction;
                float nodeTotal = source;

                if (environmentEnabled)
                {
                    float radiation = 0f;
                    if (radiating && radiationRow[i] != 0f)
                    {
                        float squared = temperature * temperature;
                        radiation = -radiationRow[i] * ((squared * squared) - ambientPow4);
                    }

                    float convection = 0f;
                    if (convecting)
                    {
                        convection = convectionRow[i] * (temperature - ambient);
                    }

                    radiationWatts = radiationShare * radiation;
                    convectionWatts = atmosphere * convection;

                    float relaxation = radiationWatts + convectionWatts;

                    if (clampRelaxation)
                    {

                        float capped = ClampRelaxation(relaxation,
                            (ambient - temperature) * thermalMass[i] * inverseH);

                        if (capped != relaxation)
                        {
                            float scale = relaxation == 0f ? 0f : capped / relaxation;
                            radiationWatts *= scale;
                            convectionWatts *= scale;
                            relaxation = capped;
                        }
                    }

                    nodeTotal += relaxation;
                }

                watts[i] = nodeTotal;

                environmentSum += radiationWatts + convectionWatts;
                if (summingRows)
                {
                    heatGainSum += source;

                    frictionSum += actualFriction;
                }

                if (!diagnostics) continue;

                ThermalNode node = nodes[i];
                node.LastConductionWatts = 0f;
                node.LastRadiationWatts = radiationWatts;
                node.LastConvectionWatts = convectionWatts;
                node.LastSolarWatts = solarRow[i];
                node.LastFrictionWatts = actualFriction;
                node.LastHeatSourceWatts = 0f;
            }

            environmentWattsAccumulator = environmentSum;
            heatGainAccumulator = heatGainSum;
            frictionAccumulator = frictionSum;

            if (to >= count) SettleHeatGainRowTotal(summingRows);
            if (fill && to >= count) Work.EnvironmentRowFills++;
            if (PrecomputeEnvironment && to >= count) environmentRowsValid = true;
        }


        private void AccumulateHeatSourceRange(HeatSourceState source, bool diagnostics,
            int from, int to)
        {
            if (source.Irradiance <= 0f) return;

            if (from == 0)
            {
                Vector3 direction = source.DirectionLocal;
                ResolveDirection(ref direction, sourceWeights);
            }

            for (int i = from; i < to; i++)
            {
                if (nodeExposedFaces[i] <= 0) continue;

                float watts = source.Irradiance * nodeAbsorptivity[i]

                    * Weighted(i, sourceWeights) * nodeExposedArea[i];
                if (watts == 0f) continue;

                nodeWatts[i] += watts;
                heatGainAccumulator += watts;
                if (diagnostics) nodes[i].LastHeatSourceWatts += watts;
            }
        }


        private bool RefreshSunShadow(ref Vector3 sunLocal)
        {
            if (!settings.SolarSelfShadowing)
            {
                if (sunShadow.IsBuilt || sunShadow.IsRunning || sunLitDirty)
                {
                    sunShadow.Clear();
                    BeginSunLit(1f);
                    sunLitDirty = false;
                }


                return StepSunLit(SunLitBudget);
            }

            if (sunLitDirty || sunShadow.NeedsRestart(ref sunLocal, SunRebuildCosine))
            {
                sunShadow.Restart(grid, sunLocal, SunOccluders);
                sunLitDirty = false;
            }

            if (sunShadow.Step(SunShadowBudget)) BeginSunLit(-1f);


            return StepSunLit(SunLitBudget);
        }


        private bool RefreshWindShadow(ref Vector3 windLocal)
        {
            if (!settings.EnableWindwardShielding)
            {
                if (windShadow.IsBuilt || windShadow.IsRunning)
                {
                    windShadow.Clear();
                    BeginWindLit(1f);
                }


                return StepWindLit(SunLitBudget);
            }

            if (windLitDirty)
            {
                windShadow.Restart(grid, windLocal, SunOccluders);
                windLitDirty = false;
            }
            else if (!windShadow.IsRunning && windShadow.NeedsRestart(ref windLocal, WindRebuildCosine))
            {
                windShadow.Restart(grid, windLocal, SunOccluders);
            }

            if (windShadow.Step(SunShadowBudget)) BeginWindLit(-1f);


            return StepWindLit(SunLitBudget);
        }


        private int EnvironmentNodeCount
        {
            get { return StepInFlight ? stepNodeCount : nodes.Count; }
        }


        private void BeginWindLit(float fill)
        {
            windLitFill = fill;
            windLitCursor = 0;
            windLitCount = EnvironmentNodeCount;
            windLitPending = true;
        }


        private bool StepWindLit(int nodeBudget)
        {
            if (!windLitPending) return false;

            int count = Math.Min(windLitCount,
                Math.Min(EnvironmentNodeCount, nodeWindLit.Length / Face.Count));
            if (windLitCursor >= count)
            {
                windLitPending = false;
                return false;
            }

            if (nodeBudget <= 0) return false;

            int end = windLitCursor + Math.Min(count - windLitCursor, nodeBudget);

            if (windLitFill >= 0f)
            {
                for (int i = windLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++) nodeWindLit[b + f] = windLitFill;
                }
            }
            else
            {
                for (int i = windLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++)
                    {
                        nodeWindLit[b + f] = windShadow.FaceLitFraction(nodes[i].Block, f);
                    }
                }
            }

            windLitCursor = end;
            if (windLitCursor >= count) windLitPending = false;
            return true;
        }


        private void BeginSunLit(float fill)
        {
            sunLitFill = fill;
            sunLitCursor = 0;
            sunLitCount = EnvironmentNodeCount;
            sunLitPending = true;
        }


        private bool StepSunLit(int nodeBudget)
        {
            if (!sunLitPending) return false;

            int count = Math.Min(sunLitCount,
                Math.Min(EnvironmentNodeCount, nodeSunLit.Length / Face.Count));
            if (sunLitCursor >= count)
            {
                sunLitPending = false;
                return false;
            }

            if (nodeBudget <= 0) return false;
            int end = sunLitCursor + Math.Min(count - sunLitCursor, nodeBudget);

            if (sunLitFill >= 0f)
            {
                for (int i = sunLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++) nodeSunLit[b + f] = sunLitFill;
                }
            }
            else
            {
                for (int i = sunLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++)
                    {
                        nodeSunLit[b + f] = sunShadow.FaceLitFraction(nodes[i].Block, f);
                    }
                }
            }

            sunLitCursor = end;
            if (sunLitCursor >= count) sunLitPending = false;
            return true;
        }


        public void FinishSunLit()
        {
            while (sunLitPending) StepSunLit(int.MaxValue);
        }

        public bool SunLitRefreshPending
        {
            get { return sunLitPending; }
        }

        public SunShadowMap SunShadow { get { return sunShadow; } }

        public SunShadowMap WindShadow { get { return windShadow; } }

        public int WindLitLength { get { return nodeWindLit.Length; } }


        public float SunLitFraction(int node, int face)
        {
            int index = (node * Face.Count) + face;
            return index >= 0 && index < nodeSunLit.Length ? nodeSunLit[index] : 1f;
        }


        private float Weighted(int node, float[] weights)
        {
            int b = node * Face.Count;
            return (nodeFaceWeights[b] * weights[0])
                + (nodeFaceWeights[b + 1] * weights[1])
                + (nodeFaceWeights[b + 2] * weights[2])
                + (nodeFaceWeights[b + 3] * weights[3])
                + (nodeFaceWeights[b + 4] * weights[4])
                + (nodeFaceWeights[b + 5] * weights[5]);
        }
    }
}
