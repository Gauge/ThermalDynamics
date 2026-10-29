using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private void AccumulateConductionRange(float h, int from, int to)
        {
            bool clamp = ConductionClampLive;
            bool diagnostics = diagnosticsSubstep;

            if (!settings.EnableConduction) return;

            float inverseH = h > 0f ? 1f / h : 0f;

            if (to > links.Count) to = links.Count;
            int[] fromIndex = linkA;
            int[] toIndex = linkB;
            float[] conductance = linkConductance;
            float[] massFactor = linkMassFactor;
            float[] temperatures = nodeTemperatures;
            float[] watts = nodeWatts;
            float[] relaxation = nodeRelaxation;

            for (int i = from; i < to; i++)
            {
                int a = fromIndex[i];
                int b = toIndex[i];

                float difference = temperatures[b] - temperatures[a];
                if (difference == 0f) continue;

                float exchange = conductance[i] * difference;

                if (clamp)
                {
                    float scale = relaxation[a] < relaxation[b] ? relaxation[a] : relaxation[b];
                    if (scale < 1f) exchange *= scale;

                    float maxWatts = difference * massFactor[i] * inverseH;
                    if (exchange > 0f)
                    {
                        if (exchange > maxWatts) exchange = maxWatts;
                    }

                    else if (exchange < maxWatts)
                    {
                        exchange = maxWatts;
                    }
                }

                watts[a] += exchange;
                watts[b] -= exchange;

                if (!diagnostics) continue;

                nodes[a].LastConductionWatts += exchange;
                nodes[b].LastConductionWatts -= exchange;
            }
        }


        private void AccumulateLoops(float h)
        {
            bool clamp = ConductionClampLive;

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                loop.ClearSegmentWatts();
                int parcelCount = loop.ParcelCount;
                if (!settings.EnableCoolantLoops || parcelCount <= 0) continue;
                float fill = loop.FillFraction;
                float stagnant = loop.StagnantFactor;

                float relaxation = 1f;
                if (clamp && h > 0f)
                {
                    if (parcelConductanceTotal.Length < parcelCount)
                    {
                        parcelConductanceTotal = new float[parcelCount];
                    }
                    for (int i = 0; i < parcelCount; i++) parcelConductanceTotal[i] = 0f;

                    AccumulateLoopConductanceRange(loop, fill, stagnant, 0, loop.Links.Count);
                    relaxation = LoopRelaxationRange(loop.SegmentThermalMass, h, relaxation, 0, parcelCount);
                }

                AccumulateLoopContactsRange(loop, h, parcelCount, fill, stagnant, relaxation,
                    0, loop.Links.Count);
            }
        }


        private void AccumulateLoopConductanceRange(CoolantLoop loop, float fill, float stagnant,
            int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                LoopLink probe = loop.Links[i];
                if (probe.SegmentIndex < 0 || probe.SegmentIndex >= loop.PipeCount) continue;
                parcelConductanceTotal[loop.ParcelOf(probe.SegmentIndex)] +=
                    probe.Conductance * fill * stagnant;
            }
        }


        private float LoopRelaxationRange(float mass, float h, float relaxation, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                float total = parcelConductanceTotal[i];
                if (total <= 0f || mass <= 0f) continue;
                float stable = mass / (h * total);
                if (stable < relaxation) relaxation = stable;
            }
            return relaxation;
        }


        private void AccumulateLoopContactsRange(CoolantLoop loop, float h, int parcelCount,
            float fill, float stagnant, float relaxation, int from, int to)
        {
            bool clamp = ConductionClampLive;
            float[] watts = loop.SegmentWatts;
            for (int i = from; i < to; i++)
            {
                LoopLink link = loop.Links[i];
                if (link.NodeIndex < 0 || link.NodeIndex >= nodes.Count) continue;
                if (link.SegmentIndex < 0 || link.SegmentIndex >= loop.PipeCount) continue;

                int parcel = loop.ParcelOf(link.SegmentIndex);
                if (parcel >= parcelCount || parcel >= watts.Length) continue;

                float difference = loop.ParcelTemperature(parcel)
                                 - nodeTemperatures[link.NodeIndex];
                float exchange = (link.Conductance * fill * stagnant) * difference;

                if (clamp)
                {

                    exchange = ClampExchange(
                        exchange, h, difference,
                        loop.SegmentThermalMass,
                        nodeThermalMass[link.NodeIndex]);

                    float scale = relaxation;
                    if (nodeRelaxation[link.NodeIndex] < scale) scale = nodeRelaxation[link.NodeIndex];
                    if (scale < 1f) exchange *= scale;
                }

                nodeWatts[link.NodeIndex] += exchange;
                watts[parcel] -= exchange;

                if (exchange < 0f) loop.AbsorbedEnergy -= exchange * h;
                else loop.RejectedEnergy += exchange * h;
            }
        }


        private void AccumulateRoomAir(float h)
        {
            for (int r = 0; r < roomAir.Count; r++) roomWatts[r] = 0f;
            if (!settings.EnableRoomAir) return;

            bool diagnostics = stepCollectDiagnostics;

            if (diagnostics)
            {
                for (int i = 0; i < stepNodeCount; i++)
                {
                    nodes[i].LastRoomWatts = 0f;
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float airTemperature = air.Temperature;

                float roomRelaxation = RoomRelaxation(r, h);

                AccumulateRoomContactsRange(r, h, airTemperature, roomRelaxation, diagnostics,
                    0, air.Links.Count);
            }
        }


        private float RoomRelaxation(int room, float h)
        {
            float relaxation = 1f;
            if (ConductionClampLive && h > 0f)
            {
                float mass = EffectiveRoomMass(room);
                float total = RoomConductance(room);
                if (mass > 0f && total > 0f)
                {
                    float stable = mass / (h * total);
                    if (stable < 1f) relaxation = stable;
                }
            }
            return relaxation;
        }


        private void AccumulateRoomContactsRange(int r, float h, float airTemperature,
            float roomRelaxation, bool diagnostics, int from, int to)
        {
            RoomAirNode air = roomAir[r];
            bool clamp = ConductionClampLive;
            for (int i = from; i < to; i++)
            {
                RoomLink link = air.Links[i];
                if (link.NodeIndex < 0 || link.NodeIndex >= nodes.Count) continue;

                float difference = airTemperature - nodeTemperatures[link.NodeIndex];
                float watts = link.Conductance * difference;

                if (clamp)
                {

                    watts = ClampExchange(
                        watts, h, difference,
                        air.ThermalMass,
                        nodeThermalMass[link.NodeIndex]);

                    float scale = roomRelaxation;
                    if (nodeRelaxation[link.NodeIndex] < scale) scale = nodeRelaxation[link.NodeIndex];
                    if (scale < 1f) watts *= scale;
                }

                nodeWatts[link.NodeIndex] += watts;
                roomWatts[r] -= watts;

                if (diagnostics) nodes[link.NodeIndex].LastRoomWatts += watts;
            }
        }
    }
}
