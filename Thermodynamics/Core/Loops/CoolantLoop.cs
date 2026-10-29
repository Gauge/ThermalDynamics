using System;
using System.Collections.Generic;

namespace Thermodynamics.Core
{
    public struct LoopLink
    {
        public int NodeIndex;
        public float Conductance;

        public int SegmentIndex;


        public LoopLink(int nodeIndex, float conductance, int segmentIndex)
        {
            NodeIndex = nodeIndex;
            Conductance = conductance;
            SegmentIndex = segmentIndex;
        }
    }

    public class CoolantLoop
    {

        public readonly List<BlockInstance> Pipes = new List<BlockInstance>();


        public readonly List<LoopLink> Links = new List<LoopLink>();

        public LoopThermalProperties Properties;

        public float Temperature
        {
            get
            {
                if (segments == null || segmentCount == 0) return seeded;

                float total = 0f;
                for (int i = 0; i < segmentCount; i++) total += segments[i];
                return total / segmentCount;
            }
            set
            {
                seeded = value;
                for (int i = 0; i < segmentCount; i++) segments[i] = value;
            }
        }


        public float SegmentTemperature(int index)
        {
            if (segments == null || segmentCount == 0) return seeded;
            if (index < 0 || index >= Pipes.Count) return seeded;
            return segments[ParcelOf(index)];
        }


        public int ParcelOf(int pipeIndex)
        {
            if (segmentCount <= 1) return 0;

            int slot = pipeIndex - shift;
            // A valid pipe and the ring origin differ by less than one circuit.
            if ((uint)pipeIndex < (uint)segmentCount)
                return slot < 0 ? slot + segmentCount : slot;

            slot %= segmentCount;
            return slot < 0 ? slot + segmentCount : slot;
        }

        internal float ParcelTemperature(int parcel)
        {
            return segments[parcel];
        }

        public int ParcelCount
        {
            get { return segmentCount; }
        }

        private int shift;

        private float travelled;


        public void SetSegmentTemperature(int pipeIndex, float temperature)
        {
            if (segments == null || segmentCount == 0) return;
            if (pipeIndex < 0 || pipeIndex >= Pipes.Count) return;

            segments[ParcelOf(pipeIndex)] = temperature < ThermalConstants.MinimumTemperature
                ? ThermalConstants.MinimumTemperature
                : temperature;
        }

        public float HottestSegment
        {
            get
            {
                if (segments == null || segmentCount == 0) return seeded;

                float worst = segments[0];
                for (int i = 1; i < segmentCount; i++)
                {
                    if (segments[i] > worst) worst = segments[i];
                }
                return worst;
            }
        }

        public float ColdestSegment
        {
            get
            {
                if (segments == null || segmentCount == 0) return seeded;

                float best = segments[0];
                for (int i = 1; i < segmentCount; i++)
                {
                    if (segments[i] < best) best = segments[i];
                }
                return best;
            }
        }

        private float[] segments = new float[0];
        private int segmentCount;

        private float seeded;

        public float ThermalMass { get; private set; }

        public float SegmentThermalMass { get; private set; }

        public float FillFraction
        {
            get { return fill; }
            set
            {
                float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
                if (clamped == fill) return;

                fill = clamped;
                RefreshThermalMass();
            }
        }

        private float fill = 1f;

        public float MassPerPipe
        {
            get { return Properties.MassPerPipe(ParcelLengthMetres); }
        }

        public float CapacityKilograms
        {
            get { return MassPerPipe * Math.Max(1, Pipes.Count); }
        }

        public float HeldKilograms
        {
            get { return CapacityKilograms * fill; }
        }

        public float RefillJoulesPerKilogram
        {
            get
            {
                return (Properties.SpecificHeat * Properties.RefillEquivalentKelvin) / heatTimeScale;
            }
        }

        public bool HasDrivingPump
        {
            get
            {
                for (int i = 0; i < Pumps.Count; i++)
                {
                    CoolantPump pump = Pumps[i];
                    if (pump == null || !pump.Enabled) continue;
                    if (pump.Speed > 0f) return true;
                }

                return false;
            }
        }

        public float RefillDemandWatts
        {
            get
            {
                if (fill >= 1f || !HasDrivingPump) return 0f;
                return Properties.RefillWattsAt(heatTimeScale);
            }
        }


        public float Refill(float deltaSeconds)
        {

            return Refill(deltaSeconds, 1f);
        }


        public float Refill(float deltaSeconds, float availableFraction)
        {
            if (deltaSeconds <= 0f || fill >= 1f || !HasDrivingPump) return 0f;

            float available = availableFraction < 0f ? 0f
                : (availableFraction > 1f ? 1f : availableFraction);
            if (available <= 0f) return 0f;

            float capacity = CapacityKilograms;
            if (capacity <= 0f) return 0f;

            float wanted = Properties.RefillKilogramsPerSecond * available * deltaSeconds;
            float room = (1f - fill) * capacity;
            float added = wanted < room ? wanted : room;
            if (added <= 0f) return 0f;

            FillFraction = fill + added / capacity;

            return (added * RefillJoulesPerKilogram) / deltaSeconds;
        }


        public float Vent(float ambientKelvin)
        {
            if (fill <= 0f) return 0f;

            float excess = Temperature - ambientKelvin;
            float heat = excess > 0f ? excess * ThermalMass : 0f;

            FillFraction = 0f;
            return heat;
        }


        public float LinkConductance(int index)
        {
            if (index < 0 || index >= Links.Count) return 0f;
            return Links[index].Conductance * fill * StagnantFactor;
        }

        public float StagnantFactor
        {
            get
            {
                if (FlowSegmentsPerSecond != 0f) return 1f;

                float fraction = Properties.StagnantTransferFraction;
                return fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
            }
        }

        public float FlowSegmentsPerSecond;

        public float ParcelLengthMetres = 2.5f;

        public float FlowMetresPerSecond
        {
            get { return FlowSegmentsPerSecond * ParcelLengthMetres; }
        }

        public bool HasPump;


        public readonly List<CoolantPump> Pumps = new List<CoolantPump>();


        public void RefreshFlow()
        {
            float demand = 0f;
            for (int i = 0; i < Pumps.Count; i++)
            {
                demand += Pumps[i].Contribution;
            }

            if (demand == 0f)
            {
                FlowSegmentsPerSecond = 0f;
                return;
            }

            float parcelLength = ParcelLengthMetres > 0f ? ParcelLengthMetres : 1f;
            float parcelsAtFullFlow = Properties.FlowRateFor(parcelLength) / parcelLength;

            float magnitude = parcelsAtFullFlow * (float)Math.Sqrt(Math.Abs(demand));
            FlowSegmentsPerSecond = demand < 0f ? -magnitude : magnitude;
        }

        public float PumpDemand
        {
            get
            {
                float demand = 0f;
                for (int i = 0; i < Pumps.Count; i++) demand += Pumps[i].Contribution;
                return demand;
            }
        }


        public long Signature { get; private set; }

        public float HeatTimeScale
        {
            get { return heatTimeScale; }
            set
            {
                heatTimeScale = value > 0f ? value : 1f;
                RefreshThermalMass();
            }
        }

        private float heatTimeScale = 1f;


        public CoolantLoop(LoopThermalProperties properties, float initialTemperature)
            : this(properties, initialTemperature, 1f)
        {
        }


        public CoolantLoop(LoopThermalProperties properties, float initialTemperature, float heatTimeScale)
        {
            Properties = (properties ?? LoopThermalProperties.Default()).Clone().Clamp();
            Temperature = initialTemperature;
            this.heatTimeScale = heatTimeScale > 0f ? heatTimeScale : 1f;
            RefreshThermalMass();
        }

        public int PipeCount
        {
            get { return Pipes.Count; }
        }


        public void RefreshThermalMass()
        {
            float perSegment =
                (fill * Properties.SpecificHeat * MassPerPipe) / heatTimeScale;
            SegmentThermalMass = Math.Max(ThermalConstants.MinimumThermalMass, perSegment);

            int count = wellMixed ? Math.Min(1, Pipes.Count) : Pipes.Count;
            if (wellMixed) SegmentThermalMass = SegmentThermalMass * Math.Max(1, Pipes.Count);

            if (count != segmentCount)
            {
                float[] resized = new float[count];
                for (int i = 0; i < count; i++)
                {
                    resized[i] = i < segmentCount ? segments[i] : seeded;
                }
                segments = resized;
                segmentCount = count;

                shift = 0;
                travelled = 0f;
            }

            ThermalMass = SegmentThermalMass * Math.Max(1, count);
        }


        public void RefreshSignature()
        {
            if (Pipes.Count == 0)
            {
                Signature = 0L;
                return;
            }

            unchecked
            {
                long combinedXor = 0L;
                long combinedSum = 0L;

                for (int i = 0; i < Pipes.Count; i++)
                {

                    long mixed = Mix(Pipes[i].Key);
                    combinedXor ^= mixed;
                    combinedSum += mixed;
                }

                long signature = (combinedXor * 31L) + combinedSum + Pipes.Count;
                Signature = signature == 0L ? 1L : signature;
            }
        }


        private static long Mix(long value)
        {
            unchecked
            {
                ulong x = (ulong)value + 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                x = x ^ (x >> 31);
                return (long)x;
            }
        }


        public bool Contains(BlockInstance block)
        {
            for (int i = 0; i < Pipes.Count; i++)
            {
                if (Pipes[i] == block) return true;
            }
            return false;
        }

        public float Energy
        {
            get
            {
                if (segments == null || segmentCount == 0) return seeded * ThermalMass;

                float total = 0f;
                for (int i = 0; i < segmentCount; i++) total += segments[i];
                return total * SegmentThermalMass;
            }
        }

        internal float[] SegmentWatts
        {
            get { return segmentWatts; }
        }

        private float[] segmentWatts = new float[0];


        internal void EnsureSegmentWatts()
        {
            if (segmentWatts.Length != segmentCount) segmentWatts = new float[segmentCount];
        }


        internal void ClearSegmentWatts()
        {
            if (segmentWatts.Length != segmentCount) segmentWatts = new float[segmentCount];
            else Array.Clear(segmentWatts, 0, segmentWatts.Length);
        }


        internal void ApplyWattsAndAdvect(float h, float effectiveMass)
        {
            int count = Math.Min(segmentCount, segmentWatts.Length);
            float parcels = FlowSegmentsPerSecond * h;
            bool mixing = segmentCount > 1 && (parcels > 1f || parcels < -1f);
            float total = 0f;
            float[] temperatures = segments;
            float[] watts = segmentWatts;

            if (mixing)
            {
                // The original mixing pass sums the applied, clamped values in parcel order.
                // Accumulate that same sum here to avoid rereading the entire ring.
                for (int i = 0; i < count; i++)
                {
                    float updated = temperatures[i] + (watts[i] * h / effectiveMass);
                    temperatures[i] = updated < ThermalConstants.MinimumTemperature
                        ? ThermalConstants.MinimumTemperature : updated;
                    total += temperatures[i];
                }
                // Public topology edits may resize parcels before the watts buffer is refreshed.
                for (int i = count; i < segmentCount; i++) total += temperatures[i];
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    float updated = temperatures[i] + (watts[i] * h / effectiveMass);
                    temperatures[i] = updated < ThermalConstants.MinimumTemperature
                        ? ThermalConstants.MinimumTemperature : updated;
                }
            }

            Advect(h, mixing, total);
        }

        public bool WellMixed
        {
            get { return wellMixed; }
            set
            {
                if (wellMixed == value) return;

                wellMixed = value;
                RefreshThermalMass();
            }
        }

        private bool wellMixed;


        public void Advect(float h)
        {
            Advect(h, false, 0f);
        }


        private void Advect(float h, bool hasTotal, float total)
        {
            if (segmentCount < 2 || FlowSegmentsPerSecond == 0f) return;

            float parcels = FlowSegmentsPerSecond * h;
            if (parcels == 0f) return;

            travelled += parcels;

            int whole = (int)travelled;
            if (whole != 0)
            {
                travelled -= whole;
                shift = (shift + whole) % segmentCount;
                if (shift < 0) shift += segmentCount;
            }

            float magnitude = parcels < 0f ? -parcels : parcels;
            if (magnitude > 1f) MixToward(1f - (1f / magnitude), hasTotal, total);
        }


        private void MixToward(float fraction, bool hasTotal, float total)
        {
            if (segmentCount < 2) return;
            if (fraction <= 0f) return;
            if (fraction > 1f) fraction = 1f;

            if (!hasTotal)
            {
                total = 0f;
                for (int i = 0; i < segmentCount; i++) total += segments[i];
            }

            float mean = total / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                segments[i] += fraction * (mean - segments[i]);
            }
        }


        public float MixingFraction(float substepSeconds)
        {
            float parcels = FlowSegmentsPerSecond * substepSeconds;
            if (parcels < 0f) parcels = -parcels;
            if (parcels <= 1f) return 0f;
            return 1f - (1f / parcels);
        }


        public float LastWattsAbsorbed;

        public float LastWattsRejected;

        public float LastNetWatts
        {
            get { return LastWattsAbsorbed - LastWattsRejected; }
        }

        internal float AbsorbedEnergy;
        internal float RejectedEnergy;


        internal void BeginStep()
        {
            AbsorbedEnergy = 0f;
            RejectedEnergy = 0f;
        }


        internal void EndStep(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            LastWattsAbsorbed = AbsorbedEnergy / deltaSeconds;
            LastWattsRejected = RejectedEnergy / deltaSeconds;
        }
    }
}
