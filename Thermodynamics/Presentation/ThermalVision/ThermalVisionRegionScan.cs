using System;
using System.Collections.Generic;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Presentation
{
    public sealed class ThermalVisionRegionScan : IDisposable
    {
        private sealed class Source
        {
            public IEnumerator<Region> Reader;
            public bool Rasterizing;
            public Vector3I Min, Max, Cell;
            public float Kelvin;
            public Vector3D BoundsMin, BoundsMax;
        }
        private readonly int capacity, sourceLimit;
        private double cellSize;
        private readonly double maxCellSize;
        public double CellSize { get { return cellSize; } }
        public int Coarsenings { get; private set; }

        private readonly List<Source> sources = new List<Source>();
        private readonly Dictionary<Vector3I, float> working = new Dictionary<Vector3I, float>();
        private readonly bool average;
        private readonly Vector3D origin;
        public Vector3D Origin { get { return origin; } }
        private readonly Dictionary<Vector3I, Vector2D> moments = new Dictionary<Vector3I, Vector2D>();

        private List<Region> published = new List<Region>();

        private List<Region> staged = new List<Region>();
        private bool publishing;
        private Dictionary<Vector3I, float>.Enumerator publication;
        private int cursor;
        public bool Running { get; private set; }
        public string Failure { get; private set; }
        public long Generation { get; private set; }
        public int SampleCount { get; private set; }
        public int Count { get { return published.Count; } }
        public Region this[int index] { get { return published[index]; } }


        public ThermalVisionRegionScan(double cellSize, int capacity, int sourceLimit, double maxCellSize = 0, bool average = false, Vector3D? origin = null)
        {
            if (double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0 || capacity < 1 || sourceLimit < 1)
                throw new ArgumentException("Positive finite scan limits required");
            if (maxCellSize == 0) maxCellSize = cellSize;
            if (average && maxCellSize != cellSize) throw new ArgumentException("Mean scans require a fixed, already budgeted cell size");
            if (double.IsNaN(maxCellSize) || double.IsInfinity(maxCellSize) || maxCellSize < cellSize)
                throw new ArgumentException("Maximum cell size must cover initial size");
            this.maxCellSize = maxCellSize;
            this.average = average;
            this.origin = origin ?? Vector3D.Zero;
            double originSum = this.origin.X + this.origin.Y + this.origin.Z;
            if (double.IsNaN(originSum) || double.IsInfinity(originSum)) throw new ArgumentException("Finite origin required");
            this.cellSize = cellSize; this.capacity = capacity; this.sourceLimit = sourceLimit;
        }


        public bool Start(IList<IEnumerable<Region>> input)
        {
            Cancel(); Failure = null; SampleCount = 0; Coarsenings = 0;
            if (input == null || input.Count > sourceLimit) { Failure = "source-limit"; return false; }
            try
            {
                foreach (IEnumerable<Region> sequence in input)
                    sources.Add(new Source { Reader = sequence.GetEnumerator() });
            }
            catch (Exception) { Fail("source-start-error"); return false; }
            Running = true;
            return true;
        }


        public int Advance(int workLimit)
        {
            if (workLimit < 1) throw new ArgumentException("Positive work budget required");
            int work = 0;
            try
            {
                while (Running && work < workLimit)
                {
                    work++;
                    if (sources.Count == 0)
                    {
                        if (!publishing) { publication = working.GetEnumerator(); publishing = true; }
                        if (publication.MoveNext())
                        {
                            KeyValuePair<Vector3I, float> entry = publication.Current;
                            Vector3D min = origin + (Vector3D)entry.Key * cellSize;
                            staged.Add(new Region(min, min + new Vector3D(cellSize), entry.Value));
                        }
                        else
                        {
                            publication.Dispose(); publishing = false;

                            published = staged; staged = new List<Region>();
                            working.Clear(); moments.Clear(); Running = false; Generation++;
                        }
                        continue;
                    }
                    Source source = sources[cursor];
                    if (source.Rasterizing)
                    {
                        float previous;
                        if (working.TryGetValue(source.Cell, out previous))
                            working[source.Cell] = Math.Max(previous, source.Kelvin);
                        else
                        {
                            if (working.Count >= capacity)
                            {
                                if (Coarsen()) continue;
                                Fail("cell-limit"); break;
                            }
                            working.Add(source.Cell, source.Kelvin);
                        }
                        if (average)
                        {
                            Vector3D cellMin = origin + (Vector3D)source.Cell * cellSize;
                            Vector3D extent = Vector3D.Min(source.BoundsMax, cellMin + new Vector3D(cellSize))
                                - Vector3D.Max(source.BoundsMin, cellMin);
                            double volume = Math.Max(0, extent.X) * Math.Max(0, extent.Y) * Math.Max(0, extent.Z);
                            Vector2D moment;
                            moments.TryGetValue(source.Cell, out moment);
                            moment.X += source.Kelvin * volume; moment.Y += volume;
                            moments[source.Cell] = moment;
                            working[source.Cell] = moment.Y > 0 ? (float)(moment.X / moment.Y) : source.Kelvin;
                        }
                        if (source.Cell.X < source.Max.X) source.Cell.X++;

                        else if (source.Cell.Y < source.Max.Y) { source.Cell.X = source.Min.X; source.Cell.Y++; }

                        else if (source.Cell.Z < source.Max.Z) { source.Cell.X = source.Min.X; source.Cell.Y = source.Min.Y; source.Cell.Z++; }
                        else source.Rasterizing = false;
                    }
                    else if (source.Reader.MoveNext())
                    {
                        Region sample = source.Reader.Current;
                        if (!Prepare(source, sample)) { Fail("invalid-sample"); break; }
                        SampleCount++;
                    }
                    else
                    {
                        sources.RemoveAt(cursor);
                        source.Reader.Dispose();
                        if (sources.Count == 0) continue;
                        cursor %= sources.Count;
                        continue;
                    }
                    cursor = (cursor + 1) % sources.Count;
                }
            }
            catch (Exception) { Fail("source-or-raster-error"); }
            return work;
        }


        private bool Coarsen()
        {
            if (cellSize > maxCellSize / 2) return false;
            var merged = new Dictionary<Vector3I, float>();
            foreach (KeyValuePair<Vector3I, float> entry in working)
            {

                Vector3I parent = Parent(entry.Key);
                float previous;
                merged[parent] = merged.TryGetValue(parent, out previous) ? Math.Max(previous, entry.Value) : entry.Value;
            }
            working.Clear();
            foreach (KeyValuePair<Vector3I, float> entry in merged) working.Add(entry.Key, entry.Value);
            foreach (Source source in sources)
                if (source.Rasterizing)
                {

                    source.Min = Parent(source.Min); source.Max = Parent(source.Max);
                    source.Cell = source.Min;
                }
            cellSize *= 2; Coarsenings++;
            return true;
        }


        private static Vector3I Parent(Vector3I cell)
        { return new Vector3I((int)Math.Floor(cell.X / 2.0), (int)Math.Floor(cell.Y / 2.0), (int)Math.Floor(cell.Z / 2.0)); }


        private bool Prepare(Source source, Region sample)
        {
            Vector3D a = (sample.Min - origin) / cellSize, b = (sample.Max - origin) / cellSize;
            double sum = a.X + a.Y + a.Z + b.X + b.Y + b.Z;
            if (double.IsNaN(sum) || double.IsInfinity(sum) || float.IsNaN(sample.Kelvin)
                || float.IsInfinity(sample.Kelvin) || sample.Kelvin < 0 || sample.Kelvin > ThermalVisionTemperatureData.OverRangeKelvin
                || a.X >= b.X || a.Y >= b.Y || a.Z >= b.Z) return false;
            const double bound = 1000000000;
            if (a.X < -bound || a.Y < -bound || a.Z < -bound || b.X > bound || b.Y > bound || b.Z > bound) return false;

            source.Min = new Vector3I((int)Math.Floor(a.X), (int)Math.Floor(a.Y), (int)Math.Floor(a.Z));

            source.Max = new Vector3I((int)Math.Ceiling(b.X) - 1, (int)Math.Ceiling(b.Y) - 1, (int)Math.Ceiling(b.Z) - 1);
            source.Cell = source.Min; source.Kelvin = sample.Kelvin; source.Rasterizing = true;
            source.BoundsMin = sample.Min; source.BoundsMax = sample.Max;
            return true;
        }


        private void Fail(string reason) { Cancel(); Failure = reason; }

        public void Cancel()
        {
            Running = false;
            foreach (Source source in sources)
            {
                try { source.Reader.Dispose(); } catch (Exception) {  }
            }
            if (publishing) publication.Dispose();
            publishing = false; staged.Clear();
            sources.Clear(); working.Clear(); moments.Clear(); cursor = 0;
        }

        public void Dispose() { Cancel(); published.Clear(); }
    }
}
