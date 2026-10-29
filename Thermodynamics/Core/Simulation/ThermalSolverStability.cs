using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        public float RequiredSubsteps(float deltaSeconds)
        {
            // Queries during a spread step must observe its scratch, not overwrite it from
            // public temperatures that have not been published yet.
            if (StepInFlight) return RequiredSubstepsFromState(deltaSeconds);
            float worstNodeRate = PrepareStepState(true);

            return RequiredSubstepsFromState(deltaSeconds, worstNodeRate);
        }


        public float RequiredSubsteps(float deltaSeconds, EnvironmentState environment)
        {
            if (StepInFlight) return RequiredSubstepsFromState(deltaSeconds, -1f, ref environment);
            Environment = environment;

            return RequiredSubsteps(deltaSeconds);
        }


        private float PrepareStepState(bool estimateStability)
        {
            RebuildLinksIfNeeded();
            EnsureBuffers();
            if (estimateStability)
            {
                // Contact totals do not read mirrored node properties. Make them current before
                // copying each node so its immediately following stability visit can be fused.
                RecomputeConductanceTotalsIfNeeded();
                return SyncNodeStateAndEstimate();
            }
            SyncNodeState();
            RecomputeConductanceTotalsIfNeeded();
            return ApplyThermalMassFloor();
        }

        private struct StabilityTerms
        {
            public bool Exposed;
            public bool Radiating;
            public float Convection;
        }


        private StabilityTerms StabilityEnvironment()
        {
            EnvironmentState environment = Environment;

            return StabilityEnvironment(ref environment);
        }


        private StabilityTerms StabilityEnvironment(ref EnvironmentState environment)
        {

            StabilityTerms terms = new StabilityTerms();

            terms.Radiating = settings.EnableEnvironment && settings.EnableRadiation;
            bool convecting = settings.EnableEnvironment && settings.EnableConvection;
            terms.Convection = convecting
                ? environment.ConvectionCoefficient * environment.AtmosphereFactor
                : 0f;
            terms.Exposed = terms.Radiating || convecting;
            return terms;
        }


        private float NodeStabilityRate(int i, ref StabilityTerms terms)
        {
            float rate = nodeConductanceTotal[i];

            if (!terms.Exposed || nodeExposedFaces[i] <= 0) return rate;

            if (terms.Radiating)
            {
                float t = nodeTemperatures[i];
                rate += 4f * nodeRadiation[i] * t * t * t;
            }

            return rate + (terms.Convection * nodeExposedArea[i]);
        }


        private float RequiredSubstepsFromState(float deltaSeconds)
        {
            return RequiredSubstepsFromState(deltaSeconds, -1f);
        }

        private float RequiredSubstepsFromState(float deltaSeconds, float worstNodeRate)
        {
            EnvironmentState environment = Environment;
            return RequiredSubstepsFromState(deltaSeconds, worstNodeRate, ref environment);
        }

        private float RequiredSubstepsFromState(float deltaSeconds, float worstNodeRate,
            ref EnvironmentState environment)
        {
            Work.StabilityEstimates++;

            float worst = worstNodeRate;
            if (worst < 0f)
            {
                worst = 0f;
                StabilityTerms terms = StabilityEnvironment(ref environment);
                int count = StepInFlight ? stepNodeCount : nodes.Count;
                for (int i = 0; i < count; i++)
                {
                    float perNode = NodeStabilityRate(i, ref terms) / nodeThermalMass[i];
                    if (perNode > worst) worst = perNode;
                }
            }

            for (int l = 0; l < loops.Count; l++)
            {

                float perLoop = SegmentConductance(l) / EffectiveLoopMass(l);
                if (perLoop > worst) worst = perLoop;

            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                if (!roomAir[r].HasAir) continue;


                float perRoom = RoomConductance(r) / EffectiveRoomMass(r);
                if (perRoom > worst) worst = perRoom;
            }

            if (worst <= 0f) return 1f;
            return (deltaSeconds * worst) / StabilitySafetyFactor;
        }

        public class SubstepProfile
        {
            public static readonly float[] DemandEdges =
                { 0.5f, 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f };

            public static readonly int[] ProjectedCaps = { 32, 16, 8, 6, 4, 3, 2, 1 };

            public float StepSeconds;
            public int Nodes;
            public int Links;

            public float RequiredSubsteps;

            public float RequiredSubstepsInForce;

            public float WorstNodeDemand;
            public int WorstNodeIndex = -1;

            public float WorstNodeConductionShare;

            public float WorstRoomAirDemand;
            public int WorstRoomAirIndex = -1;
            public float WorstLoopDemand;
            public int WorstLoopIndex = -1;

            public readonly long[] Buckets = new long[DemandEdges.Length + 1];

            public readonly long[] CapNodesFloored = new long[ProjectedCaps.Length];
            public readonly float[] CapRequiredSubsteps = new float[ProjectedCaps.Length];

            public long EnvironmentDominatedNodes;
        }


        public void PrepareForSteps()
        {
            if (StepInFlight) return;
            PrepareStepState(false);

            RefreshLinkMassFactors();
        }


        public SubstepProfile ProfileSubsteps()
        {
            if (!StepInFlight)
            {
                RebuildLinksIfNeeded();
                EnsureBuffers();
                SyncNodeState();
                RecomputeConductanceTotalsIfNeeded();
                ApplyThermalMassFloor();
            }


            SubstepProfile profile = new SubstepProfile();
            profile.StepSeconds = settings.StepSeconds;
            profile.Links = links.Count;

            float scale = settings.StepSeconds / StabilitySafetyFactor;

            StabilityTerms terms = StabilityEnvironment();

            float[] edges = SubstepProfile.DemandEdges;
            int[] caps = SubstepProfile.ProjectedCaps;

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                float demand = loop.ThermalMass <= 0f
                    ? 0f
                    : (LoopConductance(l) / loop.ThermalMass) * scale;

                if (demand > profile.WorstLoopDemand)
                {
                    profile.WorstLoopDemand = demand;
                    profile.WorstLoopIndex = l;
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float demand = air.ThermalMass <= 0f
                    ? 0f
                    : (RoomConductance(r) / air.ThermalMass) * scale;

                if (demand > profile.WorstRoomAirDemand)
                {
                    profile.WorstRoomAirDemand = demand;
                    profile.WorstRoomAirIndex = r;
                }
            }

            float coupled = profile.WorstLoopDemand > profile.WorstRoomAirDemand
                ? profile.WorstLoopDemand
                : profile.WorstRoomAirDemand;

            for (int c = 0; c < caps.Length; c++)
            {
                profile.CapNodesFloored[c] = 0;
                profile.CapRequiredSubsteps[c] = coupled > caps[c] ? caps[c] : coupled;
            }

            int count = nodes.Count;
            if (count > nodeConductanceTotal.Length) count = nodeConductanceTotal.Length;
            profile.Nodes = count;

            for (int i = 0; i < count; i++)
            {

                float rate = NodeStabilityRate(i, ref terms);
                float conduction = nodeConductanceTotal[i];

                float capacity = nodes[i].ThermalMass;
                float demand = capacity <= 0f ? 0f : (rate / capacity) * scale;

                if (demand > profile.WorstNodeDemand)
                {
                    profile.WorstNodeDemand = demand;
                    profile.WorstNodeIndex = i;
                    profile.WorstNodeConductionShare = rate <= 0f ? 0f : conduction / rate;
                }

                if (rate > 0f && conduction < rate * 0.5f && demand > 1f)
                {
                    profile.EnvironmentDominatedNodes++;
                }

                int bucket = edges.Length;
                for (int e = 0; e < edges.Length; e++)
                {
                    if (demand < edges[e]) { bucket = e; break; }
                }
                profile.Buckets[bucket]++;

                for (int c = 0; c < caps.Length; c++)
                {
                    float capped = demand > caps[c] ? caps[c] : demand;
                    if (demand > caps[c]) profile.CapNodesFloored[c]++;
                    if (capped > profile.CapRequiredSubsteps[c]) profile.CapRequiredSubsteps[c] = capped;
                }
            }

            float worst = profile.WorstNodeDemand > coupled ? profile.WorstNodeDemand : coupled;
            profile.RequiredSubsteps = worst <= 0f ? 1f : worst;


            profile.RequiredSubstepsInForce = RequiredSubstepsFromState(settings.StepSeconds);

            return profile;
        }


        private int ClampSubsteps(float required)
        {
            if (!(required > 1f)) return 1;
            if (required >= MaxSubsteps) return MaxSubsteps;

            // Bound finite and infinite demand before the runtime-dependent out-of-range cast.
            return (int)Math.Ceiling(required);
        }

        private bool conductanceTotalsDirty = true;
        // Incremental topology edits must update the mechanisms represented by the cached sum,
        // even when settings changed before the next stability preparation.
        private bool conductanceIncludesBlocks, conductanceIncludesLoops, conductanceIncludesRooms;


        private void RecomputeConductanceTotalsIfNeeded()
        {
            if (!conductanceTotalsDirty
                && conductanceIncludesBlocks == settings.EnableConduction
                && conductanceIncludesLoops == settings.EnableCoolantLoops
                && conductanceIncludesRooms == settings.EnableRoomAir) return;
            RecomputeConductanceTotals();
        }

        public int AdaptiveSubstepFloor;

        public int EffectiveSubstepFloor
        {
            get
            {
                int world = settings.MaxSubstepsPerBlock;
                int grid = AdaptiveSubstepFloor;

                if (world <= 0) return grid <= 0 ? 0 : grid;
                if (grid <= 0) return world;

                return world < grid ? world : grid;
            }
        }


        private float ApplyThermalMassFloor()
        {
            int previousFloored = FlooredNodes;
            FlooredNodes = 0;

            int cap = EffectiveSubstepFloor;
            float step = settings.StepSeconds;
            if (cap <= 0 || step <= 0f)
            {
                // Clean nodes do not pass through SyncNodeState's capacity refresh on a toggle.
                if (previousFloored > 0)
                {
                    for (int i = 0; i < nodes.Count; i++) nodeThermalMass[i] = nodes[i].ThermalMass;
                    linkMassFactorFrom = 0;
                }
                return -1f;
            }

            float perRate = step / (StabilitySafetyFactor * cap);
            bool moved = false;
            float worst = 0f;


            StabilityTerms terms = StabilityEnvironment();

            int count = nodes.Count;
            for (int i = 0; i < count; i++)
            {
                float real = nodes[i].ThermalMass;

                float rate = NodeStabilityRate(i, ref terms);
                float floor = rate * perRate;
                float wanted = real < floor ? floor : real;

                if (nodeThermalMass[i] != wanted)
                {
                    nodeThermalMass[i] = wanted;
                    moved = true;
                }

                if (wanted > real) FlooredNodes++;

                // Reuse this exact rate instead of scanning every node again for stability.
                float demand = rate / nodeThermalMass[i];
                if (demand > worst) worst = demand;
            }

            if (moved) linkMassFactorFrom = 0;

            ApplyCoupledMassFloors(perRate);
            return worst;
        }


        private float SyncNodeStateAndEstimate()
        {
            bool all = BeginNodeStateSync();
            int previousFloored = FlooredNodes;
            FlooredNodes = 0;

            int cap = EffectiveSubstepFloor;
            float step = settings.StepSeconds;
            bool flooring = !(cap <= 0 || step <= 0f);
            bool restore = !flooring && previousFloored > 0;
            float perRate = flooring ? step / (StabilitySafetyFactor * cap) : 0f;
            bool moved = restore;
            float worst = 0f;
            StabilityTerms terms = StabilityEnvironment();

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];
                float temperature = node.Temperature;
                nodeTemperatures[i] = temperature;
                nodeStepStart[i] = temperature;
                if (all || node.StateDirty) SyncNodeProperties(i, node);

                float rate = NodeStabilityRate(i, ref terms);
                if (flooring)
                {
                    float real = node.ThermalMass;
                    float floor = rate * perRate;
                    float wanted = real < floor ? floor : real;
                    if (nodeThermalMass[i] != wanted)
                    {
                        nodeThermalMass[i] = wanted;
                        moved = true;
                    }
                    if (wanted > real) FlooredNodes++;
                }
                else if (restore) nodeThermalMass[i] = node.ThermalMass;

                float demand = rate / nodeThermalMass[i];
                if (demand > worst) worst = demand;
            }

            if (moved) linkMassFactorFrom = 0;
            if (flooring) ApplyCoupledMassFloors(perRate);
            return worst;
        }


        private void ApplyCoupledMassFloors(float perRate)
        {
            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];

                float floor = SegmentConductance(l) * perRate;
                loopEffectiveMass[l] = loop.SegmentThermalMass < floor ? floor : loop.SegmentThermalMass;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];

                float floor = RoomConductance(r) * perRate;
                roomEffectiveMass[r] = air.ThermalMass < floor ? floor : air.ThermalMass;
            }
        }

        private float[] loopConductanceTotal = new float[0];
        private float[] roomConductanceTotal = new float[0];


        private float LoopConductance(int index)
        {
            if (!settings.EnableCoolantLoops) return 0f;
            return index >= 0 && index < loopConductanceTotal.Length ? loopConductanceTotal[index] : 0f;
        }


        private float SegmentConductance(int index)
        {
            if (!settings.EnableCoolantLoops) return 0f;
            if (index < 0 || index >= loops.Count) return 0f;

            CoolantLoop loop = loops[index];
            int count = loop.ParcelCount;
            if (count <= 0 || loop.Links.Count == 0) return 0f;
            float fill = loop.FillFraction;
            float stagnant = loop.StagnantFactor;

            if (segmentConductanceScratch.Length < count)
            {
                segmentConductanceScratch = new float[Math.Max(16, count * 2)];
            }
            Array.Clear(segmentConductanceScratch, 0, count);

            for (int i = 0; i < loop.Links.Count; i++)
            {
                LoopLink link = loop.Links[i];
                if (link.SegmentIndex < 0 || link.SegmentIndex >= loop.PipeCount) continue;

                int parcel = loop.ParcelOf(link.SegmentIndex);
                if (parcel < 0 || parcel >= count) continue;

                segmentConductanceScratch[parcel] += (link.Conductance * fill) * stagnant;
            }

            float worst = 0f;
            for (int i = 0; i < count; i++)
            {
                if (segmentConductanceScratch[i] > worst) worst = segmentConductanceScratch[i];
            }
            return worst;
        }

        private float[] segmentConductanceScratch = new float[0];


        private float RoomConductance(int index)
        {
            if (!settings.EnableRoomAir) return 0f;
            return index >= 0 && index < roomConductanceTotal.Length ? roomConductanceTotal[index] : 0f;
        }


        private void RecomputeCoupledConductance()
        {
            if (loopConductanceTotal.Length < loops.Count)
            {
                loopConductanceTotal = new float[Math.Max(4, loops.Count * 2)];
            }

            if (roomConductanceTotal.Length < roomAir.Count)
            {
                roomConductanceTotal = new float[Math.Max(4, roomAir.Count * 2)];
            }

            for (int l = 0; l < loops.Count; l++)
            {
                if (!settings.EnableCoolantLoops)
                {
                    loopConductanceTotal[l] = 0f;
                    continue;
                }
                CoolantLoop loop = loops[l];
                if (loop.Links.Count == 0)
                {
                    loopConductanceTotal[l] = 0f;
                    continue;
                }
                float fill = loop.FillFraction;
                float stagnant = loop.StagnantFactor;
                float total = 0f;
                for (int i = 0; i < loop.Links.Count; i++)
                    total += (loop.Links[i].Conductance * fill) * stagnant;
                loopConductanceTotal[l] = total;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!settings.EnableRoomAir || !air.HasAir)
                {
                    roomConductanceTotal[r] = 0f;
                    continue;
                }

                float total = 0f;
                for (int i = 0; i < air.Links.Count; i++) total += air.Links[i].Conductance;
                roomConductanceTotal[r] = total;
            }
        }


        private float EffectiveLoopMass(int index)
        {
            if (EffectiveSubstepFloor <= 0 || index >= loopEffectiveMass.Length
                || loopEffectiveMass[index] <= 0f)
            {
                return loops[index].SegmentThermalMass;
            }

            return loopEffectiveMass[index];
        }


        private float EffectiveRoomMass(int index)
        {
            if (EffectiveSubstepFloor <= 0 || index >= roomEffectiveMass.Length
                || roomEffectiveMass[index] <= 0f)
            {
                return roomAir[index].ThermalMass;
            }

            return roomEffectiveMass[index];
        }

        public int FlooredNodes { get; private set; }


        private void RecomputeConductanceTotals()
        {
            Work.ConductanceRecomputes++;
            conductanceTotalsDirty = false;
            conductanceIncludesBlocks = settings.EnableConduction;
            conductanceIncludesLoops = settings.EnableCoolantLoops;
            conductanceIncludesRooms = settings.EnableRoomAir;
            EnsureBuffers();
            RecomputeCoupledConductance();
            for (int i = 0; i < nodes.Count; i++)
            {
                nodeConductanceTotal[i] = 0f;
            }

            int blockLinks = conductanceIncludesBlocks ? links.Count : 0;
            for (int i = 0; i < blockLinks; i++)
            {
                ThermalLink link = links[i];
                nodeConductanceTotal[link.NodeA] += link.Conductance;
                nodeConductanceTotal[link.NodeB] += link.Conductance;
            }

            int loopCount = conductanceIncludesLoops ? loops.Count : 0;
            for (int l = 0; l < loopCount; l++)
            {
                CoolantLoop loop = loops[l];
                for (int i = 0; i < loop.Links.Count; i++)
                {
                    LoopLink link = loop.Links[i];
                    if (link.NodeIndex >= 0 && link.NodeIndex < nodeConductanceTotal.Length)
                    {
                        nodeConductanceTotal[link.NodeIndex] += link.Conductance;
                    }
                }
            }

            int roomCount = conductanceIncludesRooms ? roomAir.Count : 0;
            for (int r = 0; r < roomCount; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                for (int i = 0; i < air.Links.Count; i++)
                {
                    RoomLink link = air.Links[i];
                    if (link.NodeIndex >= 0 && link.NodeIndex < nodeConductanceTotal.Length)
                    {
                        nodeConductanceTotal[link.NodeIndex] += link.Conductance;
                    }
                }
            }
        }
    }
}
