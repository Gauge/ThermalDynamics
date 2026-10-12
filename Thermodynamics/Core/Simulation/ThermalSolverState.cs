using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        public void RefreshHeatGeneration()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].RefreshHeatGeneration();
            }
        }



        internal void SyncNodeState()
        {
            Work.NodeStateSyncs++;

            bool all = resyncAll;
            resyncAll = false;
            if (all) Work.FullNodeResyncs++;

            if (all) lowestCritical = float.PositiveInfinity;

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];

                float temperature = node.Temperature;
                nodeTemperatures[i] = temperature;
                nodeStepStart[i] = temperature;

                if (!all && !node.StateDirty) continue;
                node.StateDirty = false;

                if (nodeThermalMass[i] != node.ThermalMass) linkMassFactorFrom = 0;

                nodeThermalMass[i] = node.ThermalMass;
                nodeRadiation[i] = node.RadiationCoefficient;
                nodeGeneration[i] = node.HeatGenerationWatts;
                nodeExposedArea[i] = node.ExposedArea;
                nodeAbsorptivity[i] = node.Thermal.EffectiveSolarAbsorptivity;
                nodeCritical[i] = node.Thermal.CriticalTemperature;
                if (nodeCritical[i] > 0f && nodeCritical[i] < lowestCritical)
                {
                    lowestCritical = nodeCritical[i];
                }

                int total = node.TotalExposedFaces;
                nodeExposedFaces[i] = total;

                int b = i * Face.Count;
                if (total <= 0)
                {
                    for (int f = 0; f < Face.Count; f++) nodeFaceWeights[b + f] = 0f;
                    continue;
                }

                float inverse = 1f / total;
                for (int f = 0; f < Face.Count; f++)
                {
                    nodeFaceWeights[b + f] = node.GetExposedFaces(f) * inverse;
                }
            }
        }

        private const float ClampBindingMargin = 0.9999f;


        private bool ClampCanBind(float h)
        {
            if (h <= 0f) return false;

            int nodeCount = nodes.Count;
            if (nodeCount > nodeConductanceTotal.Length) nodeCount = nodeConductanceTotal.Length;
            if (nodeCount > nodeThermalMass.Length) nodeCount = nodeThermalMass.Length;

            for (int i = 0; i < nodeCount; i++)
            {
                if (h * nodeConductanceTotal[i] >= ClampBindingMargin * nodeThermalMass[i]) return true;
            }

            int linkCount = links.Count;
            if (linkCount > linkConductance.Length) linkCount = linkConductance.Length;
            if (linkCount > linkMassFactor.Length) linkCount = linkMassFactor.Length;

            for (int i = 0; i < linkCount; i++)
            {
                if (h * linkConductance[i] >= ClampBindingMargin * linkMassFactor[i]) return true;
            }

            for (int l = 0; l < loops.Count; l++)
            {
                if (h * SegmentConductance(l) >= ClampBindingMargin * EffectiveLoopMass(l)) return true;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                if (!roomAir[r].HasAir) continue;
                if (h * RoomConductance(r) >= ClampBindingMargin * EffectiveRoomMass(r)) return true;
            }

            return false;
        }


        private void RefreshLinkMassFactors()
        {
            int from = linkMassFactorFrom;
            if (from >= links.Count) return;
            linkMassFactorFrom = int.MaxValue;

            if (linkMassFactor.Length < links.Count)
            {
                Array.Resize(ref linkMassFactor, Math.Max(16, links.Count * 2));
            }

            for (int i = from; i < links.Count; i++)
            {
                float massA = nodeThermalMass[links[i].NodeA];
                float massB = nodeThermalMass[links[i].NodeB];
                float combined = massA + massB;
                linkMassFactor[i] = combined <= 0f ? 0f : (massA * massB) / combined;
            }
        }


        private void PublishOverheats()
        {
            for (int i = 0; i < overheated.Count; i++)
            {
                int index = overheated[i];
                overheats.Add(new OverheatEvent(
                    nodes[index].Block, nodeOverheatPeak[index], nodeOverheatDamage[index]));
            }
        }


        private void ClearOverheatAccumulator()
        {
            for (int i = 0; i < overheated.Count; i++)
            {
                nodeOverheatDamage[overheated[i]] = 0f;
                nodeOverheatPeak[overheated[i]] = 0f;
            }

            overheated.Clear();
        }


        private void ClearEnvironmentDiagnostics()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                ClearEnvironmentDiagnostics(i);
            }
        }


        private void ClearEnvironmentDiagnostics(int i)
        {
            ThermalNode node = nodes[i];
            node.LastRadiationWatts = 0f;
            node.LastConvectionWatts = 0f;
            node.LastSolarWatts = 0f;
            node.LastFrictionWatts = 0f;
            node.LastHeatSourceWatts = 0f;
        }


        private void ClearConductionDiagnostics()
        {
            if (!diagnosticsSubstep) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].LastConductionWatts = 0f;
            }
        }


        public static float ClampRelaxation(float watts, float wattsToEquilibrium)
        {
            if (watts > 0f)
            {
                if (wattsToEquilibrium <= 0f) return 0f;
                return watts > wattsToEquilibrium ? wattsToEquilibrium : watts;
            }

            if (watts < 0f)
            {
                if (wattsToEquilibrium >= 0f) return 0f;
                return watts < wattsToEquilibrium ? wattsToEquilibrium : watts;
            }

            return 0f;
        }


        public static float ClampExchange(float watts, float h, float difference, float massA, float massB)
        {
            if (h <= 0f) return watts;

            float combined = massA + massB;
            if (combined <= 0f) return watts;

            float maxEnergy = difference * (massA * massB / combined);
            float energy = watts * h;

            if (Math.Abs(energy) <= Math.Abs(maxEnergy)) return watts;
            return maxEnergy / h;
        }


        private void ApplyNodeWattsRange(float h, int from, int to)
        {
            bool damageEnabled = settings.EnableDamage;

            bool perSecond = settings.DamageIsPerSecond;

            for (int i = from; i < to; i++)
            {
                float previous = nodeTemperatures[i];
                float updated = previous + (nodeWatts[i] * h / nodeThermalMass[i]);
                if (updated < ThermalConstants.MinimumTemperature)
                {
                    updated = ThermalConstants.MinimumTemperature;
                }

                nodeTemperatures[i] = updated;

                if (!damageEnabled) continue;

                if (updated <= lowestCritical) continue;

                float critical = nodeCritical[i];
                if (critical <= 0f || updated <= critical) continue;

                float damage = (updated - critical) * nodes[i].Thermal.OverheatDamagePerKelvin;
                if (perSecond) damage *= h;
                if (damage <= 0f) continue;

                if (nodeOverheatDamage[i] == 0f) overheated.Add(i);
                nodeOverheatDamage[i] += damage;
                if (updated > nodeOverheatPeak[i]) nodeOverheatPeak[i] = updated;
            }
        }


        private void ApplyCoupledWatts(float h)
        {
            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];

                float mass = EffectiveLoopMass(l);
                float[] watts = loop.SegmentWatts;

                for (int i = 0; i < watts.Length; i++)
                {
                    loop.ApplyParcelWatts(i, watts[i], h, mass);
                }

                loop.Advect(h);
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float updated = air.Temperature + (roomWatts[r] * h / EffectiveRoomMass(r));
                air.Temperature = updated < ThermalConstants.MinimumTemperature
                    ? ThermalConstants.MinimumTemperature
                    : updated;
            }
        }



        private bool EnsureBuffers()
        {
            bool grew = false;

            if (nodeWatts.Length < nodes.Count)
            {
                grew = true;

                AbandonStep();

                conductanceTotalsDirty = true;

                int size = Math.Max(16, nodes.Count + (nodes.Count / 4) + 16);
                nodeWatts = new float[size];
                nodeTemperatures = new float[size];
                nodeStepStart = new float[size];
                nodeConductanceTotal = new float[size];
                resyncAll = true;
                sunLitDirty = true;
            windLitDirty = true;
                nodeThermalMass = new float[size];
                nodeRadiation = new float[size];
                nodeGeneration = new float[size];
                nodeExposedArea = new float[size];
                nodeAbsorptivity = new float[size];
                nodeCritical = new float[size];
                nodeExposedFaces = new int[size];
                nodeRelaxation = new float[size];
                nodeSolarRow = new float[size];
                nodeFrictionRow = new float[size];
                nodeConvectionRow = new float[size];
                nodeSourceRow = new float[size];
                nodeOverheatDamage = new float[size];
                nodeOverheatPeak = new float[size];
                InvalidateEnvironmentRows();
                nodeFaceWeights = new float[size * Face.Count];
                nodeSunLit = new float[size * Face.Count];

                nodeWindLit = settings.EnableWindwardShielding
                    ? new float[size * Face.Count]
                    : new float[0];

            }
            if (loopEffectiveMass.Length < loops.Count)
            {
                loopEffectiveMass = new float[Math.Max(4, loops.Count * 2)];
            }
            if (roomWatts.Length < roomAir.Count)
            {
                roomWatts = new float[Math.Max(4, roomAir.Count * 2)];
                roomEffectiveMass = new float[roomWatts.Length];
            }

            return grew;
        }


        public float TotalEnergy
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < nodes.Count; i++)
                {
                    total += nodes[i].Energy;
                }
                for (int i = 0; i < loops.Count; i++)
                {
                    total += loops[i].Energy;
                }
                for (int i = 0; i < roomAir.Count; i++)
                {
                    if (roomAir[i].HasAir) total += roomAir[i].Energy;
                }
                return total;
            }
        }

        private int hottestNode = -1;


        public ThermalNode HottestNode()
        {
            if (hottestNode < 0 || hottestNode >= nodes.Count)
            {
                ThermalNode found = null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (found == null || nodes[i].Temperature > found.Temperature) found = nodes[i];
                }
                return found;
            }

            return nodes[hottestNode];
        }


        public void SetAllTemperatures(float kelvin)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Temperature = kelvin;
            }
            for (int i = 0; i < loops.Count; i++)
            {
                loops[i].Temperature = kelvin;
            }
        }
    }
}
