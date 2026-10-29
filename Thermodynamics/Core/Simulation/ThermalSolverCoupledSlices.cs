using System;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private enum CoupledStage
        {
            Loop,
            ClearParcelWatts,
            ClearParcelConductance,
            ParcelConductance,
            ParcelRelaxation,
            LoopContacts,
            RoomDiagnostics,
            Room,
            RoomContacts,
            Pumps
        }

        private CoupledStage coupledStage;
        private int coupledDeviceCursor, coupledItemCursor;
        private int coupledParcels;
        private float coupledFill, coupledStagnant, coupledRelaxation, coupledAirTemperature;
        private bool coupledStarted, coupledDiagnostics;
        private long stepCoupledWork;

        private long CoupledSubstepWork()
        {
            long work = loops.Count + roomAir.Count + (long)heatPumps.Count;
            for (int l = 0; l < loops.Count; l++) work += loops[l].ParcelCount;
            if (settings.EnableCoolantLoops)
            {
                for (int l = 0; l < loops.Count; l++)
                {
                    CoolantLoop loop = loops[l];
                    if (loop.ParcelCount <= 0) continue;
                    work += loop.Links.Count;
                    if (ConductionClampLive && substepSeconds > 0f)
                        work += loop.Links.Count + 2L * loop.ParcelCount;
                }
            }
            if (settings.EnableRoomAir)
            {
                if (stepCollectDiagnostics) work += stepNodeCount;
                for (int r = 0; r < roomAir.Count; r++)
                    if (roomAir[r].HasAir) work += roomAir[r].Links.Count;
            }
            return work;
        }

        private void ResetCoupledCursor()
        {
            coupledStarted = false;
            coupledStage = CoupledStage.Loop;
            coupledDeviceCursor = coupledItemCursor = 0;
        }

        private void FinishCoupled()
        {
            stage = StepStage.Apply;
            stageCursor = 0;
        }

        private int CoupledEnd(int count, long budget)
        {
            long remaining = count - coupledItemCursor;
            return coupledItemCursor + (int)(budget < remaining ? budget : remaining);
        }

        private long AdvanceCoupled(long budget)
        {
            if (!coupledStarted)
            {
                coupledStarted = true;
                coupledDiagnostics = stepCollectDiagnostics;
                // Ordinary whole steps keep their direct loops. Sliced steps execute the same
                // range helpers and arithmetic order, with no state switch inside a contact loop.
                if (budget >= stepCoupledWork)
                {
                    AccumulateLoops(substepSeconds);
                    AccumulateRoomAir(substepSeconds);
                    AccumulateHeatPumps(substepSeconds);
                    FinishCoupled();
                    return stepCoupledWork;
                }
            }

            long spent = 0;
            while (spent < budget)
            {
                int end;
                long remaining = budget - spent;
                switch (coupledStage)
                {
                    case CoupledStage.Loop:
                        if (coupledDeviceCursor >= loops.Count)
                        {
                            coupledStage = CoupledStage.RoomDiagnostics;
                            coupledDeviceCursor = coupledItemCursor = 0;
                            continue;
                        }
                        spent++; // Inspect each device even if disabled or empty.
                        CoolantLoop loop = loops[coupledDeviceCursor];
                        coupledParcels = loop.ParcelCount;
                        loop.EnsureSegmentWatts();
                        coupledStage = CoupledStage.ClearParcelWatts;
                        coupledItemCursor = 0;
                        break;

                    case CoupledStage.ClearParcelWatts:
                        loop = loops[coupledDeviceCursor];
                        end = CoupledEnd(coupledParcels, remaining);
                        Array.Clear(loop.SegmentWatts, coupledItemCursor, end - coupledItemCursor);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end < coupledParcels) break;
                        if (!settings.EnableCoolantLoops || coupledParcels <= 0)
                        {
                            coupledStage = CoupledStage.Loop;
                            coupledDeviceCursor++;
                            coupledItemCursor = 0;
                            continue;
                        }
                        coupledFill = loop.FillFraction;
                        coupledStagnant = loop.StagnantFactor;
                        coupledRelaxation = 1f;
                        coupledItemCursor = 0;
                        if (ConductionClampLive && substepSeconds > 0f)
                        {
                            if (parcelConductanceTotal.Length < coupledParcels)
                                parcelConductanceTotal = new float[coupledParcels];
                            coupledStage = CoupledStage.ClearParcelConductance;
                        }
                        else coupledStage = CoupledStage.LoopContacts;
                        break;

                    case CoupledStage.ClearParcelConductance:
                        end = CoupledEnd(coupledParcels, remaining);
                        Array.Clear(parcelConductanceTotal, coupledItemCursor, end - coupledItemCursor);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= coupledParcels)
                        {
                            coupledStage = CoupledStage.ParcelConductance;
                            coupledItemCursor = 0;
                        }
                        break;

                    case CoupledStage.ParcelConductance:
                        loop = loops[coupledDeviceCursor];
                        end = CoupledEnd(loop.Links.Count, remaining);
                        AccumulateLoopConductanceRange(loop, coupledFill, coupledStagnant,
                            coupledItemCursor, end);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= loop.Links.Count)
                        {
                            coupledStage = CoupledStage.ParcelRelaxation;
                            coupledItemCursor = 0;
                        }
                        break;

                    case CoupledStage.ParcelRelaxation:
                        loop = loops[coupledDeviceCursor];
                        end = CoupledEnd(coupledParcels, remaining);
                        coupledRelaxation = LoopRelaxationRange(loop.SegmentThermalMass,
                            substepSeconds, coupledRelaxation, coupledItemCursor, end);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= coupledParcels)
                        {
                            coupledStage = CoupledStage.LoopContacts;
                            coupledItemCursor = 0;
                        }
                        break;

                    case CoupledStage.LoopContacts:
                        loop = loops[coupledDeviceCursor];
                        end = CoupledEnd(loop.Links.Count, remaining);
                        AccumulateLoopContactsRange(loop, substepSeconds, coupledParcels, coupledFill,
                            coupledStagnant, coupledRelaxation, coupledItemCursor, end);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= loop.Links.Count)
                        {
                            coupledStage = CoupledStage.Loop;
                            coupledDeviceCursor++;
                            coupledItemCursor = 0;
                        }
                        break;

                    case CoupledStage.RoomDiagnostics:
                        if (settings.EnableRoomAir && coupledDiagnostics)
                        {
                            end = CoupledEnd(stepNodeCount, remaining);
                            for (int i = coupledItemCursor; i < end; i++) nodes[i].LastRoomWatts = 0f;
                            spent += end - coupledItemCursor;
                            coupledItemCursor = end;
                            if (end < stepNodeCount) break;
                        }
                        coupledStage = CoupledStage.Room;
                        coupledItemCursor = 0;
                        break;

                    case CoupledStage.Room:
                        if (coupledDeviceCursor >= roomAir.Count)
                        {
                            coupledStage = CoupledStage.Pumps;
                            coupledItemCursor = 0;
                            continue;
                        }
                        spent++;
                        RoomAirNode air = roomAir[coupledDeviceCursor];
                        roomWatts[coupledDeviceCursor] = 0f;
                        if (!settings.EnableRoomAir || !air.HasAir)
                        {
                            coupledDeviceCursor++;
                            continue;
                        }
                        coupledAirTemperature = air.Temperature;
                        coupledRelaxation = RoomRelaxation(coupledDeviceCursor, substepSeconds);
                        coupledStage = CoupledStage.RoomContacts;
                        coupledItemCursor = 0;
                        break;

                    case CoupledStage.RoomContacts:
                        air = roomAir[coupledDeviceCursor];
                        end = CoupledEnd(air.Links.Count, remaining);
                        AccumulateRoomContactsRange(coupledDeviceCursor, substepSeconds,
                            coupledAirTemperature, coupledRelaxation, coupledDiagnostics,
                            coupledItemCursor, end);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= air.Links.Count)
                        {
                            coupledStage = CoupledStage.Room;
                            coupledDeviceCursor++;
                            coupledItemCursor = 0;
                        }
                        break;

                    default:
                        end = CoupledEnd(heatPumps.Count, remaining);
                        AccumulateHeatPumpRange(substepSeconds, coupledItemCursor, end);
                        spent += end - coupledItemCursor;
                        coupledItemCursor = end;
                        if (end >= heatPumps.Count)
                        {
                            FinishCoupled();
                            return spent;
                        }
                        break;
                }
            }
            return spent;
        }
    }
}
